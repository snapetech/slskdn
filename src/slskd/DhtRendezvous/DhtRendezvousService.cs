// <copyright file="DhtRendezvousService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Connections.Dht;
using MonoTorrent.Dht;
using slskd.Mesh;

/// <summary>
/// Service for discovering and connecting to mesh peers via BitTorrent DHT rendezvous.
///
/// Uses MonoTorrent's DHT implementation to:
/// - Beacons: announce_peer on rendezvous infohash to advertise overlay port
/// - Seekers: get_peers on rendezvous infohash to discover beacons
/// </summary>
public sealed class DhtRendezvousService : BackgroundService, IDhtRendezvousService
{
    private readonly object _lifecycleSync = new();
    private readonly ILogger<DhtRendezvousService> _logger;
    private readonly IMeshOverlayServer _overlayServer;
    private readonly IMeshOverlayConnector _overlayConnector;
    private readonly MeshNeighborRegistry _registry;
    private readonly IMeshPeerManager _peerManager;
    private readonly DhtRendezvousOptions _options;

    // MonoTorrent DHT components
    private DhtEngine? _dhtEngine;
    private IDhtListener? _dhtListener;

    // SECURITY: Use a bounded collection to prevent memory exhaustion
    private readonly ConcurrentDictionary<string, IPEndPoint> _discoveredPeers = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _peerConnectionAttemptedAt = new();
    private readonly ConcurrentDictionary<string, int> _peerConnectionFailureCounts = new();
    private readonly ConcurrentDictionary<string, byte> _pendingPeerConnections = new();
    private const int MaxDiscoveredPeers = 1000;
    internal const int MaxConcurrentPeerConnectionAttempts = MeshOverlayConnector.MaxConcurrentAttempts;
    internal const int MaxOverlayStartAttempts = 5;
    private static readonly TimeSpan PeerReconnectInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxPeerReconnectInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan OverlayStartRetryDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SummaryLogInterval = TimeSpan.FromMinutes(2);
    private DateTimeOffset? _lastAnnounceTime;
    private DateTimeOffset? _lastDiscoveryTime;
    private DateTimeOffset? _lastSummaryLogTime;
    private DateTimeOffset? _startedAt;
    private long _totalPeersDiscovered;
    private long _totalCandidateEndpointsSeen;
    private long _totalCandidatesAccepted;
    private long _totalCandidatesSkippedDhtPort;
    private long _totalCandidatesSkippedDiscoveredCapacity;
    private long _totalCandidatesDeferredConnectorCapacity;
    private long _totalCandidatesSkippedReconnectBackoff;
    private long _totalConnectionsAttempted;
    private long _totalConnectionsSucceeded;
    private int _loadedSavedNodeTableBytes;
    private CancellationTokenSource? _backgroundInitializationCts;
    private Task? _backgroundInitializationTask;
    private bool _backgroundServiceStarted;

    // Rendezvous infohashes (SHA1 of key strings)
    private static readonly InfoHash MainInfohash = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1")));
    private static readonly InfoHash BackupInfohash1 = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1-backup-1")));
    private static readonly InfoHash BackupInfohash2 = InfoHash.FromMemory(SHA1.HashData(Encoding.UTF8.GetBytes("slskdn-mesh-v1-backup-2")));

    public DhtRendezvousService(
        ILogger<DhtRendezvousService> logger,
        IMeshOverlayServer overlayServer,
        IMeshOverlayConnector overlayConnector,
        MeshNeighborRegistry registry,
        IMeshPeerManager peerManager,
        DhtRendezvousOptions options)
    {
        _logger = logger;
        _overlayServer = overlayServer;
        _overlayConnector = overlayConnector;
        _registry = registry;
        _peerManager = peerManager;
        _options = options;
    }

    public bool IsBeaconCapable { get; private set; }
    public bool IsDhtRunning => _dhtEngine?.State == DhtState.Ready;
    public int DhtNodeCount => _dhtEngine?.NodeCount ?? 0;
    public int DiscoveredPeerCount => _discoveredPeers.Values.Count;
    public int ActiveMeshConnections => _registry.Count;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var shouldStartBackgroundService = false;

        lock (_lifecycleSync)
        {
            if (!_backgroundServiceStarted)
            {
                _backgroundServiceStarted = true;
                shouldStartBackgroundService = true;
            }
        }

