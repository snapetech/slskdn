namespace slskd.API.VirtualSoulfind;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides disaster mode status API.
/// </summary>
[ApiController]
[Route("api/virtualsoulfind/disaster-mode")]
[Produces("application/json")]
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

        // TODO: Integrate with actual disaster mode service when available
        // For now, return stub response
        return Ok(new
        {
            is_active = false,
            activated_at = (DateTimeOffset?)null,
            reason = (string?)null
        });
    }
}















