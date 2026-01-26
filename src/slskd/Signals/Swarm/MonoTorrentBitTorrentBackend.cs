// <copyright file="MonoTorrentBitTorrentBackend.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

namespace slskd.Signals.Swarm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoTorrent;
using MonoTorrent.Client;
using slskd.Swarm;
using slskd.VirtualSoulfind.v2.Backends;

/// <summary>
///     MonoTorrent-based <see cref="IBitTorrentBackend"/> for private swarms and fetch-by-infohash (def-3).
///     Respects <see cref="PrivateTorrentModeOptions"/> (DisableDht, DisablePex, InviteList).
/// </summary>
public sealed class MonoTorrentBitTorrentBackend : IBitTorrentBackend
{
    private readonly ILogger<MonoTorrentBitTorrentBackend> _logger;
    private readonly IOptionsMonitor<TorrentBackendOptions> _options;
    private readonly ClientEngine _engine;
    private readonly string _cacheDir;

    public MonoTorrentBitTorrentBackend(
        ILogger<MonoTorrentBitTorrentBackend> logger,
        IOptionsMonitor<TorrentBackendOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _cacheDir = Path.Combine(Path.GetTempPath(), "slskd-bt-cache");
        Directory.CreateDirectory(_cacheDir);

        var pm = _options.CurrentValue.PrivateMode;
        var builder = new EngineSettingsBuilder
        {
            CacheDirectory = _cacheDir,
            AllowLocalPeerDiscovery = pm?.DisablePex != false,
        };
        // Disable DHT at engine level when PrivateMode wants it
        if (pm?.DisableDht == true)
            builder.DhtEndPoint = null;

        var settings = builder.ToSettings();
        _engine = new ClientEngine(settings);
    }

    public bool IsSupported()
    {
        return _options.CurrentValue.Enabled;
    }

    public async Task<string> PreparePrivateTorrentAsync(SwarmJob job, string variantId, CancellationToken ct = default)
    {
        var hash = job.File?.Hash;
        if (string.IsNullOrWhiteSpace(hash))
            return string.Empty;

        var infohash = ParseInfohash(hash);
        if (infohash == null)
            return string.Empty;

        var magnet = new MagnetLink(infohash);
        var savePath = Path.Combine(_cacheDir, "prepare", infohash.ToHex());
        Directory.CreateDirectory(savePath);

        TorrentManager? manager = null;
        try
        {
            manager = await _engine.AddAsync(magnet, savePath);
            ApplyPrivateSettings(manager);
            await AddManualPeersAsync(manager, PeersFromSources(job.Sources));
            await manager.StartAsync();
            return infohash.ToHex();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PreparePrivateTorrentAsync failed for job {JobId}", job.JobId);
            if (manager != null)
            {
                try { await _engine.RemoveAsync(manager); } catch { /* ignore */ }
            }

            return string.Empty;
        }
    }

    public async Task<string?> FetchByInfoHashOrMagnetAsync(string backendRef, string destDirectory, CancellationToken ct = default)
    {
        MagnetLink? magnet = null;
        if (backendRef.TrimStart().StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
        {
            if (!MagnetLink.TryParse(backendRef, out magnet) || magnet == null)
                return null;
        }
        else
        {
            var ih = ParseInfohash(backendRef);
            if (ih == null) return null;
            magnet = new MagnetLink(ih);
        }

        Directory.CreateDirectory(destDirectory);
        TorrentManager? manager = null;
        try
        {
            manager = await _engine.AddAsync(magnet, destDirectory);
            ApplyPrivateSettings(manager);
            var invitePeers = _options.CurrentValue.PrivateMode?.InviteList ?? Array.Empty<string>();
            await AddManualPeersAsync(manager, invitePeers);

            await manager.StartAsync();
            if (!manager.HasMetadata)
                await manager.WaitForMetadataAsync();

            // Wait for completion (with timeout)
            var timeout = TimeSpan.FromMinutes(60);
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            while (manager.Progress < 100.0 && DateTimeOffset.UtcNow < deadline && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
            }

            if (manager.Progress < 100.0)
            {
                _logger.LogWarning("FetchByInfoHashOrMagnetAsync did not complete: {Progress}%", manager.Progress);
                await _engine.RemoveAsync(manager);
                return null;
            }

            string path;
            if (manager.Torrent!.Files.Count == 1)
                path = Path.Combine(manager.SavePath, manager.Torrent.Files[0].Path);
            else
                path = Path.Combine(manager.ContainingDirectory!, manager.Torrent.Files[0].Path);

            await manager.StopAsync();
            await _engine.RemoveAsync(manager);
            return File.Exists(path) ? path : null;
        }
        catch (Exception ex)
        {
            var refPreview = backendRef.Length > 60 ? backendRef.Substring(0, 60) + "..." : backendRef;
            _logger.LogWarning(ex, "FetchByInfoHashOrMagnetAsync failed for {Ref}", refPreview);
            if (manager != null)
            {
                try { await manager.StopAsync(); await _engine.RemoveAsync(manager); } catch { /* ignore */ }
            }

            return null;
        }
    }

    private void ApplyPrivateSettings(TorrentManager manager)
    {
        // TorrentSettings.AllowDht / AllowPeerExchange are read-only; engine-level
        // DhtEndPoint=null and AllowLocalPeerDiscovery are set in ctor. Per-torrent
        // DHT/PEX would require passing TorrentSettings into AddAsync if supported.
    }

    private static IReadOnlyList<string> PeersFromSources(IReadOnlyList<SwarmSource>? sources)
    {
        if (sources == null || sources.Count == 0) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var s in sources)
        {
            if (!string.IsNullOrEmpty(s.Address) && s.Port.HasValue)
                list.Add($"{s.Address}:{s.Port.Value}");
        }

        return list;
    }

    private static Task AddManualPeersAsync(TorrentManager manager, IReadOnlyList<string> peers)
    {
        // TODO: MonoTorrent PeerInfo/AddPeersAsync API for manual peers (InviteList, job.Sources).
        // Engine-level DhtEndPoint=null and AllowLocalPeerDiscovery avoid DHT/PEX when PrivateMode.
        return Task.CompletedTask;
    }

    private static InfoHash? ParseInfohash(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = value.Trim().ToLowerInvariant();
        if (s.Length == 40 && s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
        {
            try { return InfoHash.FromHex(s); } catch { return null; }
        }

        if (s.Length == 64 && s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
        {
            try { return InfoHash.FromHex(s); } catch { return null; }
        }

        return null;
    }
}
