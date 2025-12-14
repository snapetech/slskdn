// <copyright file="MeshCircuitBuilder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;
using slskd.Common.Security;

namespace slskd.Mesh;

/// <summary>
/// Builds and manages multi-hop onion routing circuits through mesh peers.
/// Provides anonymous routing by forwarding traffic through multiple peers.
/// </summary>
public class MeshCircuitBuilder : IDisposable
{
    private readonly MeshOptions _meshOptions;
    private readonly ILogger<MeshCircuitBuilder> _logger;
    private readonly IMeshPeerManager _peerManager;
    private readonly IAnonymityTransportSelector _transportSelector;

    private readonly Dictionary<string, MeshCircuit> _activeCircuits = new();
    private readonly object _circuitsLock = new();

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeshCircuitBuilder"/> class.
    /// </summary>
    /// <param name="meshOptions">The mesh configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="peerManager">The mesh peer manager for peer discovery.</param>
    /// <param name="transportSelector">The transport selector for circuit connections.</param>
    public MeshCircuitBuilder(
        MeshOptions meshOptions,
        ILogger<MeshCircuitBuilder> logger,
        IMeshPeerManager peerManager,
        IAnonymityTransportSelector transportSelector)
    {
        _meshOptions = meshOptions ?? throw new ArgumentNullException(nameof(meshOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
        _transportSelector = transportSelector ?? throw new ArgumentNullException(nameof(transportSelector));
    }

    /// <summary>
    /// Builds a new onion routing circuit to the target peer.
    /// </summary>
    /// <param name="targetPeerId">The target peer ID.</param>
    /// <param name="circuitLength">The number of hops in the circuit (default: 3).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The built circuit.</returns>
    public async Task<MeshCircuit> BuildCircuitAsync(
        string targetPeerId,
        int circuitLength = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetPeerId))
        {
            throw new ArgumentException("Target peer ID cannot be null or empty", nameof(targetPeerId));
        }

        if (circuitLength < 2 || circuitLength > 6)
        {
            throw new ArgumentException("Circuit length must be between 2 and 6 hops", nameof(circuitLength));
        }

        _logger.LogInformation("Building {Length}-hop circuit to peer {TargetPeerId}", circuitLength, targetPeerId);

        try
        {
            // Select intermediate peers for the circuit
            var circuitPeers = await SelectCircuitPeersAsync(targetPeerId, circuitLength, cancellationToken);
            if (circuitPeers.Count < circuitLength)
            {
                throw new InvalidOperationException(
                    $"Could not find enough peers for {circuitLength}-hop circuit. Found {circuitPeers.Count} peers.");
            }

            // Build the circuit by establishing connections through each hop
            var circuit = await EstablishCircuitAsync(circuitPeers, cancellationToken);

            lock (_circuitsLock)
            {
                _activeCircuits[circuit.CircuitId] = circuit;
            }

            _logger.LogInformation("Successfully built circuit {CircuitId} to {TargetPeerId} with {HopCount} hops",
                circuit.CircuitId, targetPeerId, circuit.Hops.Count);

            return circuit;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build circuit to peer {TargetPeerId}", targetPeerId);
            throw;
        }
    }

    /// <summary>
    /// Gets an existing circuit by ID.
    /// </summary>
    /// <param name="circuitId">The circuit ID.</param>
    /// <returns>The circuit, or null if not found.</returns>
    public MeshCircuit? GetCircuit(string circuitId)
    {
        lock (_circuitsLock)
        {
            _activeCircuits.TryGetValue(circuitId, out var circuit);
            return circuit;
        }
    }

    /// <summary>
    /// Destroys a circuit and cleans up resources.
    /// </summary>
    /// <param name="circuitId">The circuit ID to destroy.</param>
    public void DestroyCircuit(string circuitId)
    {
        MeshCircuit? circuit;
        lock (_circuitsLock)
        {
            if (_activeCircuits.TryGetValue(circuitId, out circuit))
            {
                _activeCircuits.Remove(circuitId);
            }
        }

        if (circuit != null)
        {
            circuit.Dispose();
            _logger.LogInformation("Destroyed circuit {CircuitId}", circuitId);
        }
    }

    /// <summary>
    /// Gets all active circuits.
    /// </summary>
    /// <returns>List of active circuits.</returns>
    public List<MeshCircuit> GetActiveCircuits()
    {
        lock (_circuitsLock)
        {
            return _activeCircuits.Values.ToList();
        }
    }

    /// <summary>
    /// Gets circuit statistics.
    /// </summary>
    /// <returns>Circuit statistics.</returns>
    public CircuitStatistics GetStatistics()
    {
        lock (_circuitsLock)
        {
            return new CircuitStatistics
            {
                ActiveCircuits = _activeCircuits.Count,
                TotalCircuitsBuilt = _activeCircuits.Count, // TODO: Add persistent counter
                AverageCircuitLength = _activeCircuits.Values.Any()
                    ? _activeCircuits.Values.Average(c => c.Hops.Count)
                    : 0,
                CircuitLengths = _activeCircuits.Values.GroupBy(c => c.Hops.Count)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }
    }

    /// <summary>
    /// Performs circuit maintenance (cleanup expired circuits, etc.).
    /// </summary>
    public void PerformMaintenance()
    {
        lock (_circuitsLock)
        {
            var expiredCircuits = _activeCircuits
                .Where(kvp => kvp.Value.IsExpired())
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var circuitId in expiredCircuits)
            {
                DestroyCircuit(circuitId);
            }

            if (expiredCircuits.Count > 0)
            {
                _logger.LogInformation("Cleaned up {Count} expired circuits", expiredCircuits.Count);
            }
        }
    }

