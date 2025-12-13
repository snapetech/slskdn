// <copyright file="SoulseekMeshIdentityMapper.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQLite-backed bidirectional mapper between Soulseek usernames and mesh peer IDs.
/// </summary>
public sealed class SoulseekMeshIdentityMapper : ISoulseekMeshIdentityMapper, IDisposable
{
    private readonly ILogger<SoulseekMeshIdentityMapper> _logger;
    private readonly string _dbPath;
    private readonly ConcurrentDictionary<string, MeshPeerId> _usernameToId = new();
    private readonly ConcurrentDictionary<MeshPeerId, string> _idToUsername = new();
    private bool _disposed;

    public SoulseekMeshIdentityMapper(ILogger<SoulseekMeshIdentityMapper> logger, string appDirectory)
    {
        _logger = logger;
        _dbPath = System.IO.Path.Combine(appDirectory, "mesh-peers.db");
        EnsureTablesExist();
        LoadCacheAsync().GetAwaiter().GetResult();
    }

    private void EnsureTablesExist()
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
    }

    public async Task MapAsync(string soulseekUsername, MeshPeerId meshPeerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(soulseekUsername))
        {
            throw new ArgumentException("Soulseek username cannot be empty", nameof(soulseekUsername));
        }

        if (!meshPeerId.IsValid)
        {
            throw new ArgumentException("Mesh peer ID is invalid", nameof(meshPeerId));
        }

        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(ct);

        // Update mesh_peers table with Soulseek username
        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE mesh_peers SET soulseek_username = $username WHERE peer_id = $peer_id";
        cmd.Parameters.AddWithValue("$username", soulseekUsername);
        cmd.Parameters.AddWithValue("$peer_id", meshPeerId.Value);

        var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);
        
        if (rowsAffected > 0)
        {
            _usernameToId[soulseekUsername.ToLowerInvariant()] = meshPeerId;
            _idToUsername[meshPeerId] = soulseekUsername;
            
            _logger.LogDebug("Mapped Soulseek username {Username} to mesh peer {PeerId}",
                soulseekUsername, meshPeerId.ToShortString());
        }
        else
        {
            _logger.LogWarning("Failed to map username {Username} - mesh peer {PeerId} not found in registry",
                soulseekUsername, meshPeerId.ToShortString());
        }
    }

    public Task<MeshPeerId?> TryGetMeshPeerIdAsync(string soulseekUsername, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(soulseekUsername))
        {
            return Task.FromResult<MeshPeerId?>(null);
        }

        var normalized = soulseekUsername.ToLowerInvariant();
        return Task.FromResult(_usernameToId.TryGetValue(normalized, out var peerId) ? (MeshPeerId?)peerId : null);
    }

    public Task<string?> TryGetSoulseekUsernameAsync(MeshPeerId meshPeerId, CancellationToken ct = default)
    {
        if (!meshPeerId.IsValid)
        {
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(_idToUsername.TryGetValue(meshPeerId, out var username) ? username : (string?)null);
    }

    private async Task LoadCacheAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT peer_id, soulseek_username 
            FROM mesh_peers 
            WHERE soulseek_username IS NOT NULL";

        await using var reader = await cmd.ExecuteReaderAsync();
        var count = 0;
        
        while (await reader.ReadAsync())
        {
            var peerIdStr = reader.GetString(0);
            var username = reader.GetString(1);

            if (MeshPeerId.TryParse(peerIdStr, out var peerId))
            {
                _usernameToId[username.ToLowerInvariant()] = peerId;
                _idToUsername[peerId] = username;
                count++;
            }
        }

        _logger.LogInformation("Loaded {Count} Soulseek username mappings into cache", count);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _usernameToId.Clear();
            _idToUsername.Clear();
            _disposed = true;
        }
    }
}














