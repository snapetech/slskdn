// <copyright file="CircuitMaintenanceService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

/// <summary>
/// Background service for maintaining mesh circuits.
/// Performs cleanup, health checks, and circuit lifecycle management.
/// </summary>
public class CircuitMaintenanceService : BackgroundService
{
    private readonly ILogger<CircuitMaintenanceService> _logger;
    private readonly MeshCircuitBuilder _circuitBuilder;
    private readonly IMeshPeerManager _peerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitMaintenanceService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="circuitBuilder">The circuit builder.</param>
    /// <param name="peerManager">The peer manager.</param>
    public CircuitMaintenanceService(
        ILogger<CircuitMaintenanceService> logger,
        MeshCircuitBuilder circuitBuilder,
        IMeshPeerManager peerManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _circuitBuilder = circuitBuilder ?? throw new ArgumentNullException(nameof(circuitBuilder));
        _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
    }

    /// <summary>
    /// Executes the background maintenance loop.
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Circuit maintenance service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformMaintenanceAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Run every 5 minutes
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during circuit maintenance");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Retry after 1 minute on error
            }
        }

        _logger.LogInformation("Circuit maintenance service stopped");
    }

    private async Task PerformMaintenanceAsync(CancellationToken cancellationToken)
    {
        // Clean up expired circuits
        _circuitBuilder.PerformMaintenance();

        // Clean up old peers
        _peerManager.GetType().GetMethod("PerformMaintenance")?.Invoke(_peerManager, null);

        // Log statistics
        var circuitStats = _circuitBuilder.GetStatistics();
        var peerStats = _peerManager.GetStatistics();

        _logger.LogInformation(
            "Circuit maintenance: {ActiveCircuits} circuits, {TotalPeers} total peers, {ActivePeers} active, {OnionPeers} onion-capable",
            circuitStats.ActiveCircuits,
            peerStats.TotalPeers,
            peerStats.ActivePeers,
            peerStats.OnionRoutingPeers);

        // Test a few random circuits if we have enough peers
        if (circuitStats.ActiveCircuits == 0 && peerStats.OnionRoutingPeers >= 3)
        {
            await TestCircuitBuildingAsync(cancellationToken);
        }
    }

    private async Task TestCircuitBuildingAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Get some test peers
            var circuitPeers = await _peerManager.GetCircuitPeersAsync(0.2, cancellationToken);
            if (circuitPeers.Count >= 3)
            {
                var testPeer = circuitPeers.First();
                _logger.LogInformation("Testing circuit building to peer {PeerId}", testPeer.PeerId);

                // Build a test circuit
                var circuit = await _circuitBuilder.BuildCircuitAsync(testPeer.PeerId, 3, cancellationToken);

                // Clean it up immediately (just testing)
                _circuitBuilder.DestroyCircuit(circuit.CircuitId);

                _logger.LogInformation("Circuit building test successful");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Circuit building test failed");
        }
    }
}

