// <copyright file="FuzzyMatcherController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

/// <summary>
/// Fuzzy content matching API controller.
/// </summary>
[Route("api/v0/mediacore/fuzzymatch")]
[ApiController]
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
        if (string.IsNullOrWhiteSpace(request?.ContentIdA) || string.IsNullOrWhiteSpace(request?.ContentIdB))
        {
            return BadRequest("Both ContentID A and B are required");
        }

        try
        {
            var similarity = await _fuzzyMatcher.ScorePerceptualAsync(request.ContentIdA, request.ContentIdB, _registry, cancellationToken);

            return Ok(new
            {
                contentIdA = request.ContentIdA,
                contentIdB = request.ContentIdB,
                similarity,
                isSimilar = similarity >= (request.Threshold ?? 0.7),
                threshold = request.Threshold ?? 0.7
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FuzzyMatcher] Failed to compute perceptual similarity between {ContentIdA} and {ContentIdB}",
                           request.ContentIdA, request.ContentIdB);
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
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("ContentID is required");
        }

        try
        {
            // Get candidate ContentIDs (in a real implementation, this would be more sophisticated)
            var candidates = await GetCandidateContentIdsAsync(contentId, request?.MaxCandidates ?? 50, cancellationToken);

            var matches = await _fuzzyMatcher.FindSimilarContentAsync(
                contentId,
                candidates,
                _registry,
                request?.MinConfidence ?? 0.7,
                cancellationToken);

            return Ok(new
            {
                targetContentId = contentId,
                totalCandidates = candidates.Count(),
                matches = matches.Take(request?.MaxResults ?? 10),
                searchParameters = new
                {
                    minConfidence = request?.MinConfidence ?? 0.7,
                    maxCandidates = request?.MaxCandidates ?? 50,
                    maxResults = request?.MaxResults ?? 10
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
        if (request == null || string.IsNullOrWhiteSpace(request.TextA) || string.IsNullOrWhiteSpace(request.TextB))
        {
            return BadRequest("Both text strings are required");
        }

        try
        {
            var levenshteinScore = _fuzzyMatcher.ScoreLevenshtein(request.TextA, request.TextB);
            var phoneticScore = _fuzzyMatcher.ScorePhonetic(request.TextA, request.TextB);

            // Combined text similarity
            var combinedScore = (levenshteinScore * 0.7) + (phoneticScore * 0.3);

            return Ok(new
            {
                textA = request.TextA,
                textB = request.TextB,
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
        var candidates = await _registry.FindByDomainAsync(parsed.Domain, cancellationToken);

        // Limit and randomize for performance (in a real implementation, this would be more sophisticated)
        return candidates
            .Where(c => c != targetContentId) // Exclude self
            .Take(maxCandidates)
            .OrderBy(c => Guid.NewGuid()); // Randomize order
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

