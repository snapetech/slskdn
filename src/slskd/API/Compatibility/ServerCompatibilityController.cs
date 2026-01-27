// <copyright file="ServerCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using slskd.Core.Security;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Soulseek;

/// <summary>
/// Provides slskd-compatible server status API.
/// </summary>
[ApiController]
[Route("api/server")]
[Produces("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class ServerCompatibilityController : ControllerBase
{
    private readonly ISoulseekClient soulseek;
    private readonly ILogger<ServerCompatibilityController> logger;

    public ServerCompatibilityController(
        ISoulseekClient soulseek,
        ILogger<ServerCompatibilityController> logger)
    {
        this.soulseek = soulseek;
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

        var isConnected = soulseek.State == SoulseekClientStates.Connected ||
                         soulseek.State == SoulseekClientStates.LoggedIn;
        var stateString = isConnected
            ? (soulseek.State.HasFlag(SoulseekClientStates.LoggedIn) ? "logged_in" : "connected")
            : "disconnected";

        return Ok(new
        {
            connected = isConnected,
            state = stateString,
            username = soulseek.Username ?? string.Empty
        });
    }
}
