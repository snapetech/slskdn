namespace slskd.API.VirtualSoulfind;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides shadow index API for cross-codec deduplication.
/// </summary>
[ApiController]
[Route("api/virtualsoulfind/shadow-index")]
[Produces("application/json")]
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

        // TODO: Integrate with actual shadow index service when available
        // For now, return stub response
        return Ok(new
        {
            mbid = mbid,
            variants = new List<object>()
        });
    }
}

