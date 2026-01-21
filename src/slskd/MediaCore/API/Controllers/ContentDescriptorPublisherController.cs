// <copyright file="ContentDescriptorPublisherController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

/// <summary>
/// Content descriptor publishing API controller.
/// </summary>
[Route("api/v0/mediacore/publish")]
[ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class ContentDescriptorPublisherController : ControllerBase
{
    private readonly ILogger<ContentDescriptorPublisherController> _logger;
    private readonly IContentDescriptorPublisher _publisher;

    public ContentDescriptorPublisherController(
        ILogger<ContentDescriptorPublisherController> logger,
        IContentDescriptorPublisher publisher)
    {
        _logger = logger;
        _publisher = publisher;
    }

    /// <summary>
    /// Publish a single content descriptor.
    /// </summary>
    /// <param name="request">Publishing request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing result.</returns>
    [HttpPost("descriptor")]
    public async Task<IActionResult> PublishDescriptor([FromBody] PublishDescriptorRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Descriptor == null)
        {
            return BadRequest("Descriptor is required");
        }

        try
        {
            var result = await _publisher.PublishAsync(request.Descriptor, request.ForceUpdate ?? false, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[ContentDescriptorPublisher] Published {ContentId} v{Version}",
                    result.ContentId, result.Version);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning(
                    "[ContentDescriptorPublisher] Failed to publish {ContentId}: {Error}",
                    result.ContentId, result.ErrorMessage);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to publish descriptor");
            return StatusCode(500, new { error = "Failed to publish descriptor" });
        }
    }

    /// <summary>
    /// Publish multiple content descriptors in batch.
    /// </summary>
    /// <param name="request">Batch publishing request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch publishing results.</returns>
    [HttpPost("batch")]
    public async Task<IActionResult> PublishBatch([FromBody] PublishBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Descriptors == null || !request.Descriptors.Any())
        {
            return BadRequest("At least one descriptor is required");
        }

        try
        {
            var result = await _publisher.PublishBatchAsync(request.Descriptors, cancellationToken);

            _logger.LogInformation(
                "[ContentDescriptorPublisher] Batch publish completed: {Successful}/{Total} successful",
                result.SuccessfullyPublished, result.TotalRequested);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to publish batch");
            return StatusCode(500, new { error = "Failed to publish batch" });
        }
    }

    /// <summary>
    /// Update an existing published descriptor.
    /// </summary>
    /// <param name="contentId">The ContentID to update.</param>
    /// <param name="request">Update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result.</returns>
    [HttpPut("descriptor/{*contentId}")]
    public async Task<IActionResult> UpdateDescriptor(string contentId, [FromBody] UpdateDescriptorRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        if (request?.Updates == null)
        {
            return BadRequest("Updates are required");
        }

        try
        {
            var result = await _publisher.UpdateAsync(contentId, request.Updates, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation(
                    "[ContentDescriptorPublisher] Updated {ContentId} from v{Previous} to v{New}",
                    contentId, result.PreviousVersion, result.NewVersion);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning(
                    "[ContentDescriptorPublisher] Failed to update {ContentId}: {Error}",
                    contentId, result.ErrorMessage);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to update descriptor {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to update descriptor" });
        }
    }

    /// <summary>
    /// Republish descriptors that are about to expire.
    /// </summary>
    /// <param name="request">Republish request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Republish results.</returns>
    [HttpPost("republish")]
    public async Task<IActionResult> RepublishExpiring([FromBody] RepublishRequest? request = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _publisher.RepublishExpiringAsync(request?.ContentIds, cancellationToken);

            _logger.LogInformation(
                "[ContentDescriptorPublisher] Republish completed: {Republished}/{Checked} republished",
                result.Republished, result.TotalChecked);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to republish descriptors");
            return StatusCode(500, new { error = "Failed to republish descriptors" });
        }
    }

    /// <summary>
    /// Unpublish a content descriptor.
    /// </summary>
    /// <param name="contentId">The ContentID to unpublish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Unpublish result.</returns>
    [HttpDelete("descriptor/{*contentId}")]
    public async Task<IActionResult> UnpublishDescriptor(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        try
        {
            var result = await _publisher.UnpublishAsync(contentId, cancellationToken);

            _logger.LogInformation(
                "[ContentDescriptorPublisher] Unpublish {ContentId}: {Success}",
                contentId, result.Success ? "successful" : "failed");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to unpublish {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to unpublish descriptor" });
        }
    }

    /// <summary>
    /// Get publishing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetPublishingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _publisher.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to get publishing stats");
            return StatusCode(500, new { error = "Failed to get publishing statistics" });
        }
    }
}

/// <summary>
/// Publish descriptor request.
/// </summary>
public record PublishDescriptorRequest(ContentDescriptor Descriptor, bool? ForceUpdate = null);

/// <summary>
/// Batch publish request.
/// </summary>
public record PublishBatchRequest(IReadOnlyList<ContentDescriptor> Descriptors);

/// <summary>
/// Update descriptor request.
/// </summary>
public record UpdateDescriptorRequest(DescriptorUpdates Updates);

/// <summary>
/// Republish request.
/// </summary>
public record RepublishRequest(IReadOnlyList<string>? ContentIds = null);

