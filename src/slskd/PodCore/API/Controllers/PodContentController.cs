// <copyright file="PodContentController.cs" company="slskdn Team">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
///     Pod content linking and validation.
/// </summary>
[Route("api/v0/podcore/content")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class PodContentController : ControllerBase
{
    private readonly IContentLinkService _contentLinkService;
    private readonly ILogger<PodContentController> _logger;

    public PodContentController(
        IContentLinkService contentLinkService,
        ILogger<PodContentController> logger)
    {
        _contentLinkService = contentLinkService;
        _logger = logger;
    }

    /// <summary>
    ///     Validates a content ID for pod linking.
    /// </summary>
    /// <param name="contentId">The content ID to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <response code="200">The validation result.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ContentValidationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ValidateContentId(
        [FromBody] string contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        try
        {
            var result = await _contentLinkService.ValidateContentIdAsync(contentId.Trim(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating content ID {ContentId}", contentId);
            return StatusCode(500, "An error occurred while validating the content ID");
        }
    }

    /// <summary>
    ///     Gets metadata for a content ID.
    /// </summary>
    /// <param name="contentId">The content ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The content metadata.</returns>
    /// <response code="200">The content metadata.</response>
    /// <response code="404">The content was not found.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ContentMetadata), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetContentMetadata(
        [FromQuery] string contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        try
        {
            var metadata = await _contentLinkService.GetContentMetadataAsync(contentId.Trim(), cancellationToken);
            if (metadata == null)
            {
                return NotFound("Content not found");
            }

            return Ok(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metadata for content ID {ContentId}", contentId);
            return StatusCode(500, "An error occurred while getting content metadata");
        }
    }

    /// <summary>
    ///     Searches for content that can be linked to pods.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="domain">Optional domain filter (audio, video, etc.).</param>
    /// <param name="limit">Maximum number of results (default: 20, max: 100).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The search results.</returns>
    /// <response code="200">The search results.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<ContentSearchResult>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SearchContent(
        [FromQuery] string query,
        [FromQuery] string domain = null,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest("Search query is required");
        }

        // Enforce reasonable limits
        limit = Math.Min(Math.Max(1, limit), 100);

        try
        {
            var results = await _contentLinkService.SearchContentAsync(query.Trim(), domain, limit, cancellationToken);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching content with query '{Query}'", query);
            return StatusCode(500, "An error occurred while searching content");
        }
    }

    /// <summary>
    ///     Creates a pod linked to specific content.
    /// </summary>
    /// <param name="request">The pod creation request with content linking.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created pod.</returns>
    /// <response code="201">The pod was created.</response>
    /// <response code="400">The request is malformed or content link is invalid.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("create-pod")]
    [ProducesResponseType(typeof(Pod), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateContentLinkedPod(
        [FromBody] ContentLinkedPodRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return BadRequest("Pod creation request is required");
        }

        if (string.IsNullOrWhiteSpace(request.ContentId))
        {
            return BadRequest("Content ID is required for content-linked pods");
        }

        try
        {
            // Create the pod object
            var pod = new Pod
            {
                PodId = request.PodId,
                Name = request.Name,
                Visibility = request.Visibility,
                FocusContentId = request.ContentId,
                Tags = request.Tags ?? new List<string>(),
                Channels = request.Channels ?? new List<PodChannel>(),
                ExternalBindings = request.ExternalBindings ?? new List<ExternalBinding>(),
            };

            // Use the content-linked creation method
            var createdPod = await HttpContext.RequestServices
                .GetRequiredService<IPodService>()
                .CreateContentLinkedPodAsync(pod, cancellationToken);

            return CreatedAtAction("GetPod", "PodController", new { podId = createdPod.PodId }, createdPod);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating content-linked pod");
            return StatusCode(500, "An error occurred while creating the pod");
        }
    }
}

/// <summary>
///     Request for creating a content-linked pod.
/// </summary>
public record ContentLinkedPodRequest(
    string PodId,
    string Name,
    PodVisibility Visibility,
    string ContentId,
    List<string> Tags = null,
    List<PodChannel> Channels = null,
    List<ExternalBinding> ExternalBindings = null);
