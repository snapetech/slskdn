// <copyright file="PodMessageStorageController.cs" company="slskdn Team">
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
///     Pod message storage management.
/// </summary>
[Route("api/v0/podcore/messages")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodMessageStorageController : ControllerBase
{
    private readonly IPodMessageStorage messageStorage;
    private readonly ILogger<PodMessageStorageController> logger;

    public PodMessageStorageController(
        IPodMessageStorage messageStorage,
        ILogger<PodMessageStorageController> logger)
    {
        this.messageStorage = messageStorage;
        this.logger = logger;
    }

    /// <summary>
    ///     Searches messages in the specified pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="query">The search query.</param>
    /// <param name="channelId">Optional channel ID to limit search scope.</param>
    /// <param name="limit">Maximum number of results (default: 50, max: 500).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search results.</returns>
    /// <response code="200">The search results.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{podId}/search")]
    [ProducesResponseType(typeof(IEnumerable<PodMessage>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SearchMessages(
        [FromRoute] string podId,
        [FromQuery] string query,
        [FromQuery] string channelId = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query is required");
        }

        try
        {
            var results = await messageStorage.SearchMessagesAsync(podId, query, channelId, limit, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching messages in pod {PodId}", podId);
            return StatusCode(500, "An error occurred while searching messages");
        }
    }

    /// <summary>
    ///     Gets message storage statistics.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The storage statistics.</returns>
    /// <response code="200">The storage statistics.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(PodMessageStorageStats), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetStorageStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await messageStorage.GetStorageStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting message storage statistics");
            return StatusCode(500, "An error occurred while getting storage statistics");
        }
    }

    /// <summary>
    ///     Deletes messages older than the specified timestamp.
    /// </summary>
    /// <param name="olderThan">Unix timestamp (milliseconds) before which to delete messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of messages deleted.</returns>
    /// <response code="200">The number of messages deleted.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpDelete("cleanup")]
    [ProducesResponseType(typeof(long), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CleanupMessages(
        [FromQuery] long olderThan,
        CancellationToken cancellationToken = default)
    {
        if (olderThan <= 0)
        {
            return BadRequest("olderThan timestamp must be positive");
        }

        try
        {
            var deletedCount = await messageStorage.DeleteMessagesOlderThanAsync(olderThan, cancellationToken);
            return Ok(deletedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up messages older than {Timestamp}", olderThan);
            return StatusCode(500, "An error occurred while cleaning up messages");
        }
    }

    /// <summary>
    ///     Deletes messages in the specified pod and channel older than the specified timestamp.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="olderThan">Unix timestamp (milliseconds) before which to delete messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of messages deleted.</returns>
    /// <response code="200">The number of messages deleted.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpDelete("{podId}/{channelId}/cleanup")]
    [ProducesResponseType(typeof(long), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CleanupChannelMessages(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        [FromQuery] long olderThan,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return BadRequest("Channel ID is required");
        }

        if (olderThan <= 0)
        {
            return BadRequest("olderThan timestamp must be positive");
        }

        try
        {
            var deletedCount = await messageStorage.DeleteMessagesInChannelOlderThanAsync(podId, channelId, olderThan, cancellationToken);
            return Ok(deletedCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up messages in pod {PodId} channel {ChannelId} older than {Timestamp}",
                podId, channelId, olderThan);
            return StatusCode(500, "An error occurred while cleaning up channel messages");
        }
    }

    /// <summary>
    ///     Gets the message count for the specified pod and channel.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The message count.</returns>
    /// <response code="200">The message count.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("{podId}/{channelId}/count")]
    [ProducesResponseType(typeof(long), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetMessageCount(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            return BadRequest("Channel ID is required");
        }

        try
        {
            var count = await messageStorage.GetMessageCountAsync(podId, channelId, cancellationToken);
            return Ok(count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting message count for pod {PodId} channel {ChannelId}", podId, channelId);
            return StatusCode(500, "An error occurred while getting message count");
        }
    }

    /// <summary>
    ///     Rebuilds the full-text search index.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the rebuild was successful.</returns>
    /// <response code="200">The rebuild result.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("rebuild-index")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RebuildSearchIndex(CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await messageStorage.RebuildSearchIndexAsync(cancellationToken);
            return Ok(success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rebuilding search index");
            return StatusCode(500, "An error occurred while rebuilding search index");
        }
    }

    /// <summary>
    ///     Vacuums the message storage database.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if vacuum was successful.</returns>
    /// <response code="200">The vacuum result.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("vacuum")]
    [ProducesResponseType(typeof(bool), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> VacuumDatabase(CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await messageStorage.VacuumAsync(cancellationToken);
            return Ok(success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error vacuuming database");
            return StatusCode(500, "An error occurred while vacuuming database");
        }
    }
}
