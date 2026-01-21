// <copyright file="UsersCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides slskd-compatible users API.
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class UsersCompatibilityController : ControllerBase
{
    private readonly ILogger<UsersCompatibilityController> logger;

    public UsersCompatibilityController(ILogger<UsersCompatibilityController> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Browse user files (slskd compatibility).
    /// </summary>
    [HttpGet("{username}/browse")]
    [Authorize]
    public async Task<IActionResult> BrowseUser(
        string username,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Browse user requested: {Username}", username);

        // Return stub browse results
        await Task.CompletedTask;
        return Ok(new
        {
            username = username,
            files = new List<object>()
        });
    }
}
