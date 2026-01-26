// <copyright file="AnonymityTransportSelector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.Mesh;
using slskd.Mesh.Transport;
using slskd.Mesh.Overlay;

namespace slskd.Common.Security;

/// <summary>
/// Status summary of the transport selector.
/// </summary>
public sealed class TransportSelectorStatus
{
    /// <summary>
    /// Gets or sets the currently selected anonymity mode.
    /// </summary>
    public AnonymityMode SelectedMode { get; set; }

    /// <summary>
    /// Gets or sets the total number of configured transports.
    /// </summary>
    public int TotalTransports { get; set; }

    /// <summary>
    /// Gets or sets the number of available transports.
    /// </summary>
    public int AvailableTransports { get; set; }

    /// <summary>
    /// Gets or sets the list of available transport types.
    /// </summary>
    public List<AnonymityTransportType> AvailableTransportTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the last connectivity test.
    /// </summary>
    public DateTimeOffset LastConnectivityTest { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the primary transport is available.
    /// </summary>
    public bool PrimaryTransportAvailable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether fallback transports are available.
    /// </summary>
    public bool FallbackAvailable { get; set; }
}

/// <summary>
/// Service for intelligent selection and management of anonymity transports.
/// Handles priority ordering, automatic failover, and transport lifecycle.
/// </summary>
public class AnonymityTransportSelector : IAnonymityTransportSelector, IDisposable
{
    private readonly AdversarialOptions _adversarialOptions;
    private readonly IOverlayDataPlane? _overlayDataPlane;
    private readonly ILogger<AnonymityTransportSelector> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TransportPolicyManager _policyManager;

    private readonly Dictionary<AnonymityTransportType, IAnonymityTransport> _transports = new();
    private readonly object _transportsLock = new();

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnonymityTransportSelector"/> class.
    /// </summary>
    /// <param name="adversarialOptions">The adversarial options containing both anonymity and obfuscated transport settings.</param>
    /// <param name="policyManager">The transport policy manager for per-peer/per-pod policies.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="overlayDataPlane">Optional data-plane overlay; required for RelayOnly mode.</param>
    public AnonymityTransportSelector(
        AdversarialOptions adversarialOptions,
        TransportPolicyManager policyManager,
        ILogger<AnonymityTransportSelector> logger,
        ILoggerFactory loggerFactory,
        IOverlayDataPlane? overlayDataPlane = null)
    {
        _adversarialOptions = adversarialOptions ?? throw new ArgumentNullException(nameof(adversarialOptions));
        _policyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _overlayDataPlane = overlayDataPlane;

        InitializeTransports();
    }

    /// <summary>
    /// Gets the currently selected anonymity mode.
    /// </summary>
    public AnonymityMode SelectedMode => _adversarialOptions.AnonymityLayer.Mode;

    /// <summary>
    /// Selects the best available transport for a connection with policy consideration.
    /// </summary>
    /// <param name="peerId">The target peer ID for policy lookup.</param>
    /// <param name="podId">The pod ID for policy lookup (optional).</param>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected transport and connection stream.</returns>
    public async Task<(IAnonymityTransport Transport, Stream Stream)> SelectAndConnectAsync(
        string peerId,
        string? podId,
        string host,
        int port,
        string? isolationKey = null,
        CancellationToken cancellationToken = default)
    {
        var transport = await SelectTransportAsync(peerId, podId, cancellationToken);
        if (transport == null)
        {
            throw new InvalidOperationException("No anonymity transport is available for the specified peer/pod");
        }

        try
        {
            Stream stream;
            if (isolationKey != null && (transport.TransportType == AnonymityTransportType.Tor ||
                                        transport.TransportType == AnonymityTransportType.WebSocket ||
                                        transport.TransportType == AnonymityTransportType.HttpTunnel))
            {
                // Use stream isolation for transports that support it
                stream = await transport.ConnectAsync(host, port, isolationKey, cancellationToken);
            }
            else
            {
                stream = await transport.ConnectAsync(host, port, cancellationToken);
            }

            return (transport, stream);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect using {TransportType}, attempting failover", transport.TransportType);

            // Try failover to next available transport with policy consideration
            var fallbackTransport = await SelectTransportAsync(peerId, podId, cancellationToken, transport.TransportType);
            if (fallbackTransport != null)
            {
                try
                {
                    var fallbackStream = isolationKey != null && (fallbackTransport.TransportType == AnonymityTransportType.Tor ||
                                                                 fallbackTransport.TransportType == AnonymityTransportType.WebSocket ||
                                                                 fallbackTransport.TransportType == AnonymityTransportType.HttpTunnel)
                        ? await fallbackTransport.ConnectAsync(host, port, isolationKey, cancellationToken)
                        : await fallbackTransport.ConnectAsync(host, port, cancellationToken);

                    _logger.LogInformation("Failover successful: {FailedTransport} → {SuccessTransport}",
                        transport.TransportType, fallbackTransport.TransportType);
                    return (fallbackTransport, fallbackStream);
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Failover also failed for {TransportType}", fallbackTransport.TransportType);
                }
            }

            throw new AggregateException("All anonymity transports failed", ex);
        }
    }

