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
    private readonly VirtualSoulfind.DisasterMode.IDisasterModeCoordinator disasterModeCoordinator;

    public DisasterModeController(
        ILogger<DisasterModeController> logger,
        VirtualSoulfind.DisasterMode.IDisasterModeCoordinator disasterModeCoordinator)
    {
        this.logger = logger;
        this.disasterModeCoordinator = disasterModeCoordinator;
    }

    /// <summary>
    /// Get disaster mode status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetStatus()
    {
        logger.LogDebug("Disaster mode status requested");

        return Ok(new
        {
            is_active = disasterModeCoordinator.IsDisasterModeActive,
            activated_at = (DateTimeOffset?)null, // TODO: Track activation timestamp
            reason = (string?)null // TODO: Track activation reason
        });
    }
}
