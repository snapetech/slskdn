// <copyright file="ContentIdController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

/// <summary>
/// ContentID registry API controller.
/// </summary>
[Route("api/v0/mediacore/contentid")]
[ApiController]
public class ContentIdController : ControllerBase
{
    private readonly ILogger<ContentIdController> _logger;
    private readonly IContentIdRegistry _registry;

    public ContentIdController(
        ILogger<ContentIdController> logger,
        IContentIdRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    /// <summary>
    /// Register a mapping from external ID to ContentID.
    /// </summary>
    /// <param name="request">The registration request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration result.</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] ContentIdRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ExternalId) || string.IsNullOrWhiteSpace(request.ContentId))
        {
            return BadRequest("ExternalId and ContentId are required");
        }

        try
        {
            await _registry.RegisterAsync(request.ExternalId, request.ContentId, cancellationToken);

            _logger.LogInformation(
                "[ContentID] Registered mapping: {ExternalId} -> {ContentId}",
                request.ExternalId, request.ContentId);

            return Ok(new { message = "ContentID mapping registered successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentID] Failed to register mapping: {ExternalId} -> {ContentId}", request.ExternalId, request.ContentId);
            return StatusCode(500, new { error = "Failed to register ContentID mapping" });
        }
    }

    /// <summary>
    /// Resolve an external ID to its ContentID.
    /// </summary>
    /// <param name="externalId">The external ID to resolve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved ContentID or 404 if not found.</returns>
    [HttpGet("resolve/{externalId}")]
    public async Task<IActionResult> Resolve(string externalId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return BadRequest("ExternalId is required");
        }

        try
        {
            var contentId = await _registry.ResolveAsync(externalId, cancellationToken);

            if (contentId == null)
            {
                return NotFound(new { error = $"External ID '{externalId}' not found" });
            }

            return Ok(new { externalId, contentId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentID] Failed to resolve external ID: {ExternalId}", externalId);
            return StatusCode(500, new { error = "Failed to resolve external ID" });
        }
    }

    /// <summary>
    /// Check if an external ID is registered.
    /// </summary>
    /// <param name="externalId">The external ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether the external ID is registered.</returns>
    [HttpGet("exists/{externalId}")]
    public async Task<IActionResult> Exists(string externalId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
        {
            return BadRequest("ExternalId is required");
        }

        try
        {
            var exists = await _registry.IsRegisteredAsync(externalId, cancellationToken);
            return Ok(new { externalId, exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentID] Failed to check existence of external ID: {ExternalId}", externalId);
            return StatusCode(500, new { error = "Failed to check external ID existence" });
        }
    }

    /// <summary>
    /// Get all external IDs mapped to a ContentID.
    /// </summary>
    /// <param name="contentId">The ContentID to query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of external IDs mapped to the ContentID.</returns>
    [HttpGet("external/{contentId}")]
    public async Task<IActionResult> GetExternalIds(string contentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentId is required");
        }

        try
        {
            var externalIds = await _registry.GetExternalIdsAsync(contentId, cancellationToken);
            return Ok(new { contentId, externalIds });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentID] Failed to get external IDs for ContentID: {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to get external IDs" });
        }
    }

    /// <summary>
    /// Get ContentID registry statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Registry statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _registry.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentID] Failed to get registry statistics");
            return StatusCode(500, new { error = "Failed to get registry statistics" });
        }
    }
}

/// <summary>
/// Request model for ContentID registration.
/// </summary>
public record ContentIdRegistrationRequest(string ExternalId, string ContentId);
