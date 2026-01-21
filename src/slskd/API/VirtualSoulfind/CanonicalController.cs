// <copyright file="CanonicalController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides canonical variant selection API.
/// </summary>
[ApiController]
[Route("api/virtualsoulfind/canonical")]
[Produces("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class CanonicalController : ControllerBase
{
    private readonly ILogger<CanonicalController> logger;

    public CanonicalController(ILogger<CanonicalController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Get canonical variant for a MusicBrainz recording ID.
    /// </summary>
    [HttpGet("{mbid}")]
    [Authorize]
    public IActionResult GetCanonical(string mbid)
    {
        logger.LogDebug("Canonical variant requested for MBID: {Mbid}", mbid);

        // TODO: Integrate with actual canonical selection service when available
        // For now, return stub response
        return Ok(new
        {
            mbid = mbid,
            canonical_variant = new
            {
                codec = "FLAC",
                bitrate = 0,
                source = "test-peer"
            },
            variants = new List<object>()
        });
    }
}
