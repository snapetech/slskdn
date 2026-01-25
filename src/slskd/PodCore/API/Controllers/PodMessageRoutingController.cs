// <copyright file="PodMessageRoutingController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Pod message routing API controller.
/// </summary>
[Route("api/v0/podcore/routing")]
[ApiController]
[AllowAnonymous] // PR-02: intended-public
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodMessageRoutingController : ControllerBase
{
    private readonly ILogger<PodMessageRoutingController> _logger;
    private readonly IPodMessageRouter _messageRouter;

    public PodMessageRoutingController(
        ILogger<PodMessageRoutingController> logger,
        IPodMessageRouter messageRouter)
    {
        _logger = logger;
        _messageRouter = messageRouter;
    }

    /// <summary>
    /// Manually routes a pod message to pod members.
    /// </summary>
    /// <param name="message">The pod message to route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The routing result.</returns>
    [HttpPost("route")]
    public async Task<IActionResult> RouteMessage([FromBody] PodMessage message, CancellationToken cancellationToken = default)
    {
        if (message == null || string.IsNullOrWhiteSpace(message.MessageId) || string.IsNullOrWhiteSpace(message.ChannelId))
        {
            return BadRequest(new { error = "Valid pod message with MessageId and ChannelId is required" });
        }

        try
        {
            var result = await _messageRouter.RouteMessageAsync(message, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[PodMessageRouting] Manually routed message {MessageId} to {Success}/{Total} peers in pod {PodId}",
                    result.MessageId, result.SuccessfullyRoutedCount, result.TargetPeerCount, result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning(
                    "[PodMessageRouting] Failed to route message {MessageId}: {Error}",
                    result.MessageId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouting] Error routing message");
            return StatusCode(500, new { error = "Failed to route message" });
        }
    }

    /// <summary>
    /// Routes a pod message to specific peers.
    /// </summary>
    /// <param name="request">The routing request with message and target peers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The routing result.</returns>
    [HttpPost("route-to-peers")]
    public async Task<IActionResult> RouteMessageToPeers([FromBody] PodMessagePeerRoutingRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Message == null || request.TargetPeerIds == null || !request.TargetPeerIds.Any())
        {
            return BadRequest(new { error = "Valid message and target peer IDs are required" });
        }

        try
        {
            var result = await _messageRouter.RouteMessageToPeersAsync(request.Message, request.TargetPeerIds, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[PodMessageRouting] Routed message {MessageId} to {Success}/{Total} specific peers",
                    result.MessageId, result.SuccessfullyRoutedCount, result.TargetPeerCount);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning(
                    "[PodMessageRouting] Partially failed to route message {MessageId}: {Failed} failures",
                    result.MessageId, result.FailedRoutingCount);
                return Ok(result); // Still return 200 since some routing may have succeeded
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouting] Error routing message to peers");
            return StatusCode(500, new { error = "Failed to route message to peers" });
        }
    }

    /// <summary>
    /// Gets message routing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Routing statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetRoutingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _messageRouter.GetRoutingStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouting] Error getting routing stats");
            return StatusCode(500, new { error = "Failed to get routing statistics" });
        }
    }

    /// <summary>
    /// Checks if a message has been seen for deduplication.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="podId">The pod ID.</param>
    /// <returns>Whether the message has been seen.</returns>
    [HttpGet("seen/{messageId}/{podId}")]
    public IActionResult IsMessageSeen(string messageId, string podId)
    {
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "MessageId and PodId are required" });
        }

        try
        {
            var isSeen = _messageRouter.IsMessageSeen(messageId, podId);
            return Ok(new { messageId, podId, isSeen });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouting] Error checking if message is seen");
            return StatusCode(500, new { error = "Failed to check message seen status" });
        }
    }

    /// <summary>
    /// Manually registers a message as seen for deduplication.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="podId">The pod ID.</param>
    /// <returns>The registration result.</returns>
    [HttpPost("seen/{messageId}/{podId}")]
    public IActionResult RegisterMessageSeen(string messageId, string podId)
    {
        if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "MessageId and PodId are required" });
        }

        try
        {
            var wasNew = _messageRouter.RegisterMessageSeen(messageId, podId);
            return Ok(new { messageId, podId, wasNewlyRegistered = wasNew });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouting] Error registering message as seen");
            return StatusCode(500, new { error = "Failed to register message as seen" });
        }
    }

    /// <summary>
    /// Cleans up old seen message entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupSeenMessages(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _messageRouter.CleanupSeenMessagesAsync(cancellationToken);
            _logger.LogInformation(
                "[PodMessageRouting] Cleanup completed: {Cleaned} messages cleaned, {Retained} retained",
                result.MessagesCleaned, result.MessagesRetained);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouting] Error cleaning up seen messages");
            return StatusCode(500, new { error = "Failed to cleanup seen messages" });
        }
    }
}

/// <summary>
/// Request to route a message to specific peers.
/// </summary>
public record PodMessagePeerRoutingRequest(
    PodMessage Message,
    IReadOnlyList<string> TargetPeerIds);

