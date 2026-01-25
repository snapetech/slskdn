// <copyright file="DescriptorRetrieverController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Content descriptor retrieval API controller.
/// </summary>
[Route("api/v0/mediacore/retrieve")]
[ApiController]
[AllowAnonymous] // PR-02: intended-public
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class DescriptorRetrieverController : ControllerBase
{
    private readonly ILogger<DescriptorRetrieverController> _logger;
    private readonly IDescriptorRetriever _retriever;

    public DescriptorRetrieverController(
        ILogger<DescriptorRetrieverController> logger,
        IDescriptorRetriever retriever)
    {
        _logger = logger;
        _retriever = retriever;
    }

    /// <summary>
    /// Retrieve a content descriptor by ContentID.
    /// </summary>
    /// <param name="contentId">The ContentID to retrieve.</param>
    /// <param name="bypassCache">Whether to bypass cache (optional).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The descriptor retrieval result.</returns>
    [HttpGet("descriptor/{*contentId}")]
    public async Task<IActionResult> RetrieveDescriptor(
        string contentId,
        [FromQuery] bool bypassCache = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        try
        {
            var result = await _retriever.RetrieveAsync(contentId, bypassCache, cancellationToken);

            if (!result.Found)
            {
                return NotFound(new
                {
                    contentId,
                    found = false,
                    error = result.ErrorMessage ?? "Descriptor not found"
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Failed to retrieve descriptor {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to retrieve descriptor" });
        }
    }

    /// <summary>
    /// Retrieve multiple content descriptors in batch.
    /// </summary>
    /// <param name="request">Batch retrieval request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Batch retrieval results.</returns>
    [HttpPost("batch")]
    public async Task<IActionResult> RetrieveBatch([FromBody] BatchRetrievalRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.ContentIds == null || !request.ContentIds.Any())
        {
            return BadRequest("At least one ContentID is required");
        }

        try
        {
            var result = await _retriever.RetrieveBatchAsync(request.ContentIds, cancellationToken);

            _logger.LogInformation(
                "[DescriptorRetriever] Batch retrieval: {Requested} requested, {Found} found, {Failed} failed",
                result.Requested, result.Found, result.Failed);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Failed to retrieve batch");
            return StatusCode(500, new { error = "Failed to retrieve batch" });
        }
    }

    /// <summary>
    /// Query descriptors by domain and optional type.
    /// </summary>
    /// <param name="domain">The content domain.</param>
    /// <param name="type">Optional content type within the domain.</param>
    /// <param name="maxResults">Maximum results to return (default 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query results.</returns>
    [HttpGet("query/domain/{domain}")]
    public async Task<IActionResult> QueryByDomain(
        string domain,
        [FromQuery] string? type = null,
        [FromQuery] int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return BadRequest("Domain is required");
        }

        if (maxResults < 1 || maxResults > 1000)
        {
            return BadRequest("Max results must be between 1 and 1000");
        }

        try
        {
            var result = await _retriever.QueryByDomainAsync(domain, type, maxResults, cancellationToken);

            _logger.LogInformation(
                "[DescriptorRetriever] Domain query {Domain}:{Type}: {Found} results in {Duration}ms",
                domain, type ?? "all", result.TotalFound, result.QueryDuration.TotalMilliseconds);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Failed to query domain {Domain}", domain);
            return StatusCode(500, new { error = "Failed to query domain" });
        }
    }

    /// <summary>
    /// Verify a content descriptor's signature and freshness.
    /// </summary>
    /// <param name="request">Verification request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    [HttpPost("verify")]
    public async Task<IActionResult> VerifyDescriptor([FromBody] VerifyDescriptorRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Descriptor == null)
        {
            return BadRequest("Descriptor is required");
        }

        try
        {
            var result = await _retriever.VerifyAsync(
                request.Descriptor,
                request.RetrievedAt ?? DateTimeOffset.UtcNow,
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Failed to verify descriptor {ContentId}", request.Descriptor.ContentId);
            return StatusCode(500, new { error = "Failed to verify descriptor" });
        }
    }

    /// <summary>
    /// Get retrieval statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retrieval statistics.</returns>
    [HttpGet("stats")]
    public async Task<IActionResult> GetRetrievalStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _retriever.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Failed to get retrieval stats");
            return StatusCode(500, new { error = "Failed to get retrieval statistics" });
        }
    }

    /// <summary>
    /// Clear the retrieval cache.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cache clearing result.</returns>
    [HttpPost("cache/clear")]
    public async Task<IActionResult> ClearCache(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _retriever.ClearCacheAsync(cancellationToken);

            _logger.LogInformation(
                "[DescriptorRetriever] Cache cleared: {Entries} entries, {Bytes} bytes freed",
                result.EntriesCleared, result.BytesFreed);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Failed to clear cache");
            return StatusCode(500, new { error = "Failed to clear cache" });
        }
    }
}

/// <summary>
/// Batch retrieval request.
/// </summary>
public record BatchRetrievalRequest(IReadOnlyList<string> ContentIds);

/// <summary>
/// Verify descriptor request.
/// </summary>
public record VerifyDescriptorRequest(
    ContentDescriptor Descriptor,
    DateTimeOffset? RetrievedAt = null);

