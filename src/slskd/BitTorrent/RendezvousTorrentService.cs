// <copyright file="RendezvousTorrentService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.BitTorrent;

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.BEncoding;
using MonoTorrent.Client;

/// <summary>
/// Service that creates and seeds a deterministic rendezvous torrent for peer discovery.
/// All slskdn instances create the same torrent and join the same swarm.
/// </summary>
public sealed class RendezvousTorrentService : IHostedService, IAsyncDisposable
{
    private readonly ILogger<RendezvousTorrentService> _logger;
    private readonly string _dataPath;
    private readonly BitTorrentOptions _options;
    
    // Deterministic torrent parameters (all instances MUST use the same values)
    private const string RendezvousTorrentName = "slskdn-mesh-rendezvous-v1";
    private const string RendezvousFileName = "slskdn-rendezvous.txt";
    private const long RendezvousFileSize = 1024; // 1KB
    private const int PieceLength = 16384; // 16KB
    
    // Well-known public tracker for the rendezvous swarm
    private static readonly string[] PublicTrackers = new[]
    {
        "udp://tracker.opentrackr.org:1337/announce",
        "udp://open.stealth.si:80/announce",
        "udp://tracker.torrent.eu.org:451/announce",
    };
    
    private ClientEngine? _engine;
    private TorrentManager? _manager;
    
    public RendezvousTorrentService(
        ILogger<RendezvousTorrentService> logger,
        string dataPath,
        BitTorrentOptions options)
    {
        _logger = logger;
        _dataPath = dataPath;
        _options = options;
    }
    
    public int ConnectedPeers
    {
        get
        {
            try
            {
                return _manager?.Peers?.Available ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableRendezvousTorrent)
        {
            _logger.LogInformation("BitTorrent rendezvous is disabled (set BitTorrent.EnableRendezvousTorrent=true to enable)");
            return;
        }
        
        _logger.LogInformation("Starting BitTorrent rendezvous service...");
        
        try
        {
            // Create the deterministic rendezvous file
            var torrentDir = Path.Combine(_dataPath, "bittorrent");
            Directory.CreateDirectory(torrentDir);
            
            var rendezvousFilePath = Path.Combine(torrentDir, RendezvousFileName);
            CreateDeterministicRendezvousFile(rendezvousFilePath);
            
            // Create the deterministic .torrent file
            var torrent = await CreateDeterministicTorrentAsync(rendezvousFilePath, torrentDir);
            
            // Initialize BitTorrent client engine
            var engineSettingsBuilder = new EngineSettingsBuilder
            {
                AllowPortForwarding = false, // Don't use UPnP
                AutoSaveLoadDhtCache = true,
                CacheDirectory = torrentDir,
            };
            
            var engineSettings = engineSettingsBuilder.ToSettings();
            _engine = new ClientEngine(engineSettings);
            
            // Add and start the rendezvous torrent
            var managerSettings = new TorrentSettingsBuilder
            {
                MaximumConnections = _options.MaxRendezvousPeers,
            }.ToSettings();
            
            _manager = await _engine.AddAsync(torrent, torrentDir, managerSettings);
            
            // TODO: Register SlskdnMeshExtension here when MonoTorrent 3.x API supports it
            // For now, we just seed the torrent
            
            await _manager.StartAsync();
            
            _logger.LogInformation(
                "BitTorrent rendezvous started - InfoHash: {InfoHash}, Trackers: {TrackerCount}",
                torrent.InfoHashes.V1?.ToHex() ?? torrent.InfoHashes.V2?.ToHex(),
                torrent.AnnounceUrls.Count);
            
            // Start DHT if enabled (separate from mesh DHT)
            if (_options.EnableDht && _engine != null)
            {
                // Note: MonoTorrent 3.x uses the same DhtEngine instance already created
                // No separate start needed
                _logger.LogDebug("BitTorrent DHT enabled");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start BitTorrent rendezvous service");
            throw;
        }
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableRendezvousTorrent || _engine == null)
        {
            return;
        }
        
        _logger.LogInformation("Stopping BitTorrent rendezvous service");
        
        try
        {
            if (_manager != null)
            {
                await _manager.StopAsync();
            }
            
            if (_engine != null)
            {
                await _engine.StopAllAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping BitTorrent service");
        }
    }
    
    public async ValueTask DisposeAsync()
    {
        // TorrentManager doesn't implement Dispose in MonoTorrent 3.x
        _engine?.Dispose();
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Creates a deterministic 1KB file with fixed content.
    /// All slskdn instances create the exact same file.
    /// </summary>
    private void CreateDeterministicRendezvousFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            var existing = File.ReadAllBytes(filePath);
            if (existing.Length == RendezvousFileSize)
            {
                _logger.LogDebug("Rendezvous file already exists with correct size");
                return;
            }
        }
        
        // Create deterministic content (same for all instances)
        var content = new byte[RendezvousFileSize];
        var header = Encoding.UTF8.GetBytes($"slskdn mesh rendezvous v1\nThis is a marker file for peer discovery.\nCreated: 2025-12-11\n");
        Array.Copy(header, content, Math.Min(header.Length, content.Length));
        
        // Fill rest with zeroes (deterministic)
        File.WriteAllBytes(filePath, content);
        
        _logger.LogDebug("Created deterministic rendezvous file: {Path}, size: {Size} bytes", filePath, RendezvousFileSize);
    }
    
    /// <summary>
    /// Creates a .torrent file with deterministic settings.
    /// All slskdn instances MUST create the same .torrent to join the same swarm.
    /// </summary>
    private async Task<Torrent> CreateDeterministicTorrentAsync(string filePath, string outputDir)
    {
        var torrentPath = Path.Combine(outputDir, $"{RendezvousTorrentName}.torrent");
        
        // Check if torrent already exists and is valid
        if (File.Exists(torrentPath))
        {
            try
            {
                var existing = await Torrent.LoadAsync(torrentPath);
                _logger.LogDebug("Loaded existing rendezvous torrent: {InfoHash}", existing.InfoHashes.V1?.ToHex());
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Existing torrent file invalid, recreating");
            }
        }
        
        // Create new torrent with deterministic settings
        var creator = new TorrentCreator(TorrentType.V1Only) // V1 for maximum compatibility
        {
            PieceLength = PieceLength,
            Private = false,
        };
        
        // Add trackers (deterministic order)
        foreach (var tracker in PublicTrackers)
        {
            creator.Announces.Add(new System.Collections.Generic.List<string> { tracker });
        }
        
        // Set creation date to a fixed timestamp (deterministic)
        creator.SetCustom("creation date", (BEncodedNumber)1733961600); // 2024-12-12 00:00:00 UTC
        creator.Comment = "slskdn mesh peer discovery rendezvous swarm";
        creator.CreatedBy = "slskdn mesh v1";
        
        // Create the torrent
        var torrentData = await creator.CreateAsync(new TorrentFileSource(filePath));
        
        // Save it
        await File.WriteAllBytesAsync(torrentPath, torrentData.Encode());
        
        // Load back as Torrent object
        var torrent = await Torrent.LoadAsync(torrentPath);
        
        _logger.LogInformation(
            "Created deterministic rendezvous torrent - InfoHash: {InfoHash}, Size: {Size} bytes",
            torrent.InfoHashes.V1?.ToHex(),
            torrentData.Encode().Length);
        
        return torrent;
    }
}

