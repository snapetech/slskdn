// <copyright file="DisasterModeController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.API.VirtualSoulfind;

using slskd.Core.Security;
using slskd.VirtualSoulfind.DisasterMode;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides legacy fallback status for the dual-network search/runtime path.
/// </summary>
[ApiController]
[Route("api/virtualsoulfind/disaster-mode")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class DisasterModeController : ControllerBase
{
    private readonly ILogger<DisasterModeController> logger;
    private readonly slskd.VirtualSoulfind.DisasterMode.IDisasterModeCoordinator disasterModeCoordinator;

    public DisasterModeController(
        ILogger<DisasterModeController> logger,
        slskd.VirtualSoulfind.DisasterMode.IDisasterModeCoordinator disasterModeCoordinator)
    {
        this.logger = logger;
        this.disasterModeCoordinator = disasterModeCoordinator;
    }

    /// <summary>
    /// Get legacy fallback status.
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetStatus()
    {
        logger.LogDebug("Legacy fallback status requested");

        var level = disasterModeCoordinator.CurrentLevel;
        var levelDescription = level switch
        {
            DisasterModeLevel.Normal => "Normal default mode: Soulseek + mesh operating together",
            DisasterModeLevel.SoulseekDegraded => "Legacy fallback assisting: Soulseek degraded, mesh assisting",
            DisasterModeLevel.SoulseekUnavailable => "Legacy fallback active: Soulseek unavailable, mesh primary",
            DisasterModeLevel.FullFallback => "Legacy full fallback: shadow-index, relay, swarm-only",
            _ => "Unknown level"
        };

        return Ok(new
        {
            level = (int)level,
            level_name = level.ToString(),
            description = levelDescription,
            is_active = disasterModeCoordinator.IsDisasterModeActive,
            mode_family = "legacy_fallback",
            networks = new
            {
                soulseek_available = level <= DisasterModeLevel.SoulseekDegraded,
                mesh_assisting = level >= DisasterModeLevel.SoulseekDegraded,
                mesh_primary = level >= DisasterModeLevel.SoulseekUnavailable,
                full_fallback = level >= DisasterModeLevel.FullFallback
            }
        });
    }
}
