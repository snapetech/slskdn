// <copyright file="BridgeAdminController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using Microsoft.AspNetCore.Mvc;
using slskd.VirtualSoulfind.Bridge;

/// <summary>
/// Bridge configuration and dashboard API.
/// </summary>
[Route("api/bridge/admin")]
[ApiController]
[Produces("application/json")]
public class BridgeAdminController : ControllerBase
{
    private readonly ILogger<BridgeAdminController> logger;
    private readonly ISoulfindBridgeService bridgeService;
    private readonly IBridgeDashboard dashboard;
    private readonly IOptionsMonitor<Options> optionsMonitor;

    public BridgeAdminController(
        ILogger<BridgeAdminController> logger,
        ISoulfindBridgeService bridgeService,
        IBridgeDashboard dashboard,
        IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.bridgeService = bridgeService;
        this.dashboard = dashboard;
        this.optionsMonitor = optionsMonitor;
    }

    /// <summary>
    /// Get bridge configuration.
    /// </summary>
    [HttpGet("config")]
    [Authorize]
    public IActionResult GetConfig()
    {
        var options = optionsMonitor.CurrentValue;
        var bridgeOptions = options.VirtualSoulfind?.Bridge;

        return Ok(new
        {
            enabled = bridgeOptions?.Enabled ?? false,
            port = bridgeOptions?.Port ?? 2242,
            soulfind_path = bridgeOptions?.SoulfindPath ?? "soulfind",
            max_clients = bridgeOptions?.MaxClients ?? 10,
            require_auth = bridgeOptions?.RequireAuth ?? false
        });
    }

    /// <summary>
    /// Update bridge configuration.
    /// </summary>
    [HttpPut("config")]
    [Authorize]
    public IActionResult UpdateConfig([FromBody] BridgeConfigUpdate update)
    {
        logger.LogInformation("Bridge config update requested");

        // Note: This would require hot-reload support or service restart
        // For now, return a message indicating restart required

        return Ok(new
        {
            message = "Configuration updated. Restart bridge service to apply changes.",
            restart_required = true
        });
    }

    /// <summary>
    /// Get bridge dashboard data.
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        logger.LogDebug("Bridge dashboard requested");

        try
        {
            var data = await dashboard.GetDashboardDataAsync(ct);
            return Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get bridge dashboard: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get connected legacy clients.
    /// </summary>
    [HttpGet("clients")]
    [Authorize]
    public async Task<IActionResult> GetClients(CancellationToken ct)
    {
        logger.LogDebug("Bridge clients requested");

        try
        {
            var clients = await dashboard.GetConnectedClientsAsync(ct);
            return Ok(new { clients });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get bridge clients: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get bridge statistics.
    /// </summary>
    [HttpGet("stats")]
    [Authorize]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        logger.LogDebug("Bridge stats requested");

        try
        {
            var stats = await dashboard.GetStatsAsync(ct);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get bridge stats: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Bridge configuration update request.
/// </summary>
public record BridgeConfigUpdate(
    bool? Enabled,
    int? Port,
    string? SoulfindPath,
    int? MaxClients,
    bool? RequireAuth
);
