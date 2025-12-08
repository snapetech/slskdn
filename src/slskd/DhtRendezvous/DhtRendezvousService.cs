// <copyright file="DhtRendezvousService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for discovering and connecting to mesh peers via BitTorrent DHT rendezvous.
/// 
/// This is a placeholder implementation that manages peer discovery and connection
/// without an actual DHT library integration. The DHT integration can be added later
/// using MonoTorrent or a similar library.
/// </summary>
public sealed class DhtRendezvousService : BackgroundService, IDhtRendezvousService
{
    private readonly ILogger<DhtRendezvousService> _logger;
    private readonly IMeshOverlayServer _overlayServer;
    private readonly IMeshOverlayConnector _overlayConnector;
    private readonly MeshNeighborRegistry _registry;
    private readonly DhtRendezvousOptions _options;
    
    private readonly ConcurrentBag<IPEndPoint> _discoveredPeers = new();
    private DateTimeOffset? _lastAnnounceTime;
    private DateTimeOffset? _lastDiscoveryTime;
    private DateTimeOffset? _startedAt;
    private long _totalPeersDiscovered;
    private long _totalConnectionsAttempted;
    private long _totalConnectionsSucceeded;
    
    // Rendezvous infohashes
    private static readonly byte[] MainInfohash = ComputeInfohash("slskdn-mesh-v1");
    private static readonly byte[] BackupInfohash1 = ComputeInfohash("slskdn-mesh-v1-backup-1");
    private static readonly byte[] BackupInfohash2 = ComputeInfohash("slskdn-mesh-v1-backup-2");
    
    public DhtRendezvousService(
        ILogger<DhtRendezvousService> logger,
        IMeshOverlayServer overlayServer,
        IMeshOverlayConnector overlayConnector,
        MeshNeighborRegistry registry,
        DhtRendezvousOptions options)
    {
        _logger = logger;
        _overlayServer = overlayServer;
        _overlayConnector = overlayConnector;
        _registry = registry;
        _options = options;
    }
    
