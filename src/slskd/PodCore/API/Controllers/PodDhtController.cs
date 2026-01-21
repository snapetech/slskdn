// <copyright file="PodDhtController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.PodCore.API.Controllers;

/// <summary>
/// Pod DHT publishing API controller.
/// </summary>
[Route("api/v0/podcore/dht")]
[ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodDhtController : ControllerBase
{
    private readonly ILogger<PodDhtController> _logger;
    private readonly IPodDhtPublisher _podPublisher;

    public PodDhtController(
        ILogger<PodDhtController> logger,
        IPodDhtPublisher podPublisher)
    {
        _logger = logger;
        _podPublisher = podPublisher;
    }

    /// <summary>
    /// Publishes pod metadata to the DHT.
    /// </summary>
    /// <param name="request">The publish request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The publish result.</returns>
    [HttpPost("publish")]
    public async Task<IActionResult> PublishPod([FromBody] PublishPodRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Pod == null)
        {
            return BadRequest(new { error = "Pod data is required" });
        }

        try
        {
            var result = await _podPublisher.PublishAsync(request.Pod, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Published pod {PodId} to DHT", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to publish pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error publishing pod");
            return StatusCode(500, new { error = "Failed to publish pod" });
        }
    }

    /// <summary>
    /// Updates existing pod metadata in the DHT.
    /// </summary>
    /// <param name="request">The update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    [HttpPost("update")]
    public async Task<IActionResult> UpdatePod([FromBody] UpdatePodRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Pod == null)
        {
            return BadRequest(new { error = "Pod data is required" });
        }

        try
        {
            var result = await _podPublisher.UpdateAsync(request.Pod, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Updated pod {PodId} in DHT", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to update pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error updating pod");
            return StatusCode(500, new { error = "Failed to update pod" });
        }
    }

    /// <summary>
    /// Unpublishes pod metadata from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID to unpublish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unpublish result.</returns>
    [HttpDelete("unpublish/{*podId}")]
    public async Task<IActionResult> UnpublishPod(string podId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.UnpublishAsync(podId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Unpublished pod {PodId} from DHT", result.PodId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to unpublish pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error unpublishing pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to unpublish pod" });
        }
    }

    /// <summary>
    /// Gets published pod metadata from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pod metadata.</returns>
    [HttpGet("metadata/{*podId}")]
    public async Task<IActionResult> GetPodMetadata(string podId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.GetPublishedMetadataAsync(podId, cancellationToken);

            if (result.Found)
            {
                return Ok(result);
            }
            else
            {
                return NotFound(new { podId, found = false, error = result.ErrorMessage ?? "Pod not found" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error retrieving pod metadata for {PodId}", podId);
            return StatusCode(500, new { error = "Failed to retrieve pod metadata" });
        }
    }

    /// <summary>
    /// Refreshes published pod metadata.
    /// </summary>
    /// <param name="podId">The pod ID to refresh.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refresh result.</returns>
    [HttpPost("refresh/{*podId}")]
    public async Task<IActionResult> RefreshPod(string podId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest(new { error = "Pod ID is required" });
        }

        try
        {
            var result = await _podPublisher.RefreshAsync(podId, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("[PodDht] Refreshed pod {PodId}, republished: {Republished}", result.PodId, result.WasRepublished);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("[PodDht] Failed to refresh pod {PodId}: {Error}", result.PodId, result.ErrorMessage);
                return StatusCode(500, new { error = result.ErrorMessage });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error refreshing pod {PodId}", podId);
            return StatusCode(500, new { error = "Failed to refresh pod" });
        }
    }

    /// <summary>
    /// Gets pod publishing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetPublishingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _podPublisher.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDht] Error getting publishing stats");
            return StatusCode(500, new { error = "Failed to get publishing statistics" });
        }
    }
}

/// <summary>
/// Request to publish a pod.
/// </summary>
public record PublishPodRequest(Pod Pod);

/// <summary>
/// Request to update a pod.
/// </summary>
public record UpdatePodRequest(Pod Pod);
