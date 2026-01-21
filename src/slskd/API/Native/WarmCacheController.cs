// <copyright file="WarmCacheController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd;
using OptionsModel = slskd.Options;
using slskd.Transfers.MultiSource.Caching;

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

        logger.LogInformation("Received warm cache hints: {ReleaseCount} releases, {ArtistCount} artists, {LabelCount} labels",
            request.MbReleaseIds?.Count ?? 0,
            request.MbArtistIds?.Count ?? 0,
            request.MbLabelIds?.Count ?? 0);

        // Record popularity for each hinted item
        var tasks = new List<Task>();

        if (request.MbReleaseIds != null)
        {
            foreach (var releaseId in request.MbReleaseIds)
            {
                tasks.Add(popularityService.RecordAccessAsync($"mb:release:{releaseId}", cancellationToken));
            }
        }

        if (request.MbArtistIds != null)
        {
            foreach (var artistId in request.MbArtistIds)
            {
                tasks.Add(popularityService.RecordAccessAsync($"mb:artist:{artistId}", cancellationToken));
            }
        }

        if (request.MbLabelIds != null)
        {
            foreach (var labelId in request.MbLabelIds)
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
