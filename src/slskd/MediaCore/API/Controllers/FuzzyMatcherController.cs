// <copyright file="FuzzyMatcherController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Asp.Versioning;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// Fuzzy content matching API controller.
/// </summary>
[Route("api/v{version:apiVersion}/mediacore/fuzzymatch")]
[ApiVersion("0")]
[ApiController]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class FuzzyMatcherController : ControllerBase
{
    private readonly ILogger<FuzzyMatcherController> _logger;
    private readonly IFuzzyMatcher _fuzzyMatcher;
    private readonly IContentIdRegistry _registry;

    public FuzzyMatcherController(
        ILogger<FuzzyMatcherController> logger,
        IFuzzyMatcher fuzzyMatcher,
        IContentIdRegistry registry)
    {
        _logger = logger;
        _fuzzyMatcher = fuzzyMatcher;
        _registry = registry;
    }

    /// <summary>
    /// Computes perceptual similarity between two ContentIDs.
    /// </summary>
    /// <param name="request">Similarity computation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Similarity analysis result</returns>
    [HttpPost("perceptual")]
    public async Task<IActionResult> ComputePerceptualSimilarity([FromBody] PerceptualSimilarityRequest request, CancellationToken cancellationToken = default)
    {
        var contentIdA = request?.ContentIdA?.Trim() ?? string.Empty;
        var contentIdB = request?.ContentIdB?.Trim() ?? string.Empty;
        var threshold = request?.Threshold ?? 0.7;

        if (string.IsNullOrWhiteSpace(contentIdA) || string.IsNullOrWhiteSpace(contentIdB))
        {
            return BadRequest("Both ContentID A and B are required");
        }

        if (threshold < 0 || threshold > 1)
        {
            return BadRequest("Threshold must be between 0 and 1");
        }

        try
        {
            var similarity = await _fuzzyMatcher.ScorePerceptualAsync(contentIdA, contentIdB, _registry, cancellationToken);

            return Ok(new
            {
                contentIdA,
                contentIdB,
                similarity,
                isSimilar = similarity >= threshold,
                threshold
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FuzzyMatcher] Failed to compute perceptual similarity between {ContentIdA} and {ContentIdB}",
                           contentIdA, contentIdB);
            return StatusCode(500, new { error = "Failed to compute perceptual similarity" });
        }
    }

    /// <summary>
    /// Finds similar content for a given ContentID.
    /// </summary>
    /// <param name="contentId">Target ContentID to find matches for</param>
    /// <param name="request">Search parameters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of similar content matches</returns>
    [HttpPost("find/{*contentId}")]
    public async Task<IActionResult> FindSimilarContent(string contentId, [FromBody] FindSimilarRequest? request = null, CancellationToken cancellationToken = default)
    {
        contentId = contentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        var minConfidence = request?.MinConfidence ?? 0.7;
        var maxCandidates = request?.MaxCandidates ?? 50;
        var maxResults = request?.MaxResults ?? 10;

        if (minConfidence < 0 || minConfidence > 1)
        {
            return BadRequest("MinConfidence must be between 0 and 1");
        }

        if (maxCandidates <= 0 || maxResults <= 0)
        {
            return BadRequest("MaxCandidates and MaxResults must be greater than 0");
        }

        try
        {
            // Get candidate ContentIDs (in a real implementation, this would be more sophisticated)
            var candidates = await GetCandidateContentIdsAsync(contentId, maxCandidates, cancellationToken);

            var matches = await _fuzzyMatcher.FindSimilarContentAsync(
                contentId,
                candidates,
                _registry,
                minConfidence,
                cancellationToken);

            return Ok(new
            {
                targetContentId = contentId,
                totalCandidates = candidates.Count(),
                matches = matches.Take(maxResults),
                searchParameters = new
                {
                    minConfidence,
                    maxCandidates,
                    maxResults
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FuzzyMatcher] Failed to find similar content for {ContentId}", contentId);
            return StatusCode(500, new { error = "Failed to find similar content" });
        }
    }

    /// <summary>
    /// Computes text-based similarity score.
    /// </summary>
    /// <param name="request">Text similarity request</param>
    /// <returns>Similarity score</returns>
    [HttpPost("text")]
    public IActionResult ComputeTextSimilarity([FromBody] TextSimilarityRequest request)
    {
        var textA = request?.TextA?.Trim() ?? string.Empty;
        var textB = request?.TextB?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(textA) || string.IsNullOrWhiteSpace(textB))
        {
            return BadRequest("Both text strings are required");
        }

        try
        {
            var levenshteinScore = _fuzzyMatcher.ScoreLevenshtein(textA, textB);
            var phoneticScore = _fuzzyMatcher.ScorePhonetic(textA, textB);

            // Combined text similarity
            var combinedScore = (levenshteinScore * 0.7) + (phoneticScore * 0.3);

            return Ok(new
            {
                textA,
                textB,
                levenshteinSimilarity = levenshteinScore,
                phoneticSimilarity = phoneticScore,
                combinedSimilarity = combinedScore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FuzzyMatcher] Failed to compute text similarity");
            return StatusCode(500, new { error = "Failed to compute text similarity" });
        }
    }

    /// <summary>
    /// Gets candidate ContentIDs for similarity search.
    /// </summary>
    private async Task<IEnumerable<string>> GetCandidateContentIdsAsync(string targetContentId, int maxCandidates, CancellationToken cancellationToken)
    {
        var parsed = ContentIdParser.Parse(targetContentId);
        if (parsed == null)
            return Enumerable.Empty<string>();

        // Get candidates from the same domain
        var candidates = await _registry.FindByDomainAsync(
            ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type),
            cancellationToken);

        // Keep ordering deterministic so repeated identical requests return stable candidate sets.
        return candidates
            .Where(c => !string.Equals(c, targetContentId, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.Ordinal)
            .Take(maxCandidates)
            .ToList();
    }
}

/// <summary>
/// Perceptual similarity request.
/// </summary>
public record PerceptualSimilarityRequest(string ContentIdA, string ContentIdB, double? Threshold = null);

/// <summary>
/// Find similar content request.
/// </summary>
public record FindSimilarRequest(double? MinConfidence = null, int? MaxCandidates = null, int? MaxResults = null);

/// <summary>
/// Text similarity request.
/// </summary>
public record TextSimilarityRequest(string TextA, string TextB);
