// <copyright file="MediaCoreStatsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore.API.Controllers;

using slskd.Core.Security;

/// <summary>
/// MediaCore statistics API controller.
/// </summary>
[Route("api/v0/mediacore/stats")]
[ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class MediaCoreStatsController : ControllerBase
{
    private readonly ILogger<MediaCoreStatsController> _logger;
    private readonly IMediaCoreStatsService _statsService;

    public MediaCoreStatsController(
        ILogger<MediaCoreStatsController> logger,
        IMediaCoreStatsService statsService)
    {
        _logger = logger;
        _statsService = statsService;
    }

    /// <summary>
    /// Gets the complete MediaCore statistics dashboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete statistics dashboard.</returns>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken = default)
    {
        try
        {
            var dashboard = await _statsService.GetDashboardAsync(cancellationToken);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting dashboard");
            return StatusCode(500, new { error = "Failed to get dashboard statistics" });
        }
    }

    /// <summary>
    /// Gets content registry statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content registry statistics.</returns>
    [HttpGet("registry")]
    public async Task<IActionResult> GetContentRegistryStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetContentRegistryStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting registry stats");
            return StatusCode(500, new { error = "Failed to get registry statistics" });
        }
    }

    /// <summary>
    /// Gets descriptor retrieval statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Descriptor statistics.</returns>
    [HttpGet("descriptors")]
    public async Task<IActionResult> GetDescriptorStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetDescriptorStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting descriptor stats");
            return StatusCode(500, new { error = "Failed to get descriptor statistics" });
        }
    }

    /// <summary>
    /// Gets fuzzy matching statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fuzzy matching statistics.</returns>
    [HttpGet("fuzzy")]
    public async Task<IActionResult> GetFuzzyMatchingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetFuzzyMatchingStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting fuzzy matching stats");
            return StatusCode(500, new { error = "Failed to get fuzzy matching statistics" });
        }
    }

    /// <summary>
    /// Gets IPLD mapping statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>IPLD mapping statistics.</returns>
    [HttpGet("ipld")]
    public async Task<IActionResult> GetIpldMappingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetIpldMappingStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting IPLD mapping stats");
            return StatusCode(500, new { error = "Failed to get IPLD mapping statistics" });
        }
    }

    /// <summary>
    /// Gets perceptual hashing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Perceptual hashing statistics.</returns>
    [HttpGet("perceptual")]
    public async Task<IActionResult> GetPerceptualHashingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetPerceptualHashingStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting perceptual hashing stats");
            return StatusCode(500, new { error = "Failed to get perceptual hashing statistics" });
        }
    }

    /// <summary>
    /// Gets metadata portability statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Metadata portability statistics.</returns>
    [HttpGet("portability")]
    public async Task<IActionResult> GetMetadataPortabilityStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetMetadataPortabilityStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting portability stats");
            return StatusCode(500, new { error = "Failed to get portability statistics" });
        }
    }

    /// <summary>
    /// Gets content publishing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Content publishing statistics.</returns>
    [HttpGet("publishing")]
    public async Task<IActionResult> GetContentPublishingStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _statsService.GetContentPublishingStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error getting publishing stats");
            return StatusCode(500, new { error = "Failed to get publishing statistics" });
        }
    }

    /// <summary>
    /// Resets all MediaCore statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reset operation result.</returns>
    [HttpPost("reset")]
    public async Task<IActionResult> ResetStats(CancellationToken cancellationToken = default)
    {
        try
        {
            await _statsService.ResetStatsAsync(cancellationToken);
            _logger.LogInformation("[MediaCoreStats] Statistics reset via API");
            return Ok(new { message = "Statistics reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MediaCoreStats] Error resetting stats");
            return StatusCode(500, new { error = "Failed to reset statistics" });
        }
    }
}

