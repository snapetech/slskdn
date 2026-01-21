// <copyright file="IpldController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// IPLD content graph API controller.
/// </summary>
[Route("api/v0/mediacore/ipld")]
[ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class IpldController : ControllerBase
{
    private readonly ILogger<IpldController> _logger;
    private readonly IIpldMapper _ipldMapper;

    public IpldController(
        ILogger<IpldController> logger,
        IIpldMapper ipldMapper)
    {
        _logger = logger;
        _ipldMapper = ipldMapper;
    }

    /// <summary>
    /// Traverse the content graph following a specific link type.
    /// </summary>
    /// <param name="startContentId">The starting ContentID.</param>
    /// <param name="linkName">The link name to follow.</param>
    /// <param name="maxDepth">Maximum traversal depth (1-10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The traversal result.</returns>
    [HttpGet("traverse/{*startContentId}")]
    public async Task<IActionResult> Traverse(
        string startContentId,
        [FromQuery] string linkName,
        [FromQuery] int maxDepth = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(startContentId))
        {
            return BadRequest("Start ContentID is required");
        }

        if (string.IsNullOrWhiteSpace(linkName))
        {
            return BadRequest("Link name is required");
        }

        if (maxDepth < 1 || maxDepth > 10)
        {
            return BadRequest("Max depth must be between 1 and 10");
        }

        try
        {
            var result = await _ipldMapper.TraverseAsync(startContentId, linkName, maxDepth, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Failed to traverse from {StartContentId} following {LinkName}", startContentId, linkName);
            return StatusCode(500, new { error = "Failed to traverse content graph" });
        }
    }

    /// <summary>
    /// Get the content graph for a specific ContentID.
    /// </summary>
    /// <param name="contentId">The ContentID to get the graph for.</param>
    /// <param name="maxDepth">Maximum graph depth (1-5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content graph structure.</returns>
    [HttpGet("graph/{*contentId}")]
    public async Task<IActionResult> GetGraph(
        string contentId,
        [FromQuery] int maxDepth = 2,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        if (maxDepth < 1 || maxDepth > 5)
        {
            return BadRequest("Max depth must be between 1 and 5");
        }

        try
        {
            var graph = await _ipldMapper.GetGraphAsync(contentId, maxDepth, cancellationToken);
            return Ok(graph);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Failed to get graph for {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to get content graph" });
        }
    }

    /// <summary>
    /// Find all content that links to the specified ContentID.
    /// </summary>
    /// <param name="targetContentId">The target ContentID.</param>
    /// <param name="linkName">Optional link name filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of ContentIDs that link to the target.</returns>
    [HttpGet("inbound/{*targetContentId}")]
    public async Task<IActionResult> FindInboundLinks(
        string targetContentId,
        [FromQuery] string? linkName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetContentId))
        {
            return BadRequest("Target ContentID is required");
        }

        try
        {
            var inboundLinks = await _ipldMapper.FindInboundLinksAsync(targetContentId, linkName, cancellationToken);
            return Ok(new { targetContentId, linkName, inboundLinks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Failed to find inbound links for {TargetContentId}", targetContentId);
            return StatusCode(500, new { error = "Failed to find inbound links" });
        }
    }

    /// <summary>
    /// Validate IPLD links in the registry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation results.</returns>
    [HttpGet("validate")]
    public async Task<IActionResult> ValidateLinks(CancellationToken cancellationToken = default)
    {
        try
        {
            var validation = await _ipldMapper.ValidateLinksAsync(cancellationToken);
            return Ok(validation);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Failed to validate links");
            return StatusCode(500, new { error = "Failed to validate IPLD links" });
        }
    }

    /// <summary>
    /// Add IPLD links to a content descriptor.
    /// </summary>
    /// <param name="contentId">The ContentID to add links to.</param>
    /// <param name="request">The link addition request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    [HttpPost("links/{*contentId}")]
    public async Task<IActionResult> AddLinks(
        string contentId,
        [FromBody] AddLinksRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        if (request?.Links == null || !request.Links.Any())
        {
            return BadRequest("At least one link is required");
        }

        try
        {
            var links = request.Links.Select(l => new IpldLink(l.Name, l.Target, l.LinkName)).ToList();
            await _ipldMapper.AddLinksAsync(contentId, links, cancellationToken);

            _logger.LogInformation(
                "[IPLD] Added {LinkCount} links to {ContentId}",
                links.Count, contentId);

            return Ok(new { message = $"Added {links.Count} links to {contentId}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Failed to add links to {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to add IPLD links" });
        }
    }
}

/// <summary>
/// Request model for adding IPLD links.
/// </summary>
public record AddLinksRequest(IReadOnlyList<IpldLinkRequest> Links);

/// <summary>
/// IPLD link request model.
/// </summary>
public record IpldLinkRequest(string Name, string Target, string? LinkName = null);

