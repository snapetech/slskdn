// <copyright file="ShadowIndexController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using slskd.Core.Security;
using slskd.VirtualSoulfind.ShadowIndex;

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
    private readonly slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery shadowIndexQuery;

    public ShadowIndexController(
        ILogger<ShadowIndexController> logger,
        slskd.VirtualSoulfind.ShadowIndex.IShadowIndexQuery shadowIndexQuery)
    {
        this.logger = logger;
        this.shadowIndexQuery = shadowIndexQuery;
    }

    /// <summary>
    /// Get shadow index entries for a MusicBrainz recording ID.
    /// </summary>
    [HttpGet("{mbid}")]
    [Authorize]
    public async Task<IActionResult> GetShadowIndex(string mbid, CancellationToken ct)
    {
        mbid = mbid?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(mbid))
        {
            return BadRequest(new { error = "MBID is required" });
        }

        logger.LogDebug("Shadow index requested for MBID: {Mbid}", mbid);

        try
        {
            var result = await shadowIndexQuery.QueryAsync(mbid, ct);

            if (result == null)
            {
                return Ok(new { variants = new List<object>() });
            }

            // Convert to API-friendly format
            var variants = result.CanonicalVariants.Select(v => new
            {
                codec = v.Codec,
                bitrate = v.BitrateKbps,
                fileSize = v.SizeBytes,
                qualityScore = v.QualityScore
            }).ToList();

            return Ok(new { variants });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to query shadow index for MBID: {Mbid}", mbid);
            return StatusCode(500, new { error = "Failed to query shadow index" });
        }
    }
}
