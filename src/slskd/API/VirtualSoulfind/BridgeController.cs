// <copyright file="BridgeController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using slskd.Core.Security;

using Microsoft.AspNetCore.Mvc;
using slskd.VirtualSoulfind.Bridge;

/// <summary>
/// Bridge API controller for legacy client compatibility.
/// </summary>
[Route("api/bridge")]
[ApiController]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class BridgeController : ControllerBase
{
    private readonly ILogger<BridgeController> logger;
    private readonly IBridgeApi bridgeApi;
    private readonly ISoulfindBridgeService bridgeService;
    private readonly IBridgeDashboard bridgeDashboard;

    public BridgeController(
        ILogger<BridgeController> logger,
        IBridgeApi bridgeApi,
        ISoulfindBridgeService bridgeService,
        IBridgeDashboard bridgeDashboard)
    {
        this.logger = logger;
        this.bridgeApi = bridgeApi;
        this.bridgeService = bridgeService;
        this.bridgeDashboard = bridgeDashboard;
    }

    /// <summary>
    /// Bridge search endpoint (legacy client → mesh).
    /// </summary>
    [HttpPost("search")]
    [Authorize]
    public async Task<IActionResult> Search([FromBody] BridgeSearchRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required" });
        }

        var query = request.Query.Trim();
        logger.LogDebug("Bridge search: {Query}", query);

        try
        {
            var result = await bridgeApi.SearchAsync(query, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge search failed");
            return StatusCode(500, new { error = "Bridge search failed" });
        }
    }

    /// <summary>
    /// Bridge download endpoint (legacy client → mesh).
    /// </summary>
    [HttpPost("download")]
    [Authorize]
    public async Task<IActionResult> Download([FromBody] BridgeDownloadRequest request, CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(new { error = "Request is required" });
        }

        var username = request.Username?.Trim() ?? string.Empty;
        var filename = request.Filename?.Trim() ?? string.Empty;
        var targetPath = request.TargetPath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(filename) || string.IsNullOrWhiteSpace(targetPath))
        {
            return BadRequest(new { error = "Username, filename, and targetPath are required" });
        }

        logger.LogDebug("Bridge download: {Username}/{Filename}", username, filename);

        try
        {
            var transferId = await bridgeApi.DownloadAsync(
                username,
                filename,
                targetPath,
                ct);

            return Ok(new { transfer_id = transferId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge download failed");
            return StatusCode(500, new { error = "Bridge download failed" });
        }
    }

    /// <summary>
    /// Bridge rooms endpoint (scenes → legacy rooms).
    /// </summary>
    [HttpGet("rooms")]
    [Authorize]
    public async Task<IActionResult> GetRooms(CancellationToken ct)
    {
        logger.LogDebug("Bridge get rooms");

        try
        {
            var rooms = await bridgeApi.GetRoomsAsync(ct);
            return Ok(new { rooms });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge get rooms failed");
            return StatusCode(500, new { error = "Bridge get rooms failed" });
        }
    }

    /// <summary>
    /// Get bridge service status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        logger.LogDebug("Bridge get status");

        try
        {
            var health = await bridgeService.GetHealthAsync(ct);
            var stats = await bridgeDashboard.GetStatsAsync(ct);
            health.ActiveConnections = stats.CurrentConnections;
            return Ok(health);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge get status failed");
            return StatusCode(500, new { error = "Bridge get status failed" });
        }
    }

    /// <summary>
    /// Start bridge service.
    /// </summary>
    [HttpPost("start")]
    [Authorize]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        logger.LogInformation("Bridge start requested");

        try
        {
            await bridgeService.StartAsync(ct);
            return Ok(new { status = "started" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge start failed");
            return StatusCode(500, new { error = "Bridge start failed" });
        }
    }

    /// <summary>
    /// Stop bridge service.
    /// </summary>
    [HttpPost("stop")]
    [Authorize]
    public async Task<IActionResult> Stop(CancellationToken ct)
    {
        logger.LogInformation("Bridge stop requested");

        try
        {
            await bridgeService.StopAsync(ct);
            return Ok(new { status = "stopped" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge stop failed");
            return StatusCode(500, new { error = "Bridge stop failed" });
        }
    }

    /// <summary>
    /// Get transfer progress (T-857: Transfer progress proxying).
    /// </summary>
    [HttpGet("transfer/{transferId}/progress")]
    [Authorize]
    public async Task<IActionResult> GetTransferProgress(string transferId, CancellationToken ct)
    {
        transferId = transferId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(transferId))
        {
            return BadRequest(new { error = "TransferId is required" });
        }

        logger.LogDebug("Bridge transfer progress: {TransferId}", transferId);

        try
        {
            var progress = await bridgeApi.GetTransferProgressAsync(transferId, ct);

            if (progress == null)
            {
                return NotFound(new { error = "Transfer not found" });
            }

            return Ok(progress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge transfer progress failed");
            return StatusCode(500, new { error = "Bridge transfer progress failed" });
        }
    }
}

/// <summary>
/// Bridge search request.
/// </summary>
public record BridgeSearchRequest(string Query);

/// <summary>
/// Bridge download request.
/// </summary>
public record BridgeDownloadRequest(string Username, string Filename, string TargetPath);
