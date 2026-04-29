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
    private readonly IMeshCircuitBuilder _circuitBuilder;
    private readonly IMeshPeerManager _peerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitMaintenanceService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="circuitBuilder">The circuit builder.</param>
    /// <param name="peerManager">The peer manager.</param>
    public CircuitMaintenanceService(
        ILogger<CircuitMaintenanceService> logger,
        IMeshCircuitBuilder circuitBuilder,
        IMeshPeerManager peerManager)
    {
        logger.LogDebug("[CircuitMaintenanceService] Constructor called");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _circuitBuilder = circuitBuilder ?? throw new ArgumentNullException(nameof(circuitBuilder));
        _peerManager = peerManager ?? throw new ArgumentNullException(nameof(peerManager));
        logger.LogDebug("[CircuitMaintenanceService] Constructor completed");
    }

    /// <summary>
    /// Executes the background maintenance loop.
    /// </summary>
    /// <param name="stoppingToken">The stopping token.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

        _logger.LogDebug("[CircuitMaintenanceService] ExecuteAsync called");
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

    private Task PerformMaintenanceAsync(CancellationToken cancellationToken)
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

        return Task.CompletedTask;
    }
}
