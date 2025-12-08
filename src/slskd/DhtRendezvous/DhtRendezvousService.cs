// <copyright file="DhtRendezvousService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;

/// <summary>
/// Service for discovering and connecting to mesh peers via BitTorrent DHT rendezvous.
/// 
/// Uses MonoTorrent's DHT implementation to:
/// - Beacons: announce_peer on rendezvous infohash to advertise overlay port
/// - Seekers: get_peers on rendezvous infohash to discover beacons
/// </summary>
public sealed class DhtRendezvousService : BackgroundService, IDhtRendezvousService
{
    private readonly ILogger<DhtRendezvousService> _logger;
    private readonly IMeshOverlayServer _overlayServer;
    private readonly IMeshOverlayConnector _overlayConnector;
    private readonly MeshNeighborRegistry _registry;
    private readonly DhtRendezvousOptions _options;
    
    // MonoTorrent DHT components
    private DhtEngine? _dhtEngine;
    private IDhtListener? _dhtListener;
    
    // SECURITY: Use a bounded collection to prevent memory exhaustion
    private readonly ConcurrentDictionary<string, IPEndPoint> _discoveredPeers = new();
    private const int MaxDiscoveredPeers = 1000;
    private DateTimeOffset? _lastAnnounceTime;
    private DateTimeOffset? _lastDiscoveryTime;
    private DateTimeOffset? _startedAt;
    private long _totalPeersDiscovered;
    private long _totalConnectionsAttempted;
    private long _totalConnectionsSucceeded;
    
