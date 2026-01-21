// <copyright file="LibraryCompatibilityController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.API.Compatibility;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.LibraryHealth;

/// <summary>
/// Provides slskd-compatible library API.
/// </summary>
[ApiController]
[Route("api/library")]
[Produces("application/json")]
public class LibraryCompatibilityController : ControllerBase
{
    private readonly ILibraryHealthService healthService;
    private readonly ILogger<LibraryCompatibilityController> logger;

    public LibraryCompatibilityController(
        ILibraryHealthService healthService,
        ILogger<LibraryCompatibilityController> logger)
    {
        this.healthService = healthService;
        this.logger = logger;
    }

    /// <summary>
    /// Trigger a library health scan (slskd compatibility).
    /// </summary>
    [HttpPost("scan")]
    [Authorize]
    public async Task<IActionResult> StartScan(
        [FromBody] LibraryHealthScanRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Compatibility library scan requested");

        var scanId = await healthService.StartScanAsync(
            request ?? new LibraryHealthScanRequest { LibraryPath = null },
            cancellationToken);

        return Ok(new { scan_id = scanId });
    }
}
