// <copyright file="MeshPeerRegistry.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;

/// <summary>
/// SQLite-backed registry for mesh peer identities.
/// Provides in-memory caching for fast lookups.
/// </summary>
public sealed class MeshPeerRegistry : IMeshPeerRegistry, IDisposable
{
    private readonly ILogger<MeshPeerRegistry> _logger;
    private readonly string _dbPath;
    private readonly ConcurrentDictionary<MeshPeerId, MeshPeer> _cache = new();
    private bool _disposed;

    public MeshPeerRegistry(ILogger<MeshPeerRegistry> logger, string appDirectory)
    {
        _logger = logger;
        _dbPath = System.IO.Path.Combine(appDirectory, "mesh-peers.db");
        InitializeDatabase();
        LoadCacheAsync().GetAwaiter().GetResult();
    }

    public async Task<MeshPeer?> RegisterOrUpdateAsync(MeshPeerDescriptor descriptor, CancellationToken ct = default)
    {
        if (descriptor?.MeshPeerId == default)
        {
            _logger.LogWarning("Cannot register peer with empty MeshPeerId");
            return null;
        }

        var meshPeerId = descriptor.MeshPeerId;

        // Verify descriptor signature
        if (!descriptor.VerifySignature())
        {
            _logger.LogWarning("Invalid descriptor signature for mesh peer: {PeerId}", meshPeerId.ToShortString());
            return null;
        }
        
        var isVerified = true; // Signature verified above

        var peer = new MeshPeer
        {
            Id = meshPeerId,
            Descriptor = descriptor,
            IsVerified = isVerified,
            LastSeen = DateTimeOffset.UtcNow,
            SoulseekUsername = null, // Will be set via UpdateSoulseekAliasAsync
        };

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO mesh_peers (peer_id, descriptor_json, is_verified, last_seen_unix)
            VALUES ($peer_id, $descriptor, $verified, $last_seen)
            ON CONFLICT(peer_id) DO UPDATE SET
                descriptor_json = $descriptor,
                is_verified = $verified,
                last_seen_unix = $last_seen";
        
        cmd.Parameters.AddWithValue("$peer_id", meshPeerId.Value);
        cmd.Parameters.AddWithValue("$descriptor", System.Text.Json.JsonSerializer.Serialize(descriptor));
        cmd.Parameters.AddWithValue("$verified", isVerified ? 1 : 0);
        cmd.Parameters.AddWithValue("$last_seen", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await cmd.ExecuteNonQueryAsync(ct);

        _cache[meshPeerId] = peer;
        _logger.LogDebug("Registered mesh peer: {PeerId}", meshPeerId.ToShortString());
        
        return peer;
    }

    public async Task<MeshPeer?> TryGetAsync(MeshPeerId id, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT descriptor_json, is_verified, last_seen_unix, soulseek_username
            FROM mesh_peers WHERE peer_id = $peer_id";
        cmd.Parameters.AddWithValue("$peer_id", id.Value);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var descriptorJson = reader.GetString(0);
        var isVerified = reader.GetInt32(1) == 1;
        var lastSeenUnix = reader.GetInt64(2);
        var soulseekUsername = reader.IsDBNull(3) ? null : reader.GetString(3);

        var descriptor = System.Text.Json.JsonSerializer.Deserialize<MeshPeerDescriptor>(descriptorJson);
        if (descriptor == null)
        {
            return null;
        }

        var peer = new MeshPeer
        {
            Id = id,
            Descriptor = descriptor,
            IsVerified = isVerified,
            LastSeen = DateTimeOffset.FromUnixTimeSeconds(lastSeenUnix),
            SoulseekUsername = soulseekUsername,
        };

        _cache[id] = peer;
        return peer;
    }

    public async IAsyncEnumerable<MeshPeer> GetAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT peer_id, descriptor_json, is_verified, last_seen_unix, soulseek_username
            FROM mesh_peers WHERE is_verified = 1 ORDER BY last_seen_unix DESC";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var peerIdStr = reader.GetString(0);
            if (!MeshPeerId.TryParse(peerIdStr, out var peerId))
            {
                continue;
            }

            var descriptorJson = reader.GetString(1);
            var lastSeenUnix = reader.GetInt64(3);
            var soulseekUsername = reader.IsDBNull(4) ? null : reader.GetString(4);

            var descriptor = System.Text.Json.JsonSerializer.Deserialize<MeshPeerDescriptor>(descriptorJson);
            if (descriptor == null)
            {
                continue;
            }

            yield return new MeshPeer
            {
                Id = peerId,
                Descriptor = descriptor,
                IsVerified = true,
                LastSeen = DateTimeOffset.FromUnixTimeSeconds(lastSeenUnix),
                SoulseekUsername = soulseekUsername,
            };
        }
    }

    public async Task<bool> IsVerifiedAsync(MeshPeerId id, CancellationToken ct = default)
    {
        var peer = await TryGetAsync(id, ct);
        return peer?.IsVerified ?? false;
    }

    public async Task UpdateSoulseekAliasAsync(MeshPeerId id, string soulseekUsername, CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE mesh_peers SET soulseek_username = $username WHERE peer_id = $peer_id";
        cmd.Parameters.AddWithValue("$username", soulseekUsername);
        cmd.Parameters.AddWithValue("$peer_id", id.Value);

        await cmd.ExecuteNonQueryAsync(ct);

        if (_cache.TryGetValue(id, out var cached))
        {
            var updated = cached with { SoulseekUsername = soulseekUsername };
            _cache[id] = updated;
        }

        _logger.LogInformation("Mapped Soulseek username {Username} to mesh peer {PeerId}", 
            soulseekUsername, id.ToShortString());
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM mesh_peers WHERE is_verified = 1";
        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS mesh_peers (
                peer_id TEXT PRIMARY KEY NOT NULL,
                descriptor_json TEXT NOT NULL,
                is_verified INTEGER NOT NULL DEFAULT 1,
                last_seen_unix INTEGER NOT NULL,
                soulseek_username TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_mesh_peers_last_seen ON mesh_peers(last_seen_unix);
            CREATE INDEX IF NOT EXISTS idx_mesh_peers_soulseek ON mesh_peers(soulseek_username);";
        cmd.ExecuteNonQuery();
        
        _logger.LogInformation("Mesh peer registry database initialized at {Path}", _dbPath);
    }

    private async Task LoadCacheAsync()
    {
        var count = 0;
        await foreach (var peer in GetAllAsync())
        {
            _cache[peer.Id] = peer;
            count++;
        }
        _logger.LogInformation("Loaded {Count} mesh peers into cache", count);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cache.Clear();
            _disposed = true;
        }
    }
}
