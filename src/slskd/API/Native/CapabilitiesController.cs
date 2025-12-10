namespace slskd.API.Native;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

/// <summary>
/// Provides slskdn-native capabilities detection API.
/// </summary>
[ApiController]
[Route("api/slskdn")]
[Produces("application/json")]
public class CapabilitiesController : ControllerBase
{
    private readonly IOptionsMonitor<Options> optionsMonitor;
    private readonly ILogger<CapabilitiesController> logger;

    public CapabilitiesController(
        IOptionsMonitor<Options> optionsMonitor,
        ILogger<CapabilitiesController> logger)
    {
        this.optionsMonitor = optionsMonitor;
        this.logger = logger;
    }

    /// <summary>
    /// Get slskdn capabilities and feature flags.
    /// </summary>
    [HttpGet("capabilities")]
    [Authorize]
    public IActionResult GetCapabilities()
    {
        logger.LogDebug("Capabilities endpoint called");

        var options = optionsMonitor.CurrentValue;
        var features = new List<string> { "mbid_jobs" };

        // Add feature flags based on configuration
        if (options.Integrations?.MusicBrainz?.Enabled == true)
        {
            features.Add("discography_jobs");
            features.Add("label_crate_jobs");
        }

        if (options.Audio?.CanonicalScoring?.Enabled == true)
        {
            features.Add("canonical_scoring");
        }

        if (options.Transfers?.RescueMode?.Enabled == true)
        {
            features.Add("rescue_mode");
        }

        if (options.LibraryHealth?.Enabled == true)
        {
            features.Add("library_health");
        }

        features.Add("warm_cache");
        features.Add("job_manifests");
        features.Add("session_traces");
        features.Add("playback_aware");

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "unknown";

        return Ok(new
        {
            impl = "slskdn",
            version,
            features
        });
    }
}
