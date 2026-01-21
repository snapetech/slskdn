// <copyright file="PodMessageBackfillController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore.API.Controllers;

using slskd.Core.Security;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Pod message backfill management.
/// </summary>
[Route("api/v0/podcore/backfill")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodMessageBackfillController : ControllerBase
{
    private readonly IPodMessageBackfill _backfillService;
    private readonly ILogger<PodMessageBackfillController> _logger;

    public PodMessageBackfillController(
        IPodMessageBackfill backfillService,
        ILogger<PodMessageBackfillController> logger)
    {
        _backfillService = backfillService;
        _logger = logger;
    }

    /// <summary>
    ///     Triggers backfill synchronization for a pod after rejoining.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="lastSeenTimestamps">Last seen message timestamps per channel.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backfill result.</returns>
    /// <response code="200">The backfill operation completed.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("{podId}/sync")]
    [ProducesResponseType(typeof(PodBackfillResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SyncOnRejoin(
        [FromRoute] string podId,
        [FromBody] Dictionary<string, long> lastSeenTimestamps,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (lastSeenTimestamps == null || lastSeenTimestamps.Count == 0)
        {
            return BadRequest("Last seen timestamps are required");
        }

        try
        {
            var result = await _backfillService.SyncOnRejoinAsync(podId, lastSeenTimestamps, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing backfill for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while syncing backfill");
        }
    }

    /// <summary>
    ///     Gets the current last seen timestamps for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <returns>The last seen timestamps per channel.</returns>
    /// <response code="200">The last seen timestamps.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{podId}/last-seen")]
    [ProducesResponseType(typeof(Dictionary<string, long>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public IActionResult GetLastSeenTimestamps([FromRoute] string podId)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        try
        {
            var timestamps = _backfillService.GetLastSeenTimestamps(podId);
            return Ok(timestamps);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last seen timestamps for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while getting last seen timestamps");
        }
    }

    /// <summary>
    ///     Updates the last seen timestamp for a channel.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="timestamp">The timestamp of the last seen message.</param>
    /// <returns>Success status.</returns>
    /// <response code="200">The timestamp was updated.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPut("{podId}/{channelId}/last-seen")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public IActionResult UpdateLastSeenTimestamp(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        [FromBody] long timestamp)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return BadRequest("Channel ID is required");
        }

        if (timestamp <= 0)
        {
            return BadRequest("Timestamp must be positive");
        }

        try
        {
            _backfillService.UpdateLastSeenTimestamp(podId, channelId, timestamp);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating last seen timestamp for pod {PodId} channel {ChannelId}", podId, channelId);
            return StatusCode(500, "An error occurred while updating last seen timestamp");
        }
    }

    /// <summary>
    ///     Gets backfill statistics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backfill statistics.</returns>
    /// <response code="200">The backfill statistics.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(PodBackfillStats), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetBackfillStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _backfillService.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting backfill statistics");
            return StatusCode(500, "An error occurred while getting backfill statistics");
        }
    }

    /// <summary>
    ///     Manually triggers backfill for all pods (for testing/debugging).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The backfill results for all pods.</returns>
    /// <response code="200">The backfill operations completed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("sync-all")]
    [ProducesResponseType(typeof(List<PodBackfillResult>), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SyncAllPods(CancellationToken cancellationToken = default)
    {
        try
        {
            // This would need to be implemented to get all pods the local peer is a member of
            // For now, return a placeholder response
            var results = new List<PodBackfillResult>();
            _logger.LogWarning("SyncAllPods not fully implemented - needs pod membership integration");
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing all pods for backfill");
            return StatusCode(500, "An error occurred while syncing all pods");
        }
    }
}