    /// <summary>
    /// Selects the best available transport for a connection (legacy method without policy).
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected transport and connection stream.</returns>
    [Obsolete("Use SelectAndConnectAsync(peerId, podId, host, port, isolationKey, cancellationToken) for policy-aware selection")]
    public async Task<(IAnonymityTransport Transport, Stream Stream)> SelectAndConnectAsync(
        string host,
        int port,
        string? isolationKey = null,
        CancellationToken cancellationToken = default)
    {
        // Use default peer/pod context (no specific policy)
        return await SelectAndConnectAsync(peerId: null, podId: null, host, port, isolationKey, cancellationToken);
    }

    /// <summary>
    /// Gets the status of all available transports.
    /// </summary>
    /// <returns>Dictionary of transport statuses.</returns>
    public Dictionary<AnonymityTransportType, AnonymityTransportStatus> GetAllStatuses()
    {
        lock (_transportsLock)
        {
            return _transports.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetStatus());
        }
    }

    /// <summary>
    /// Gets a summary status of the transport selector.
    /// </summary>
    /// <returns>Overall transport selector status.</returns>
    public TransportSelectorStatus GetSelectorStatus()
    {
        var allStatuses = GetAllStatuses();
        var availableTransports = allStatuses.Where(kvp => kvp.Value.IsAvailable).ToList();

        return new TransportSelectorStatus
        {
            SelectedMode = SelectedMode,
            TotalTransports = allStatuses.Count,
            AvailableTransports = availableTransports.Count,
            AvailableTransportTypes = availableTransports.Select(kvp => kvp.Key).ToList(),
            LastConnectivityTest = DateTimeOffset.UtcNow,
            PrimaryTransportAvailable = availableTransports.Any(),
            FallbackAvailable = availableTransports.Count > 1
        };
    }

    /// <summary>
    /// Gets a specific transport by type.
    /// </summary>
    /// <param name="transportType">The type of transport to get.</param>
    /// <returns>The transport instance, or null if not available.</returns>
    public IAnonymityTransport? GetTransport(AnonymityTransportType transportType)
    {
        lock (_transportsLock)
        {
            return _transports.TryGetValue(transportType, out var transport) ? transport : null;
        }
    }

    /// <summary>
    /// Gets the Tor transport if available.
    /// </summary>
    /// <returns>The Tor transport, or null if not configured.</returns>
    public TorSocksTransport? GetTorTransport()
    {
        return GetTransport(AnonymityTransportType.Tor) as TorSocksTransport;
    }

    /// <summary>
    /// Gets the status of all available transports.
    /// </summary>
    public Dictionary<AnonymityTransportType, AnonymityTransportStatus> GetTransportStatuses()
    {
        var statuses = new Dictionary<AnonymityTransportType, AnonymityTransportStatus>();

        lock (_transportsLock)
        {
            foreach (var kvp in _transports)
            {
                statuses[kvp.Key] = kvp.Value.GetStatus();
            }
        }

        return statuses;
    }

    /// <summary>
    /// Tests connectivity for all transports.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task that completes when all connectivity tests are done.</returns>
    public async Task TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        var testTasks = new List<Task>();

        lock (_transportsLock)
        {
            foreach (var transport in _transports.Values)
            {
                testTasks.Add(TestTransportConnectivityAsync(transport, cancellationToken));
            }
        }

