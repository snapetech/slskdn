// <copyright file="WarmCacheController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd;
using slskd.Transfers.MultiSource.Caching;
using OptionsModel = slskd.Options;

/// <summary>
/// Provides slskdn-native warm cache hints API.
/// </summary>
[ApiController]
[Route("api/slskdn/warm-cache")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class WarmCacheController : ControllerBase
{
    private readonly IWarmCachePopularityService popularityService;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private readonly ILogger<WarmCacheController> logger;

    public WarmCacheController(
        IWarmCachePopularityService popularityService,
        IOptionsMonitor<OptionsModel> optionsMonitor,
        ILogger<WarmCacheController> logger)
    {
        this.popularityService = popularityService;
        this.optionsMonitor = optionsMonitor;
        this.logger = logger;
    }

    /// <summary>
    /// Submit popularity hints for warm cache prefetching.
    /// </summary>
    [HttpPost("hints")]
    [Authorize]
    public async Task<IActionResult> SubmitHints(
        [FromBody] WarmCacheHintsRequest request,
        CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.WarmCache?.Enabled != true)
        {
            return BadRequest(new { error = "Warm cache not enabled" });
        }

        if (request == null)
        {
            return BadRequest(new { error = "Request is required" });
        }

        var releaseIds = (request.MbReleaseIds ?? new List<string>())
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var artistIds = (request.MbArtistIds ?? new List<string>())
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var labelIds = (request.MbLabelIds ?? new List<string>())
            .Select(id => id?.Trim() ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (releaseIds.Count == 0 && artistIds.Count == 0 && labelIds.Count == 0)
        {
            return BadRequest(new { error = "At least one MusicBrainz identifier is required" });
        }

        logger.LogInformation("Received warm cache hints: {ReleaseCount} releases, {ArtistCount} artists, {LabelCount} labels",
            releaseIds.Count,
            artistIds.Count,
            labelIds.Count);

        // Record popularity for each hinted item
        var tasks = new List<Task>();

        if (releaseIds.Count != 0)
        {
            foreach (var releaseId in releaseIds)
            {
                tasks.Add(popularityService.RecordAccessAsync($"mb:release:{releaseId}", cancellationToken));
            }
        }

        if (artistIds.Count != 0)
        {
            foreach (var artistId in artistIds)
            {
                tasks.Add(popularityService.RecordAccessAsync($"mb:artist:{artistId}", cancellationToken));
            }
        }

        if (labelIds.Count != 0)
        {
            foreach (var labelId in labelIds)
            {
                tasks.Add(popularityService.RecordAccessAsync($"mb:label:{labelId}", cancellationToken));
            }
        }

        await Task.WhenAll(tasks);

        return Ok(new { accepted = true });
    }
}

public record WarmCacheHintsRequest(
    List<string>? MbReleaseIds = null,
    List<string>? MbArtistIds = null,
    List<string>? MbLabelIds = null);
