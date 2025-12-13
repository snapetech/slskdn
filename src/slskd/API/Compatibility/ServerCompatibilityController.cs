namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides slskd-compatible server status API.
/// </summary>
[ApiController]
[Route("api/server")]
[Produces("application/json")]
public class ServerCompatibilityController : ControllerBase
{
    private readonly ILogger<ServerCompatibilityController> logger;

    public ServerCompatibilityController(ILogger<ServerCompatibilityController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Get server connection status (slskd compatibility).
    /// </summary>
    [HttpGet("status")]
    [Authorize]
    public IActionResult GetStatus()
    {
        logger.LogDebug("Server status requested");

        // Return stub status - connected by default in test
        return Ok(new
        {
            connected = true,
            state = "logged_in",
            username = "test-user"
        });
    }
}