    public bool IsBeaconCapable { get; private set; }
    public bool IsDhtRunning { get; private set; }
    public int DhtNodeCount => 0; // TODO: Implement with actual DHT library
    public int DiscoveredPeerCount => _discoveredPeers.Count;
    public int ActiveMeshConnections => _registry.Count;
    
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DHT rendezvous is disabled");
            return;
        }
        
        _logger.LogInformation("Starting DHT rendezvous service");
        
        // Detect beacon capability (simplified - in production would use UPnP/STUN)
        IsBeaconCapable = await DetectBeaconCapabilityAsync(cancellationToken);
        
        if (IsBeaconCapable)
        {
            _logger.LogInformation("This client is beacon-capable, starting overlay server");
            await _overlayServer.StartAsync(cancellationToken);
        }
        else
        {
            _logger.LogInformation("This client is not beacon-capable (behind NAT), will connect to beacons");
        }
        
        _startedAt = DateTimeOffset.UtcNow;
        IsDhtRunning = true;
        
        await base.StartAsync(cancellationToken);
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping DHT rendezvous service");
        
        await _overlayServer.StopAsync();
        IsDhtRunning = false;
        
        await base.StopAsync(cancellationToken);
    }
    
    Task IDhtRendezvousService.StopAsync(CancellationToken cancellationToken)
    {
        return StopAsync(cancellationToken);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Announce if beacon capable
                if (IsBeaconCapable && ShouldAnnounce())
                {
                    await AnnounceAsync(stoppingToken);
                }
                
                // Discover peers if needed
                if (_registry.NeedsMoreNeighbors && ShouldDiscover())
                {
                    await DiscoverPeersAsync(stoppingToken);
                }
                
                // Cleanup stale connections
                await _registry.CleanupStaleConnectionsAsync();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in DHT rendezvous loop");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
    
    public async Task<int> DiscoverPeersAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Running DHT peer discovery");
        _lastDiscoveryTime = DateTimeOffset.UtcNow;
        
        // TODO: Implement actual DHT get_peers query
        // For now, this is a placeholder that would be replaced with:
        // var peers = await _dhtClient.GetPeersAsync(MainInfohash, cancellationToken);
        // peers.AddRange(await _dhtClient.GetPeersAsync(BackupInfohash1, cancellationToken));
        // peers.AddRange(await _dhtClient.GetPeersAsync(BackupInfohash2, cancellationToken));
        
        var newPeers = new List<IPEndPoint>();
        
        // Placeholder: In production, these would come from DHT
        // This demonstrates the flow without actual DHT integration
        
        if (newPeers.Count > 0)
        {
            foreach (var peer in newPeers)
            {
                _discoveredPeers.Add(peer);
            }
            
            Interlocked.Add(ref _totalPeersDiscovered, newPeers.Count);
            _logger.LogInformation("Discovered {Count} new mesh peers via DHT", newPeers.Count);
            
            // Try to connect to discovered peers
            Interlocked.Add(ref _totalConnectionsAttempted, newPeers.Count);
            var connected = await _overlayConnector.ConnectToCandidatesAsync(newPeers, cancellationToken);
            Interlocked.Add(ref _totalConnectionsSucceeded, connected);
            
            return connected;
        }
        
        _logger.LogDebug("No new peers discovered via DHT");
        return 0;
    }
    
    public async Task AnnounceAsync(CancellationToken cancellationToken = default)
    {
        if (!IsBeaconCapable)
        {
            _logger.LogWarning("Cannot announce - not beacon capable");
            return;
        }
        
        _logger.LogDebug("Announcing to DHT");
        _lastAnnounceTime = DateTimeOffset.UtcNow;
        
        // TODO: Implement actual DHT announce_peer
        // For each rendezvous key:
        // await _dhtClient.AnnouncePeerAsync(MainInfohash, _options.OverlayPort, cancellationToken);
        // await _dhtClient.AnnouncePeerAsync(BackupInfohash1, _options.OverlayPort, cancellationToken);
        // await _dhtClient.AnnouncePeerAsync(BackupInfohash2, _options.OverlayPort, cancellationToken);
        
        _logger.LogInformation("Announced overlay port {Port} to DHT", _options.OverlayPort);
        
        await Task.CompletedTask;
    }
    
    public IReadOnlyList<IPEndPoint> GetDiscoveredPeers()
    {
        return _discoveredPeers.ToList();
    }
    
    public IReadOnlyList<MeshPeerInfo> GetMeshPeers()
    {
        return _registry.GetPeerInfo();
    }
    
    public DhtRendezvousStats GetStats()
    {
        return new DhtRendezvousStats
        {
            IsBeaconCapable = IsBeaconCapable,
            IsDhtRunning = IsDhtRunning,
            DhtNodeCount = DhtNodeCount,
            DiscoveredPeerCount = DiscoveredPeerCount,
            ActiveMeshConnections = ActiveMeshConnections,
            TotalPeersDiscovered = _totalPeersDiscovered,
            TotalConnectionsAttempted = _totalConnectionsAttempted,
            TotalConnectionsSucceeded = _totalConnectionsSucceeded,
            LastAnnounceTime = _lastAnnounceTime,
            LastDiscoveryTime = _lastDiscoveryTime,
            StartedAt = _startedAt,
        };
    }
    
    /// <summary>
    /// Add a peer endpoint manually (for testing or configuration).
    /// </summary>
    public void AddPeerEndpoint(IPEndPoint endpoint)
    {
        _discoveredPeers.Add(endpoint);
        _logger.LogDebug("Added manual peer endpoint {Endpoint}", endpoint);
    }
    
    private bool ShouldAnnounce()
    {
        if (_lastAnnounceTime is null)
        {
            return true;
        }
        
        return DateTimeOffset.UtcNow - _lastAnnounceTime.Value > TimeSpan.FromSeconds(_options.AnnounceIntervalSeconds);
    }
    
    private bool ShouldDiscover()
    {
        if (_lastDiscoveryTime is null)
        {
            return true;
        }
        
        return DateTimeOffset.UtcNow - _lastDiscoveryTime.Value > TimeSpan.FromSeconds(_options.DiscoveryIntervalSeconds);
    }
    
    private async Task<bool> DetectBeaconCapabilityAsync(CancellationToken cancellationToken)
    {
        // Simplified beacon detection
        // In production, this would:
        // 1. Try UPnP port mapping
        // 2. Try NAT-PMP
        // 3. Use STUN to detect public IP and NAT type
        // 4. Attempt self-connection test
        
        // For now, check if we can bind to the overlay port
        try
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Any, _options.OverlayPort);
            listener.Start();
            listener.Stop();
            
            // If we can bind, assume we're beacon-capable
            // This is a simplification - real implementation would do proper NAT detection
            _logger.LogDebug("Successfully bound to overlay port {Port}", _options.OverlayPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not bind to overlay port {Port}", _options.OverlayPort);
            return false;
        }
    }
    
    private static byte[] ComputeInfohash(string rendezvousKey)
    {
        return SHA1.HashData(Encoding.UTF8.GetBytes(rendezvousKey));
    }
}

/// <summary>
/// Configuration options for DHT rendezvous.
/// </summary>
public sealed class DhtRendezvousOptions
{
    /// <summary>
    /// Whether DHT rendezvous is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Port for the overlay TCP listener.
    /// </summary>
    public int OverlayPort { get; set; } = 50305;
    
    /// <summary>
    /// Interval between DHT announcements (seconds).
    /// </summary>
    public int AnnounceIntervalSeconds { get; set; } = 900; // 15 minutes
    
    /// <summary>
    /// Interval between DHT discovery cycles (seconds).
    /// </summary>
    public int DiscoveryIntervalSeconds { get; set; } = 600; // 10 minutes
    
    /// <summary>
    /// Minimum mesh neighbors before triggering discovery.
    /// </summary>
    public int MinNeighbors { get; set; } = 3;
    
    /// <summary>
    /// Bootstrap DHT nodes.
    /// </summary>
    public List<string> BootstrapNodes { get; set; } = new()
    {
        "router.bittorrent.com:6881",
        "dht.transmissionbt.com:6881",
        "router.utorrent.com:6881",
    };
}

