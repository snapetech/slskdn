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
    private readonly ITransferProgressProxy progressProxy;

    public BridgeController(
        ILogger<BridgeController> logger,
        IBridgeApi bridgeApi,
        ISoulfindBridgeService bridgeService,
        ITransferProgressProxy progressProxy)
    {
        this.logger = logger;
        this.bridgeApi = bridgeApi;
        this.bridgeService = bridgeService;
        this.progressProxy = progressProxy;
    }

    /// <summary>
    /// Bridge search endpoint (legacy client → mesh).
    /// </summary>
    [HttpPost("search")]
    [Authorize]
    public async Task<IActionResult> Search([FromBody] BridgeSearchRequest request, CancellationToken ct)
    {
        logger.LogDebug("Bridge search: {Query}", request.Query);

        try
        {
            var result = await bridgeApi.SearchAsync(request.Query, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge search failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Bridge download endpoint (legacy client → mesh).
    /// </summary>
    [HttpPost("download")]
    [Authorize]
    public async Task<IActionResult> Download([FromBody] BridgeDownloadRequest request, CancellationToken ct)
    {
        logger.LogDebug("Bridge download: {Username}/{Filename}", request.Username, request.Filename);

        try
        {
            var transferId = await bridgeApi.DownloadAsync(
                request.Username,
                request.Filename,
                request.TargetPath,
                ct);

            return Ok(new { transfer_id = transferId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge download failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
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
            logger.LogError(ex, "Bridge get rooms failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
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
            return Ok(health);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge get status failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
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
            logger.LogError(ex, "Bridge start failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
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
            logger.LogError(ex, "Bridge stop failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get transfer progress (T-857: Transfer progress proxying).
    /// </summary>
    [HttpGet("transfer/{transferId}/progress")]
    [Authorize]
    public async Task<IActionResult> GetTransferProgress(string transferId, CancellationToken ct)
    {
        logger.LogDebug("Bridge transfer progress: {TransferId}", transferId);

        try
        {
            // Try to find proxy ID from transfer ID
            // In practice, we'd maintain a mapping, but for now we'll use transfer ID as proxy ID
            var progress = await progressProxy.GetLegacyProgressAsync(transferId, ct);
            
            if (progress == null)
            {
                return NotFound(new { error = "Transfer not found" });
            }

            return Ok(progress);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bridge transfer progress failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message });
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