        if (shouldStartBackgroundService)
        {
            await base.StartAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!_options.Enabled)
        {
            _logger.LogInformation("DHT rendezvous is disabled");
            return;
        }

        _logger.LogInformation("Starting DHT rendezvous service with MonoTorrent DHT");

        lock (_lifecycleSync)
        {
            if (_dhtEngine is not null)
            {
                return;
            }

            CancelBackgroundInitializationNoLock();

            var backgroundInitializationCts = new CancellationTokenSource();
            _backgroundInitializationCts = backgroundInitializationCts;
            _backgroundInitializationTask = StartBackgroundInitializationAsync(backgroundInitializationCts.Token);
        }
    }

    private async Task StartBackgroundInitializationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(async () =>
            {
                // Initialize MonoTorrent DHT
                await InitializeDhtAsync(cancellationToken).ConfigureAwait(false);

                IsBeaconCapable = await StartOverlayServerIfPossibleAsync(cancellationToken).ConfigureAwait(false);

                _startedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation("DHT rendezvous service initialization complete");
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("DHT rendezvous service initialization cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize DHT rendezvous service in background");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? backgroundInitializationCts;
        Task? backgroundInitializationTask;
        DhtEngine? dhtEngine;
        IDhtListener? dhtListener;
        var shouldStopBackgroundService = false;

        lock (_lifecycleSync)
        {
            shouldStopBackgroundService = _backgroundServiceStarted;
            _backgroundServiceStarted = false;
            backgroundInitializationCts = _backgroundInitializationCts;
            _backgroundInitializationCts = null;
            backgroundInitializationTask = _backgroundInitializationTask;
            _backgroundInitializationTask = null;
            dhtEngine = DetachDhtEngineNoLock();
            dhtListener = DetachDhtListenerNoLock();
        }

        backgroundInitializationCts?.Cancel();

        if (backgroundInitializationTask != null)
        {
            try
            {
                await backgroundInitializationTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        backgroundInitializationCts?.Dispose();

        _logger.LogInformation("Stopping DHT rendezvous service");

        try
        {
            // Save DHT state for faster restart
            if (dhtEngine is not null)
            {
                var dhtStatePath = Path.Combine(Program.AppDirectory, "dht_nodes.bin");
                var nodes = await dhtEngine.SaveNodesAsync();
                if (nodes.Length > 0)
                {
                    await File.WriteAllBytesAsync(dhtStatePath, nodes.ToArray(), cancellationToken);
                    _logger.LogDebug("Saved {Count} bytes of DHT state", nodes.Length);
                }

                await dhtEngine.StopAsync();
                dhtEngine.Dispose();
            }

            dhtListener?.Stop();
            await _overlayServer.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during DHT shutdown");
        }

        if (shouldStopBackgroundService)
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    Task IDhtRendezvousService.StopAsync(CancellationToken cancellationToken)
    {
        return StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

        // Wait for DHT initialization to complete (it's running in background from StartAsync)
        _logger.LogInformation("Waiting for DHT initialization to complete...");
        var initTimeout = TimeSpan.FromSeconds(60);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        while (_dhtEngine == null && sw.Elapsed < initTimeout && !stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }

        if (_dhtEngine == null)
        {
            _logger.LogWarning("DHT initialization did not complete within timeout, continuing anyway");
            return;
        }

        // Wait for DHT to bootstrap. Cold public-DHT starts routinely need longer
        // than warm saved-node starts; LAN-only mode should fail fast because it
        // intentionally skips public routers.
        var bootstrapTimeoutSeconds = GetBootstrapTimeoutSeconds(_options, _loadedSavedNodeTableBytes);
        _logger.LogInformation(
            "Waiting up to {TimeoutSeconds}s for DHT to bootstrap (savedNodeTableBytes={SavedNodeTableBytes}, lanOnly={LanOnly})...",
            bootstrapTimeoutSeconds,
            _loadedSavedNodeTableBytes,
            _options.LanOnly);
        var bootstrapTimeout = TimeSpan.FromSeconds(bootstrapTimeoutSeconds);
        sw.Restart();

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
            _logger.LogWarning(
                "DHT bootstrap did not reach Ready within adaptive {TimeoutSeconds}s on UDP port {Port} (state: {State}, nodes: {Nodes}, savedNodeTableBytes: {SavedNodeTableBytes}, lanOnly: {LanOnly}). " +
                "Peer announce/discovery will stay disabled until bootstrap succeeds. If the DHT still has not reached Ready after this grace period, " +
                "verify that UDP port {Port} is reachable, forwarded, and allowed through the host firewall.",
                (int)bootstrapTimeout.TotalSeconds,
                _options.DhtPort,
                _dhtEngine?.State,
                _dhtEngine?.NodeCount ?? 0,
                _loadedSavedNodeTableBytes,
                _options.LanOnly,
                _options.DhtPort);
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
                LogPeriodicSummaryIfDue();
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

    public override void Dispose()
    {
        CancellationTokenSource? backgroundInitializationCts;
        DhtEngine? dhtEngine;
        IDhtListener? dhtListener;

        lock (_lifecycleSync)
        {
            _backgroundServiceStarted = false;
            backgroundInitializationCts = _backgroundInitializationCts;
            _backgroundInitializationCts = null;
            _backgroundInitializationTask = null;
            dhtEngine = DetachDhtEngineNoLock();
            dhtListener = DetachDhtListenerNoLock();
        }

        backgroundInitializationCts?.Cancel();
        backgroundInitializationCts?.Dispose();
        dhtListener?.Stop();
        dhtEngine?.Dispose();

        base.Dispose();
    }

    private async Task InitializeDhtAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Initializing MonoTorrent DHT engine");

        DhtEngine? dhtEngine = null;
        IDhtListener? dhtListener = null;

        try
        {
            // Create DHT engine
            dhtEngine = new DhtEngine();

            // Subscribe to peer discovery events
            dhtEngine.PeersFound += OnPeersFound;
            dhtEngine.StateChanged += OnDhtStateChanged;

            // Use a stable UDP port so operators can forward or allow-list it explicitly.
            var dhtPort = _options.DhtPort;
            dhtListener = MonoTorrent.Factories.Default.CreateDhtListener(new IPEndPoint(IPAddress.Any, dhtPort));

            if (dhtListener is null)
            {
                throw new InvalidOperationException("Failed to create DHT listener");
            }

            _logger.LogDebug("Created DHT listener for port {Port}", dhtPort);

            // Attach listener to engine
            await dhtEngine.SetListenerAsync(dhtListener);

            // Try to load saved DHT state
            var dhtStatePath = Path.Combine(Program.AppDirectory, "dht_nodes.bin");
            ReadOnlyMemory<byte> initialNodes = default;

            if (File.Exists(dhtStatePath))
            {
                try
                {
                    initialNodes = await File.ReadAllBytesAsync(dhtStatePath, cancellationToken);
                    _loadedSavedNodeTableBytes = initialNodes.Length;
                    _logger.LogDebug("Loaded {Bytes} bytes of saved DHT state", initialNodes.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load saved DHT state");
                }
            }

            // Start DHT engine (will bootstrap from saved nodes or public bootstrap nodes).
            // HARDENING-2026-04-20 H12: LanOnly suppresses all public bootstraps so the DHT
            // engine never contacts router.bittorrent.com / router.utorrent.com / dht.transmissionbt.com
            // and never publishes this node's (ip, port) to the public rendezvous. Saved node
            // tables are also skipped in this mode to avoid re-leaking a previously announced IP.
            var bootstrapRouters = _options.LanOnly
                ? Array.Empty<string>()
                : _options.BootstrapRouters?.Where(router => !string.IsNullOrWhiteSpace(router)).ToArray() ?? Array.Empty<string>();

            if (_options.LanOnly)
            {
                _logger.LogWarning(
                    "[DHT] HARDENING-2026-04-20 H12: LanOnly=true — public DHT bootstrap is DISABLED. " +
                    "This node will not appear on the public BitTorrent DHT and cannot be discovered " +
                    "via DHT rendezvous. Mesh peer discovery is confined to local / already-known peers.");
                await dhtEngine.StartAsync(Array.Empty<string>());
            }
            else if (initialNodes.Length > 0)
            {
                await dhtEngine.StartAsync(initialNodes, bootstrapRouters);
            }
            else
            {
                await dhtEngine.StartAsync(bootstrapRouters);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // SECURITY: Verify we actually bound to the expected port after startup
            // Note: LocalEndPoint may not be available until after engine starts
            var actualEndpoint = dhtListener.LocalEndPoint;
            if (actualEndpoint is not null && actualEndpoint.Port != dhtPort)
            {
                _logger.LogWarning(
                    "DHT listener bound to unexpected port! Expected {Expected}, got {Actual}. Possible local attack.",
                    dhtPort,
                    actualEndpoint.Port);
            }

            _logger.LogInformation("DHT engine started on port {Port}, state: {State}",
                actualEndpoint?.Port ?? dhtPort, dhtEngine.State);

            lock (_lifecycleSync)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                _dhtEngine = dhtEngine;
                _dhtListener = dhtListener;
                dhtEngine = null;
                dhtListener = null;
            }
        }
        catch
        {
            if (dhtEngine is not null)
            {
                dhtEngine.PeersFound -= OnPeersFound;
                dhtEngine.StateChanged -= OnDhtStateChanged;
                dhtEngine.Dispose();
            }

            dhtListener?.Stop();
            throw;
        }
    }

    private void OnPeersFound(object? sender, PeersFoundEventArgs e)
    {
        _logger.LogDebug("[DHT EVENT] OnPeersFound fired - InfoHash: {Hash}, Peer count: {Count}, IsOurs: {IsOurs}",
            e.InfoHash, e.Peers.Count, IsOurRendezvousHash(e.InfoHash));

        // Check if this is for one of our rendezvous infohashes
        if (!IsOurRendezvousHash(e.InfoHash))
        {
            return;
        }

        _logger.LogDebug("DHT found {Count} peers for rendezvous hash {Hash}",
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
                Interlocked.Increment(ref _totalCandidateEndpointsSeen);

                if (IsLikelyDhtContactPort(endpoint.Port, _options.DhtPort, _options.OverlayPort))
                {
                    Interlocked.Increment(ref _totalCandidatesSkippedDhtPort);
                    _logger.LogDebug(
                        "Ignoring DHT rendezvous candidate {Endpoint} because it uses the configured DHT UDP port, not the TCP overlay port {OverlayPort}",
                        OverlayLogSanitizer.Endpoint(endpoint),
                        _options.OverlayPort);
                    continue;
                }

                // SECURITY: Cap discovered peers to prevent memory exhaustion
                if (_discoveredPeers.Count >= MaxDiscoveredPeers)
                {
                    Interlocked.Increment(ref _totalCandidatesSkippedDiscoveredCapacity);
                    _logger.LogDebug("Discovered peers at capacity ({Max}), skipping {Endpoint}", MaxDiscoveredPeers, OverlayLogSanitizer.Endpoint(endpoint));
                    continue;
                }

                Interlocked.Increment(ref _totalCandidatesAccepted);

                // Don't add ourselves or already-known peers
                if (_discoveredPeers.TryAdd(endpointKey, endpoint))
                {
                    Interlocked.Increment(ref _totalPeersDiscovered);
                    _logger.LogDebug("Discovered new mesh peer: {Endpoint}", OverlayLogSanitizer.Endpoint(endpoint));
                }

                PublishDiscoveredPeer(endpointKey, endpoint);
                SchedulePeerConnection(endpointKey, endpoint);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse peer info: {Peer}", peerInfo);
            }
        }
    }

    private void PublishDiscoveredPeer(string peerId, IPEndPoint endpoint)
    {
        _peerManager.UpdatePeerInfo(
            peerId,
            new List<IPEndPoint> { endpoint },
            version: "dht-discovered",
            supportsOnionRouting: false);
    }

    private void SchedulePeerConnection(string peerId, IPEndPoint endpoint)
    {
        var now = DateTimeOffset.UtcNow;
        var peer = _peerManager.GetPeer(peerId);
        _peerConnectionAttemptedAt.TryGetValue(peerId, out var lastAttempt);
        var reconnectInterval = GetPeerReconnectInterval(
            _peerConnectionFailureCounts.TryGetValue(peerId, out var failureCount)
                ? failureCount
                : 0);

        if (_pendingPeerConnections.Count >= MaxConcurrentPeerConnectionAttempts)
        {
            Interlocked.Increment(ref _totalCandidatesDeferredConnectorCapacity);
            _logger.LogDebug(
                "Deferring DHT peer connection to {Endpoint}; {PendingCount}/{MaxPending} rendezvous attempts are already pending",
                OverlayLogSanitizer.Endpoint(endpoint),
                _pendingPeerConnections.Count,
                MaxConcurrentPeerConnectionAttempts);
            return;
        }

        if (!ShouldRetryPeerConnection(
                now,
                _peerConnectionAttemptedAt.ContainsKey(peerId) ? lastAttempt : null,
                _registry.IsConnectedTo(endpoint),
                peer?.SupportsOnionRouting == true,
                _pendingPeerConnections.ContainsKey(peerId),
                reconnectInterval))
        {
            if (_peerConnectionAttemptedAt.ContainsKey(peerId) &&
                lastAttempt != default &&
                now - lastAttempt < reconnectInterval &&
                !_registry.IsConnectedTo(endpoint) &&
                peer?.SupportsOnionRouting != true &&
                !_pendingPeerConnections.ContainsKey(peerId))
            {
                Interlocked.Increment(ref _totalCandidatesSkippedReconnectBackoff);
            }

            return;
        }

        if (!_pendingPeerConnections.TryAdd(peerId, 0))
        {
            return;
        }

        _peerConnectionAttemptedAt[peerId] = now;
        _ = TryConnectToPeerAsync(peerId, endpoint);
    }

    private async Task TryConnectToPeerAsync(string peerId, IPEndPoint endpoint)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            Interlocked.Increment(ref _totalConnectionsAttempted);
            var connected = await _overlayConnector.ConnectToCandidatesAsync(new[] { endpoint });
            if (connected > 0)
            {
                Interlocked.Increment(ref _totalConnectionsSucceeded);
                var latencyMs = Math.Max(1, (int)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
                _peerManager.UpdatePeerInfo(
                    peerId,
                    version: "overlay-verified",
                    supportsOnionRouting: true);
                _peerManager.RecordConnectionSuccess(peerId, latencyMs);
                _peerConnectionFailureCounts.TryRemove(peerId, out _);
                return;
            }

            _peerManager.RecordConnectionFailure(peerId);
            RecordPeerConnectionFailure(peerId);
        }
        catch (Exception ex)
        {
            _peerManager.RecordConnectionFailure(peerId);
            RecordPeerConnectionFailure(peerId);
            _logger.LogDebug(ex, "Failed to connect to discovered peer {Endpoint}", OverlayLogSanitizer.Endpoint(endpoint));
        }
        finally
        {
            _pendingPeerConnections.TryRemove(peerId, out _);
        }
    }

    internal static bool ShouldRetryPeerConnection(
        DateTimeOffset now,
        DateTimeOffset? lastAttempt,
        bool isConnected,
        bool isVerified,
        bool isPending,
        TimeSpan reconnectInterval)
    {
        if (isConnected || isVerified || isPending)
        {
            return false;
        }

        return !lastAttempt.HasValue || now - lastAttempt.Value >= reconnectInterval;
    }

    internal static TimeSpan GetPeerReconnectInterval(int consecutiveFailures)
    {
        if (consecutiveFailures <= 1)
        {
            return PeerReconnectInterval;
        }

        var minutes = consecutiveFailures switch
        {
            2 => 15,
            3 => 30,
            _ => 60,
        };

        var interval = TimeSpan.FromMinutes(minutes);
        return interval <= MaxPeerReconnectInterval ? interval : MaxPeerReconnectInterval;
    }

    internal static int GetBootstrapTimeoutSeconds(DhtRendezvousOptions options, int savedNodeTableBytes)
    {
        if (options.LanOnly)
        {
            return Math.Max(1, options.LanOnlyBootstrapTimeoutSeconds);
        }

        if (savedNodeTableBytes <= 0)
        {
            return Math.Max(1, options.ColdBootstrapTimeoutSeconds);
        }

        return Math.Max(1, options.BootstrapTimeoutSeconds);
    }

    private void RecordPeerConnectionFailure(string peerId)
    {
        _peerConnectionFailureCounts.AddOrUpdate(peerId, 1, (_, count) => Math.Min(count + 1, 4));
    }

    internal static bool IsLikelyDhtContactPort(int candidatePort, int dhtPort, int overlayPort)
    {
        return dhtPort > 0 &&
               candidatePort == dhtPort &&
               candidatePort != overlayPort;
    }

    private void OnDhtStateChanged(object? sender, EventArgs e)
    {
        var dhtEngine = sender as DhtEngine;
        _logger.LogInformation("DHT state changed to: {State}, nodes: {NodeCount}",
            dhtEngine?.State ?? _dhtEngine?.State, dhtEngine?.NodeCount ?? _dhtEngine?.NodeCount ?? 0);
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

        _logger.LogInformation("Running DHT peer discovery (get_peers) - DHT state: {State}, nodes: {NodeCount}",
            _dhtEngine.State, _dhtEngine.NodeCount);
        _lastDiscoveryTime = DateTimeOffset.UtcNow;

        var beforeCount = _totalPeersDiscovered;

        // Query all rendezvous infohashes
        // GetPeers is non-blocking; results come via PeersFound event
        _logger.LogInformation("Querying DHT for rendezvous infohash 1: {Hash}", MainInfohash);
        _dhtEngine.GetPeers(MainInfohash);
        _logger.LogInformation("Querying DHT for rendezvous infohash 2: {Hash}", BackupInfohash1);
        _dhtEngine.GetPeers(BackupInfohash1);
        _logger.LogInformation("Querying DHT for rendezvous infohash 3: {Hash}", BackupInfohash2);
        _dhtEngine.GetPeers(BackupInfohash2);

        // Wait a bit for responses
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        var newPeers = (int)(_totalPeersDiscovered - beforeCount);
        _logger.LogInformation("DHT discovery found {Count} new peers (total: {Total})", newPeers, _totalPeersDiscovered);

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
            IsEnabled = _options.Enabled,
            LanOnly = _options.LanOnly,
            IsBeaconCapable = IsBeaconCapable,
            IsDhtRunning = IsDhtRunning,
            DhtNodeCount = DhtNodeCount,
            DhtState = _dhtEngine?.State.ToString() ?? "NotStarted",
            DiscoveredPeerCount = DiscoveredPeerCount,
            ActiveMeshConnections = ActiveMeshConnections,
            VerifiedBeaconCount = (int)_totalConnectionsSucceeded,
            TotalPeersDiscovered = _totalPeersDiscovered,
            TotalCandidateEndpointsSeen = _totalCandidateEndpointsSeen,
            TotalCandidatesAccepted = _totalCandidatesAccepted,
            TotalCandidatesSkippedDhtPort = _totalCandidatesSkippedDhtPort,
            TotalCandidatesSkippedDiscoveredCapacity = _totalCandidatesSkippedDiscoveredCapacity,
            TotalCandidatesDeferredConnectorCapacity = _totalCandidatesDeferredConnectorCapacity,
            TotalCandidatesSkippedReconnectBackoff = _totalCandidatesSkippedReconnectBackoff,
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
            _logger.LogDebug("Added manual peer endpoint {Endpoint}", OverlayLogSanitizer.Endpoint(endpoint));
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

    private void LogPeriodicSummaryIfDue()
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastSummaryLogTime.HasValue && now - _lastSummaryLogTime.Value < SummaryLogInterval)
        {
            return;
        }

        _lastSummaryLogTime = now;
        var connectorStats = _overlayConnector.GetStats();
        var topFailures = new List<string>();

        AppendFailureReason(topFailures, "timeout", connectorStats.FailureReasons.ConnectTimeouts);
        AppendFailureReason(topFailures, "no-route", connectorStats.FailureReasons.NoRouteFailures);
        AppendFailureReason(topFailures, "conn-refused", connectorStats.FailureReasons.ConnectionRefusedFailures);
        AppendFailureReason(topFailures, "conn-reset", connectorStats.FailureReasons.ConnectionResetFailures);
        AppendFailureReason(topFailures, "tls-eof", connectorStats.FailureReasons.TlsEofFailures);
        AppendFailureReason(topFailures, "tls-handshake", connectorStats.FailureReasons.TlsHandshakeFailures);
        AppendFailureReason(topFailures, "protocol", connectorStats.FailureReasons.ProtocolHandshakeFailures);
        AppendFailureReason(topFailures, "registration", connectorStats.FailureReasons.RegistrationFailures);

        var degradedEndpoints = connectorStats.TopProblemEndpoints
            .Where(endpoint => endpoint.ConsecutiveFailureCount > 0)
            .Take(3)
            .Select(endpoint => $"{endpoint.Endpoint}:{endpoint.LastFailureReason}x{endpoint.ConsecutiveFailureCount}")
            .ToArray();

        _logger.LogInformation(
            "DHT/overlay summary: state={State} nodes={NodeCount} activeMesh={ActiveMesh} discovered={Discovered} seen={Seen} accepted={Accepted} skippedPort={SkippedPort} skippedCapacity={SkippedCapacity} deferredCapacity={DeferredCapacity} retryBackoff={RetryBackoff} attempts={Attempts} successes={Successes} cooldownSkips={CooldownSkips} failureMix=[{FailureMix}] degraded=[{DegradedEndpoints}]",
            _dhtEngine?.State ?? DhtState.NotReady,
            DhtNodeCount,
            ActiveMeshConnections,
            DiscoveredPeerCount,
            _totalCandidateEndpointsSeen,
            _totalCandidatesAccepted,
            _totalCandidatesSkippedDhtPort,
            _totalCandidatesSkippedDiscoveredCapacity,
            _totalCandidatesDeferredConnectorCapacity,
            _totalCandidatesSkippedReconnectBackoff,
            _totalConnectionsAttempted,
            _totalConnectionsSucceeded,
            connectorStats.EndpointCooldownSkips,
            topFailures.Count > 0 ? string.Join(", ", topFailures) : "none",
            degradedEndpoints.Length > 0 ? string.Join(", ", degradedEndpoints) : "none");
    }

    private static void AppendFailureReason(ICollection<string> reasons, string label, long count)
    {
        if (count > 0)
        {
            reasons.Add($"{label}={count}");
        }
    }

    private async Task<bool> StartOverlayServerIfPossibleAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxOverlayStartAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation("Starting overlay server on port {Port}", _options.OverlayPort);
                await _overlayServer.StartAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("This client is beacon-capable on overlay port {Port}", _options.OverlayPort);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse && attempt < MaxOverlayStartAttempts)
            {
                _logger.LogWarning(
                    "Overlay port {Port} is still in use during startup; retrying bind attempt {Attempt}/{MaxAttempts} after {DelayMs}ms",
                    _options.OverlayPort,
                    attempt,
                    MaxOverlayStartAttempts,
                    (int)OverlayStartRetryDelay.TotalMilliseconds);
                await Task.Delay(OverlayStartRetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not start overlay server on port {Port}; this node will connect to beacons but will not announce itself",
                    _options.OverlayPort);
                return false;
            }
        }

        _logger.LogWarning(
            "Could not start overlay server on port {Port} after {Attempts} attempts; this node will connect to beacons but will not announce itself",
            _options.OverlayPort,
            MaxOverlayStartAttempts);
        return false;
    }

    private void CancelBackgroundInitializationNoLock()
    {
        _backgroundInitializationCts?.Cancel();
        _backgroundInitializationCts?.Dispose();
        _backgroundInitializationCts = null;
        _backgroundInitializationTask = null;
    }

    private DhtEngine? DetachDhtEngineNoLock()
    {
        var dhtEngine = _dhtEngine;
        if (dhtEngine is null)
        {
            return null;
        }

        dhtEngine.PeersFound -= OnPeersFound;
        dhtEngine.StateChanged -= OnDhtStateChanged;
        _dhtEngine = null;

        return dhtEngine;
    }

    private IDhtListener? DetachDhtListenerNoLock()
    {
        var dhtListener = _dhtListener;
        _dhtListener = null;
        return dhtListener;
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
    /// UDP port for DHT.
    /// </summary>
    public int DhtPort { get; set; } = 50305;

    /// <summary>
    /// Bootstrap routers used to seed the public BitTorrent DHT when no saved node table is available.
    /// </summary>
    public string[] BootstrapRouters { get; set; } =
    {
        "router.bittorrent.com",
        "router.utorrent.com",
        "dht.transmissionbt.com",
    };

    /// <summary>
    /// HARDENING-2026-04-20 H12: disable all contact with the public BitTorrent DHT. Skips the
    /// bootstrap router list on engine start, so this node will not publish (ip, port) to any
    /// public rendezvous server — peer discovery is confined to whatever local / private
    /// transports (LAN multicast, locally-known peers, etc.) are still configured.
    /// Default <c>false</c>; operators who don't want their residential IP enumerable via the
    /// public DHT should flip this to <c>true</c>.
    /// </summary>
    public bool LanOnly { get; set; } = false;

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
    /// Warm bootstrap grace period in seconds when a saved DHT node table is present.
    /// </summary>
    public int BootstrapTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Cold bootstrap grace period in seconds when no saved DHT node table is available.
    /// </summary>
    public int ColdBootstrapTimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// LAN-only bootstrap grace period in seconds. This is shorter because public bootstrap routers are intentionally disabled.
    /// </summary>
    public int LanOnlyBootstrapTimeoutSeconds { get; set; } = 30;

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
