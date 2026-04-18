// <copyright file="CapabilitiesController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Native;

using slskd.Core.Security;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd;
using OptionsModel = slskd.Options;

/// <summary>
/// Provides slskdn-native capabilities detection API.
/// </summary>
[ApiController]
[Route("api/slskdn")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class CapabilitiesController : ControllerBase
{
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private readonly ILogger<CapabilitiesController> logger;

    public CapabilitiesController(
        IOptionsMonitor<OptionsModel> optionsMonitor,
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

        var features = new List<string>
        {
            "mbid_jobs",
            "discography_jobs",
            "label_crate_jobs",
            "canonical_scoring",
            "rescue_mode",
            "library_health",
            "warm_cache",
            "job_manifests",
            "session_traces",
            "playback_aware"
        };

        if (optionsMonitor.CurrentValue.Feature.ScenePodBridge)
        {
            features.Add("scene_pod_bridge");
        }

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "unknown";

        return Ok(new
        {
            impl = "slskdn",
            compat = "slskd",
            version,
            features,
            feature = new
            {
                scenePodBridge = optionsMonitor.CurrentValue.Feature.ScenePodBridge
            }
        });
    }
}