    // Rendezvous infohashes (SHA1 of key strings)
    private static readonly InfoHash MainInfohash = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1")));
    private static readonly InfoHash BackupInfohash1 = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1-backup-1")));
    private static readonly InfoHash BackupInfohash2 = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1-backup-2")));
    
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
    public bool IsDhtRunning => _dhtEngine?.State == DhtState.Ready;
    public int DhtNodeCount => _dhtEngine?.NodeCount ?? 0;
    public int DiscoveredPeerCount => _discoveredPeers.Values.Count;
    public int ActiveMeshConnections => _registry.Count;
    
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DHT rendezvous is disabled");
            return;
        }
        
        _logger.LogInformation("Starting DHT rendezvous service with MonoTorrent DHT");
        
        try
        {
            // Initialize MonoTorrent DHT
            await InitializeDhtAsync(cancellationToken);
            
            // Detect beacon capability
            IsBeaconCapable = await DetectBeaconCapabilityAsync(cancellationToken);
            
            if (IsBeaconCapable)
            {
                _logger.LogInformation("This client is beacon-capable, starting overlay server on port {Port}", _options.OverlayPort);
                await _overlayServer.StartAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("This client is not beacon-capable (behind NAT), will connect to beacons");
            }
            
            _startedAt = DateTimeOffset.UtcNow;
            
            await base.StartAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start DHT rendezvous service");
            throw;
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping DHT rendezvous service");
        
        try
        {
            // Save DHT state for faster restart
            if (_dhtEngine is not null)
            {
                var dhtStatePath = Path.Combine(Program.AppDirectory, "dht_nodes.bin");
                var nodes = await _dhtEngine.SaveNodesAsync();
                if (nodes.Length > 0)
                {
                    await File.WriteAllBytesAsync(dhtStatePath, nodes.ToArray(), cancellationToken);
                    _logger.LogDebug("Saved {Count} bytes of DHT state", nodes.Length);
                }
                
                await _dhtEngine.StopAsync();
                _dhtEngine.Dispose();
                _dhtEngine = null;
            }
            
            await _overlayServer.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during DHT shutdown");
        }
        
        await base.StopAsync(cancellationToken);
    }
    
    Task IDhtRendezvousService.StopAsync(CancellationToken cancellationToken)
    {
        return StopAsync(cancellationToken);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for DHT to bootstrap
        _logger.LogInformation("Waiting for DHT to bootstrap...");
        var bootstrapTimeout = TimeSpan.FromSeconds(30);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        while (_dhtEngine?.State != DhtState.Ready && sw.Elapsed < bootstrapTimeout && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
        
        if (_dhtEngine?.State == DhtState.Ready)
        {
            _logger.LogInformation("DHT bootstrapped successfully with {NodeCount} nodes", _dhtEngine.NodeCount);
        }
        else
        {
            _logger.LogWarning("DHT bootstrap timed out, continuing anyway (state: {State}, nodes: {Nodes})", 
                _dhtEngine?.State, _dhtEngine?.NodeCount ?? 0);
        }
        
        // Main loop
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
    
    private async Task InitializeDhtAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing MonoTorrent DHT engine");
        
        // Create DHT engine
        _dhtEngine = new DhtEngine();
        
        // Subscribe to peer discovery events
        _dhtEngine.PeersFound += OnPeersFound;
        _dhtEngine.StateChanged += OnDhtStateChanged;
        
        // Create UDP listener for DHT on a random port (standard DHT port range)
        // SECURITY: Use cryptographic RNG for port selection
        var dhtPort = _options.DhtPort > 0 ? _options.DhtPort : System.Security.Cryptography.RandomNumberGenerator.GetInt32(6881, 7000);
        _dhtListener = MonoTorrent.Factories.Default.CreateDhtListener(new IPEndPoint(IPAddress.Any, dhtPort));
        
        if (_dhtListener is null)
        {
            throw new InvalidOperationException("Failed to create DHT listener");
        }
        
        _logger.LogDebug("Created DHT listener for port {Port}", dhtPort);
        
        // Attach listener to engine
        await _dhtEngine.SetListenerAsync(_dhtListener);
        
        // Try to load saved DHT state
        var dhtStatePath = Path.Combine(Program.AppDirectory, "dht_nodes.bin");
        ReadOnlyMemory<byte> initialNodes = default;
        
        if (File.Exists(dhtStatePath))
        {
            try
            {
                initialNodes = await File.ReadAllBytesAsync(dhtStatePath, cancellationToken);
                _logger.LogDebug("Loaded {Bytes} bytes of saved DHT state", initialNodes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load saved DHT state");
            }
        }
        
        // Start DHT engine (will bootstrap from saved nodes or public bootstrap nodes)
        if (initialNodes.Length > 0)
        {
            await _dhtEngine.StartAsync(initialNodes);
        }
        else
        {
            await _dhtEngine.StartAsync();
        }
        
        // SECURITY: Verify we actually bound to the expected port after startup
        // Note: LocalEndPoint may not be available until after engine starts
        var actualEndpoint = _dhtListener.LocalEndPoint;
        if (actualEndpoint is not null && actualEndpoint.Port != dhtPort)
        {
            _logger.LogWarning(
                "DHT listener bound to unexpected port! Expected {Expected}, got {Actual}. Possible local attack.",
                dhtPort,
                actualEndpoint.Port);
        }
        
        _logger.LogInformation("DHT engine started on port {Port}, state: {State}", 
            actualEndpoint?.Port ?? dhtPort, _dhtEngine.State);
    }
    
    private void OnPeersFound(object? sender, PeersFoundEventArgs e)
    {
        // Check if this is for one of our rendezvous infohashes
        if (!IsOurRendezvousHash(e.InfoHash))
        {
            return;
        }
        
        _logger.LogInformation("DHT found {Count} peers for rendezvous hash {Hash}", 
            e.Peers.Count, e.InfoHash);
        
        foreach (var peerInfo in e.Peers)
        {
            try
            {
                // PeerInfo contains a ConnectionUri with the peer address
                var uri = peerInfo.ConnectionUri;
                if (uri is null)
                {
                    continue;
                }
                
                // Parse IP and port from URI (format: ipv4://192.168.1.1:port or ipv6://[::1]:port)
                if (!IPAddress.TryParse(uri.Host, out var ip))
                {
                    _logger.LogDebug("Could not parse IP from peer URI: {Uri}", uri);
                    continue;
                }
                
                var endpoint = new IPEndPoint(ip, uri.Port);
                var endpointKey = $"{ip}:{uri.Port}";
                
                // SECURITY: Cap discovered peers to prevent memory exhaustion
                if (_discoveredPeers.Count >= MaxDiscoveredPeers)
                {
                    _logger.LogDebug("Discovered peers at capacity ({Max}), skipping {Endpoint}", MaxDiscoveredPeers, endpoint);
                    continue;
                }
                
                // Don't add ourselves or already-known peers
                if (_discoveredPeers.TryAdd(endpointKey, endpoint))
                {
                    Interlocked.Increment(ref _totalPeersDiscovered);
                    _logger.LogDebug("Discovered new mesh peer: {Endpoint}", endpoint);
                    
                    // Try to connect asynchronously
                    _ = TryConnectToPeerAsync(endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse peer info: {Peer}", peerInfo);
            }
        }
    }
    
    private async Task TryConnectToPeerAsync(IPEndPoint endpoint)
    {
        try
        {
            Interlocked.Increment(ref _totalConnectionsAttempted);
            var connected = await _overlayConnector.ConnectToCandidatesAsync(new[] { endpoint });
            if (connected > 0)
            {
                Interlocked.Increment(ref _totalConnectionsSucceeded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to connect to discovered peer {Endpoint}", endpoint);
        }
    }
    
    private void OnDhtStateChanged(object? sender, EventArgs e)
    {
        _logger.LogInformation("DHT state changed to: {State}, nodes: {NodeCount}", 
            _dhtEngine?.State, _dhtEngine?.NodeCount ?? 0);
    }
    
    private bool IsOurRendezvousHash(InfoHash hash)
    {
        return hash.Equals(MainInfohash) || 
               hash.Equals(BackupInfohash1) || 
               hash.Equals(BackupInfohash2);
    }
    
    public async Task<int> DiscoverPeersAsync(CancellationToken cancellationToken = default)
    {
        if (_dhtEngine is null || _dhtEngine.State != DhtState.Ready)
        {
            _logger.LogWarning("Cannot discover peers - DHT not ready (state: {State})", _dhtEngine?.State);
            return 0;
        }
        
        _logger.LogDebug("Running DHT peer discovery (get_peers)");
        _lastDiscoveryTime = DateTimeOffset.UtcNow;
        
        var beforeCount = _totalPeersDiscovered;
        
        // Query all rendezvous infohashes
        // GetPeers is non-blocking; results come via PeersFound event
        _dhtEngine.GetPeers(MainInfohash);
        _dhtEngine.GetPeers(BackupInfohash1);
        _dhtEngine.GetPeers(BackupInfohash2);
        
        // Wait a bit for responses
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        
        var newPeers = (int)(_totalPeersDiscovered - beforeCount);
        _logger.LogInformation("DHT discovery found {Count} new peers", newPeers);
        
        return newPeers;
    }
    
    public Task AnnounceAsync(CancellationToken cancellationToken = default)
    {
        if (!IsBeaconCapable)
        {
            _logger.LogWarning("Cannot announce - not beacon capable");
            return Task.CompletedTask;
        }
        
        if (_dhtEngine is null || _dhtEngine.State != DhtState.Ready)
        {
            _logger.LogWarning("Cannot announce - DHT not ready (state: {State})", _dhtEngine?.State);
            return Task.CompletedTask;
        }
        
        _logger.LogDebug("Announcing to DHT (announce_peer) with overlay port {Port}", _options.OverlayPort);
        _lastAnnounceTime = DateTimeOffset.UtcNow;
        
        // Announce on all rendezvous infohashes
        _dhtEngine.Announce(MainInfohash, _options.OverlayPort);
        _dhtEngine.Announce(BackupInfohash1, _options.OverlayPort);
        _dhtEngine.Announce(BackupInfohash2, _options.OverlayPort);
        
        _logger.LogInformation("Announced overlay port {Port} to DHT on {Count} infohashes", 
            _options.OverlayPort, 3);
        
        return Task.CompletedTask;
    }
    
    public IReadOnlyList<IPEndPoint> GetDiscoveredPeers()
    {
        return _discoveredPeers.Values.ToList();
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
            DhtState = _dhtEngine?.State.ToString() ?? "NotStarted",
            DiscoveredPeerCount = DiscoveredPeerCount,
            ActiveMeshConnections = ActiveMeshConnections,
            VerifiedBeaconCount = (int)_totalConnectionsSucceeded,
            TotalPeersDiscovered = _totalPeersDiscovered,
            TotalConnectionsAttempted = _totalConnectionsAttempted,
            TotalConnectionsSucceeded = _totalConnectionsSucceeded,
            LastAnnounceTime = _lastAnnounceTime,
            LastDiscoveryTime = _lastDiscoveryTime,
            StartedAt = _startedAt,
            RendezvousInfohashes = new[]
            {
                MainInfohash.ToHex(),
                BackupInfohash1.ToHex(),
                BackupInfohash2.ToHex(),
            },
        };
    }
    
    /// <summary>
    /// Add a peer endpoint manually (for testing or configuration).
    /// </summary>
    public void AddPeerEndpoint(IPEndPoint endpoint)
    {
        var key = $"{endpoint.Address}:{endpoint.Port}";
        if (_discoveredPeers.TryAdd(key, endpoint))
        {
            _logger.LogDebug("Added manual peer endpoint {Endpoint}", endpoint);
        }
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
            
            _logger.LogDebug("Successfully bound to overlay port {Port}", _options.OverlayPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not bind to overlay port {Port}", _options.OverlayPort);
            return false;
        }
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
    /// UDP port for DHT. If 0, a random port in range 6881-6999 is used.
    /// </summary>
    public int DhtPort { get; set; } = 0;
    
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
    /// Enable UPnP/NAT-PMP port mapping.
    /// WARNING: UPnP has known security issues. Only enable if you understand the risks
    /// and need automatic port forwarding. Most users should manually configure port
    /// forwarding or use a VPN instead.
    /// Default: false (opt-in only)
    /// </summary>
    public bool EnableUpnp { get; set; } = false;
    
    /// <summary>
    /// Enable STUN for public IP detection.
    /// Uses external STUN servers to detect your public IP address.
    /// This is generally safe but does contact external servers.
    /// Default: true
    /// </summary>
    public bool EnableStun { get; set; } = true;
    
    /// <summary>
    /// Enable Soulseek username verification for overlay peers.
    /// Verifies that peers control the Soulseek account they claim by checking
    /// for a challenge token in their UserInfo description.
    /// Default: false (adds latency to handshake)
    /// </summary>
    public bool EnableUsernameVerification { get; set; } = false;
    
    /// <summary>
    /// Enable peer diversity checks (anti-eclipse attack protection).
    /// Ensures mesh neighbors come from diverse IP ranges and ASNs.
    /// Default: true
    /// </summary>
    public bool EnablePeerDiversity { get; set; } = true;
}
