namespace slskd.API.Compatibility;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soulseek;

/// <summary>
/// Provides slskd API compatibility for external clients like Soulbeet.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class CompatibilityController : ControllerBase
{
    private readonly ISoulseekClient soulseek;
    private readonly ILogger<CompatibilityController> logger;

    public CompatibilityController(
        ISoulseekClient soulseek,
        ILogger<CompatibilityController> logger)
    {
        this.soulseek = soulseek;
        this.logger = logger;
    }

    /// <summary>
    /// Get server info and status (slskd compatibility).
    /// </summary>
    [HttpGet("info")]
    [Authorize]
    public IActionResult GetInfo()
    {
        logger.LogDebug("Compatibility info endpoint called");

        var version = Assembly.GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString() ?? "unknown";

        return Ok(new
        {
            impl = "slskdn",
            compat = "slskd",
            version,
            soulseek = new
            {
                connected = soulseek.State == SoulseekClientStates.Connected ||
                           soulseek.State == SoulseekClientStates.LoggedIn,
                user = soulseek.Username
            }
        });
    }
}

