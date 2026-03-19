// <copyright file="ShadowIndexController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides shadow index API for cross-codec deduplication.
/// </summary>
[ApiController]
[Route("api/virtualsoulfind/shadow-index")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class ShadowIndexController : ControllerBase
{
    private readonly ILogger<ShadowIndexController> logger;

    public ShadowIndexController(ILogger<ShadowIndexController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Get shadow index entries for a MusicBrainz recording ID.
    /// </summary>
    [HttpGet("{mbid}")]
    [Authorize]
    public IActionResult GetShadowIndex(string mbid)
    {
        logger.LogDebug("Shadow index requested for MBID: {Mbid}", mbid);

        // CRITICAL: Return 501 instead of fake data to prevent false confidence
        throw new Common.Exceptions.FeatureNotImplementedException(
            "Shadow index is not yet implemented. This feature tracks file variants across different codecs and formats for deduplication and quality comparison.");
    }
}
