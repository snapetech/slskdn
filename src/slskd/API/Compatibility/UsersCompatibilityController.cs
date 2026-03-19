// <copyright file="UsersCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using slskd.Core.Security;
using Soulseek;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Provides user browsing compatibility API.
/// </summary>
[ApiController]
[Route("api/compatibility/users")]
[Produces("application/json")]
[ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class UsersCompatibilityController : ControllerBase
{
    private readonly ILogger<UsersCompatibilityController> logger;
    private readonly ISoulseekClient soulseekClient;

    public UsersCompatibilityController(
        ILogger<UsersCompatibilityController> logger,
        ISoulseekClient soulseekClient)
    {
        this.logger = logger;
        this.soulseekClient = soulseekClient;
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

        try
        {
            var browseResult = await soulseekClient.BrowseAsync(username, cancellationToken);

            // Convert Soulseek browse result to compatibility format
            var directories = browseResult.Directories.Select(dir => new
            {
                name = dir.Name,
                files = dir.Files.Select(file => new
                {
                    filename = file.Filename,
                    size = file.Size,
                    attributes = new[] { file.Extension },
                    bitrate = file.Bitrate,
                    duration = file.Length,
                    sampleRate = file.SampleRate,
                    bitDepth = file.BitDepth,
                    codec = file.Codec
                }).ToList()
            }).ToList();

            return Ok(new
            {
                username = username,
                directories = directories
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to browse user {Username}", username);
            return StatusCode(500, new { error = "Failed to browse user", username, details = ex.Message });
        }
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

        // CRITICAL: Return 501 instead of fake data to prevent false confidence
        throw new Common.Exceptions.FeatureNotImplementedException(
            "User browsing compatibility is not yet implemented. This provides backward compatibility for older Soulseek client protocols.");
    }
}