    private async Task<List<MeshPeer>> SelectCircuitPeersAsync(
        string targetPeerId,
        int circuitLength,
        CancellationToken cancellationToken)
    {
        // Get all available peers except ourselves and the target
        var availablePeers = await _peerManager.GetAvailablePeersAsync(cancellationToken);
        var filteredPeers = availablePeers
            .Where(p => p.PeerId != _meshOptions.SelfPeerId && p.PeerId != targetPeerId)
            .ToList();

        if (filteredPeers.Count < circuitLength - 1) // Need length-1 intermediate peers
        {
            throw new InvalidOperationException(
                $"Not enough intermediate peers available. Need {circuitLength - 1}, found {filteredPeers.Count}");
        }

        // Select peers based on quality metrics (trust, latency, bandwidth)
        var selectedPeers = filteredPeers
            .OrderByDescending(p => p.GetQualityScore()) // Higher score = better peer
            .Take(circuitLength - 1) // Intermediate peers
            .ToList();

        // Add the target peer as the final hop
        var targetPeer = availablePeers.FirstOrDefault(p => p.PeerId == targetPeerId);
        if (targetPeer == null)
        {
            throw new InvalidOperationException($"Target peer {targetPeerId} not found in mesh");
        }

        selectedPeers.Add(targetPeer);

        _logger.LogDebug("Selected circuit peers: {Peers}",
            string.Join(" → ", selectedPeers.Select(p => p.PeerId)));

        return selectedPeers;
    }

    private async Task<MeshCircuit> EstablishCircuitAsync(
        List<MeshPeer> circuitPeers,
        CancellationToken cancellationToken)
    {
        var circuitId = GenerateCircuitId();
        var hops = new List<CircuitHop>();

        // Establish the circuit by building connections through each peer
        for (var i = 0; i < circuitPeers.Count; i++)
        {
            var peer = circuitPeers[i];
            var isEntryNode = i == 0;
            var isExitNode = i == circuitPeers.Count - 1;

            // For each hop, establish a connection to the peer
            // In a real implementation, this would use layered encryption
            // where each hop only knows the next hop, not the final destination
            var hop = new CircuitHop
            {
                HopNumber = i + 1,
                PeerId = peer.PeerId,
                PeerAddress = peer.GetBestAddress(),
                Role = isEntryNode ? CircuitHopRole.Entry :
                      isExitNode ? CircuitHopRole.Exit :
                      CircuitHopRole.Intermediate,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // In the placeholder implementation, we create a connection to each peer
            // In reality, this would establish encrypted tunnels with onion routing
            try
            {
                // Connect to this peer using available transports
                var (transport, stream) = await _transportSelector.SelectAndConnectAsync(
                    hop.PeerAddress.Host,
                    hop.PeerAddress.Port,
                    circuitId, // Use circuit ID as isolation key
                    cancellationToken);

                hop.Transport = transport;
                hop.Stream = stream;
                hop.IsEstablished = true;

                _logger.LogDebug("Established hop {HopNumber} to peer {PeerId} via {Transport}",
                    hop.HopNumber, hop.PeerId, transport.TransportType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to establish hop {HopNumber} to peer {PeerId}", hop.HopNumber, hop.PeerId);
                hop.IsEstablished = false;
                hop.ErrorMessage = ex.Message;
            }

            hops.Add(hop);
        }

        var circuit = new MeshCircuit(circuitId, hops, TimeSpan.FromHours(1)); // 1 hour TTL

        // Validate that the circuit is complete
        if (!circuit.IsComplete())
        {
            throw new InvalidOperationException("Circuit establishment failed - not all hops connected");
        }

        return circuit;
    }

    private string GenerateCircuitId()
    {
        // Generate a unique circuit ID
        return $"circuit_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
    }

    /// <summary>
    /// Disposes resources used by the circuit builder.
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
                lock (_circuitsLock)
                {
                    foreach (var circuit in _activeCircuits.Values)
                    {
                        circuit.Dispose();
                    }
                    _activeCircuits.Clear();
                }
            }

            _disposed = true;
        }
    }
}

/// <summary>
/// Statistics about mesh circuits.
/// </summary>
public class CircuitStatistics
{
    /// <summary>
    /// Gets or sets the number of active circuits.
    /// </summary>
    public int ActiveCircuits { get; set; }

    /// <summary>
    /// Gets or sets the total number of circuits built since startup.
    /// </summary>
    public int TotalCircuitsBuilt { get; set; }

    /// <summary>
    /// Gets or sets the average circuit length.
    /// </summary>
    public double AverageCircuitLength { get; set; }

    /// <summary>
    /// Gets or sets the distribution of circuit lengths.
    /// </summary>
    public Dictionary<int, int> CircuitLengths { get; set; } = new();
}

/// <summary>
/// Role of a circuit hop.
/// </summary>
public enum CircuitHopRole
{
    /// <summary>
    /// Entry node (first hop).
    /// </summary>
    Entry,

    /// <summary>
    /// Intermediate node.
    /// </summary>
    Intermediate,

    /// <summary>
    /// Exit node (final hop).
    /// </summary>
    Exit
}
