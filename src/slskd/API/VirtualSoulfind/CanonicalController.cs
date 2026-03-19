// <copyright file="CanonicalController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using slskd.Core.Security;

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

        // CRITICAL: Return 501 instead of fake data to prevent false confidence
        throw new Common.Exceptions.FeatureNotImplementedException(
            "Canonical variant selection is not yet implemented. This feature will analyze available file variants and select the highest quality canonical version.");
    }
}
