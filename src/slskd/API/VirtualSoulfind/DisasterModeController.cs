// <copyright file="DisasterModeController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.VirtualSoulfind;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides disaster mode status API.
/// </summary>
[ApiController]
[Route("api/virtualsoulfind/disaster-mode")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class DisasterModeController : ControllerBase
{
    private readonly ILogger<DisasterModeController> logger;

    public DisasterModeController(ILogger<DisasterModeController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Get disaster mode status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetStatus()
    {
        logger.LogDebug("Disaster mode status requested");

        // CRITICAL: Return 501 instead of fake data to prevent false confidence
        throw new Common.Exceptions.FeatureNotImplementedException(
            "Disaster mode status is not yet implemented. This feature provides fallback search capabilities when primary Soulseek network is degraded.");
    }
}