        await Task.WhenAll(testTasks);
        _logger.LogInformation("Completed connectivity tests for all anonymity transports");
    }

    private void InitializeTransports()
    {
        lock (_transportsLock)
        {
            _transports.Clear();

            // Initialize anonymity transports based on mode
            var anonymityOptions = _adversarialOptions.AnonymityLayer;
            if (anonymityOptions.Mode == AnonymityMode.Tor || anonymityOptions.Mode == AnonymityMode.Direct)
            {
                _transports[AnonymityTransportType.Tor] = new TorSocksTransport(
                    anonymityOptions.Tor, 
                    _loggerFactory.CreateLogger<TorSocksTransport>());
            }

            if (anonymityOptions.Mode == AnonymityMode.I2P)
            {
                _transports[AnonymityTransportType.I2P] = new I2PTransport(
                    anonymityOptions.I2P,
                    _loggerFactory.CreateLogger<I2PTransport>());
            }

            if (anonymityOptions.Mode == AnonymityMode.RelayOnly && _overlayDataPlane != null)
            {
                _transports[AnonymityTransportType.RelayOnly] = new RelayOnlyTransport(
                    anonymityOptions.RelayOnly,
                    _overlayDataPlane,
                    _loggerFactory.CreateLogger<RelayOnlyTransport>());
            }

            // Initialize obfuscated transports (available regardless of anonymity mode)
            var transportOptions = _adversarialOptions.ObfuscatedTransports;
            if (transportOptions.WebSocket.Enabled)
            {
                _transports[AnonymityTransportType.WebSocket] = new WebSocketTransport(
                    transportOptions.WebSocket, 
                    _loggerFactory.CreateLogger<WebSocketTransport>());
            }

            if (transportOptions.HttpTunnel.Enabled)
            {
                _transports[AnonymityTransportType.HttpTunnel] = new HttpTunnelTransport(
                    transportOptions.HttpTunnel, 
                    _loggerFactory.CreateLogger<HttpTunnelTransport>());
            }

            if (transportOptions.Obfs4.Enabled)
            {
                _transports[AnonymityTransportType.Obfs4] = new Obfs4Transport(
                    transportOptions.Obfs4, 
                    _loggerFactory.CreateLogger<Obfs4Transport>());
            }

            if (transportOptions.Meek.Enabled)
            {
                _transports[AnonymityTransportType.Meek] = new MeekTransport(
                    transportOptions.Meek, 
                    _loggerFactory.CreateLogger<MeekTransport>());
            }

            _logger.LogInformation("Initialized {Count} transports: {TransportTypes}",
                _transports.Count, string.Join(", ", _transports.Keys));
        }
    }

    /// <summary>
    /// Selects the best available transport considering per-peer/per-pod policies.
    /// </summary>
    /// <param name="peerId">The peer ID for policy lookup (optional).</param>
    /// <param name="podId">The pod ID for policy lookup (optional).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="excludeType">Transport type to exclude from selection (optional).</param>
    /// <returns>The selected transport, or null if none available.</returns>
    private async Task<IAnonymityTransport?> SelectTransportAsync(
        string? peerId,
        string? podId,
        CancellationToken cancellationToken,
        AnonymityTransportType? excludeType = null)
    {
        // Get applicable transport policy
        TransportPolicy? policy = null;
        if (!string.IsNullOrEmpty(peerId))
        {
            policy = _policyManager.GetApplicablePolicy(peerId, podId);
        }

        // Get priority-ordered list of transport types
        var priorityOrder = GetTransportPriorityOrder(policy);

        foreach (var transportType in priorityOrder)
        {
            if (excludeType.HasValue && transportType == excludeType.Value)
                continue;

            IAnonymityTransport? transport;
            lock (_transportsLock)
            {
                _transports.TryGetValue(transportType, out transport);
            }

            if (transport != null)
            {
                // Check if transport is allowed by policy
                if (policy != null && !IsTransportAllowedByPolicy(transportType, policy))
                {
                    _logger.LogDebug("Transport {TransportType} not allowed by policy for peer {PeerId}, pod {PodId}",
                        transportType, peerId ?? "unknown", podId ?? "none");
                    continue;
                }

                var isAvailable = await transport.IsAvailableAsync(cancellationToken);
                if (isAvailable)
                {
                    _logger.LogDebug("Selected anonymity transport {TransportType} for peer {PeerId}, pod {PodId}",
                        transportType, peerId ?? "unknown", podId ?? "none");
                    return transport;
                }
                else
                {
                    _logger.LogDebug("Transport {TransportType} is not available, trying next", transportType);
                }
            }
        }

        _logger.LogWarning("No available anonymity transports found for peer {PeerId}, pod {PodId}",
            peerId ?? "unknown", podId ?? "none");
        return null;
    }

    /// <summary>
    /// Legacy method for backward compatibility - selects transport without policy consideration.
    /// </summary>
    private async Task<IAnonymityTransport?> SelectTransportAsync(
        CancellationToken cancellationToken,
        AnonymityTransportType? excludeType = null)
    {
        return await SelectTransportAsync(peerId: null, podId: null, cancellationToken, excludeType);
    }

    private List<AnonymityTransportType> GetTransportPriorityOrder(TransportPolicy? policy = null)
    {
        var priorityOrder = new List<AnonymityTransportType>();

        // If policy specifies preference order, use it
        if (policy?.TransportPreferenceOrder != null && policy.TransportPreferenceOrder.Any())
        {
            foreach (var transportType in policy.TransportPreferenceOrder)
            {
                var anonymityType = MapTransportTypeToAnonymityType(transportType);
                if (anonymityType.HasValue && !priorityOrder.Contains(anonymityType.Value))
                {
                    priorityOrder.Add(anonymityType.Value);
                }
            }
        }
        else
        {
            // Use default priority order based on configuration
            // Add obfuscated transports first (if enabled and available)
            var transportOptions = _adversarialOptions.ObfuscatedTransports;
            if (transportOptions.Enabled)
            {
                var primaryTransport = transportOptions.Mode switch
                {
                    ObfuscatedTransportMode.WebSocket => AnonymityTransportType.WebSocket,
                    ObfuscatedTransportMode.HttpTunnel => AnonymityTransportType.HttpTunnel,
                    ObfuscatedTransportMode.Obfs4 => AnonymityTransportType.Obfs4,
                    ObfuscatedTransportMode.Meek => AnonymityTransportType.Meek,
                    _ => (AnonymityTransportType?)null
                };

                if (primaryTransport.HasValue)
                {
                    priorityOrder.Add(primaryTransport.Value);
                }
            }

            // Add anonymity transports based on mode and policy preferences
            var anonymityMode = _adversarialOptions.AnonymityLayer.Mode;

            // If policy prefers private transports, prioritize them
            if (policy?.PreferPrivateTransports == true)
            {
                var privateTransports = new List<AnonymityTransportType>();
                switch (anonymityMode)
                {
                    case AnonymityMode.Tor:
                        privateTransports.Add(AnonymityTransportType.Tor);
                        break;
                    case AnonymityMode.I2P:
                        privateTransports.Add(AnonymityTransportType.I2P);
                        privateTransports.Add(AnonymityTransportType.Tor);
                        break;
                    case AnonymityMode.RelayOnly:
                        privateTransports.Add(AnonymityTransportType.RelayOnly);
                        break;
                }

                // Insert private transports at the beginning
                priorityOrder.InsertRange(0, privateTransports.Where(t => !priorityOrder.Contains(t)));
            }
            else
            {
                // Default ordering
                var anonymityTransports = anonymityMode switch
                {
                    AnonymityMode.Direct => new[] { AnonymityTransportType.Tor },
                    AnonymityMode.Tor => new[] { AnonymityTransportType.Tor },
                    AnonymityMode.I2P => new[] { AnonymityTransportType.I2P, AnonymityTransportType.Tor },
                    AnonymityMode.RelayOnly => new[] { AnonymityTransportType.RelayOnly },
                    _ => new[] { AnonymityTransportType.Tor }
                };

                priorityOrder.AddRange(anonymityTransports.Where(t => !priorityOrder.Contains(t)));
            }
        }

        // Ensure Direct is always last as fallback (unless policy disables clearnet)
        if (!priorityOrder.Contains(AnonymityTransportType.Direct) && policy?.DisableClearnet != true)
        {
            priorityOrder.Add(AnonymityTransportType.Direct);
        }

        // Remove duplicates
        priorityOrder = priorityOrder.Distinct().ToList();

        _logger.LogDebug("Transport priority order: {Order}", string.Join(" → ", priorityOrder));
        return priorityOrder;
    }

    private bool IsTransportAllowedByPolicy(AnonymityTransportType transportType, TransportPolicy policy)
    {
        // Convert anonymity transport type to mesh transport type for policy checking
        var meshTransportType = MapAnonymityTypeToTransportType(transportType);
        if (!meshTransportType.HasValue)
        {
            return false;
        }

        // Check if transport is allowed by policy
        return policy.IsTransportAllowed(meshTransportType.Value, _adversarialOptions.MeshTransportOptions);
    }

    private static AnonymityTransportType? MapTransportTypeToAnonymityType(TransportType transportType)
    {
        return transportType switch
        {
            TransportType.DirectQuic => AnonymityTransportType.Direct,
            TransportType.TorOnionQuic => AnonymityTransportType.Tor,
            TransportType.I2PQuic => AnonymityTransportType.I2P,
            _ => null
        };
    }

    private static TransportType? MapAnonymityTypeToTransportType(AnonymityTransportType anonymityType)
    {
        return anonymityType switch
        {
            AnonymityTransportType.Direct => TransportType.DirectQuic,
            AnonymityTransportType.Tor => TransportType.TorOnionQuic,
            AnonymityTransportType.I2P => TransportType.I2PQuic,
            _ => null
        };
    }

    private async Task TestTransportConnectivityAsync(IAnonymityTransport transport, CancellationToken cancellationToken)
    {
        try
        {
            var isAvailable = await transport.IsAvailableAsync(cancellationToken);
            var status = transport.GetStatus();

            _logger.LogInformation("Transport {TransportType} connectivity test: {Status} ({Error})",
                transport.TransportType,
                isAvailable ? "Available" : "Unavailable",
                status.LastError ?? "No error");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connectivity test failed for transport {TransportType}", transport.TransportType);
        }
    }

    /// <summary>
    /// Disposes resources used by the transport selector.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                lock (_transportsLock)
                {
                    foreach (var transport in _transports.Values)
                    {
                        if (transport is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    _transports.Clear();
                }
            }

            _disposed = true;
        }
    }
}
