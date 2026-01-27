// <copyright file="AnalyticsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.API;

using System;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;
using slskd.Transfers.MultiSource.Analytics;

/// <summary>
///     API for swarm analytics and reporting.
/// </summary>
[Route("api/v{version:apiVersion}/swarm/analytics")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public class AnalyticsController : ControllerBase
{
    private readonly ISwarmAnalyticsService _analyticsService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnalyticsController"/> class.
    /// </summary>
    public AnalyticsController(ISwarmAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    /// <summary>
    ///     Gets overall swarm performance metrics.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Performance metrics.</returns>
    [HttpGet("performance")]
    public async Task<IActionResult> GetPerformanceMetrics(
        [FromQuery] int? timeWindowHours = null,
        CancellationToken cancellationToken = default)
    {
        var timeWindow = timeWindowHours.HasValue
            ? TimeSpan.FromHours(timeWindowHours.Value)
            : (TimeSpan?)null;

        var metrics = await _analyticsService.GetPerformanceMetricsAsync(timeWindow, cancellationToken).ConfigureAwait(false);
        return Ok(metrics);
    }

    /// <summary>
    ///     Gets peer performance rankings.
    /// </summary>
    /// <param name="limit">Maximum number of peers to return (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Peer rankings.</returns>
    [HttpGet("peers/rankings")]
    public async Task<IActionResult> GetPeerRankings(
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 100)
        {
            return BadRequest("Limit must be between 1 and 100");
        }

        var rankings = await _analyticsService.GetPeerRankingsAsync(limit, cancellationToken).ConfigureAwait(false);
        return Ok(rankings);
    }

    /// <summary>
    ///     Gets swarm efficiency metrics.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Efficiency metrics.</returns>
    [HttpGet("efficiency")]
    public async Task<IActionResult> GetEfficiencyMetrics(
        [FromQuery] int? timeWindowHours = null,
        CancellationToken cancellationToken = default)
    {
        var timeWindow = timeWindowHours.HasValue
            ? TimeSpan.FromHours(timeWindowHours.Value)
            : (TimeSpan?)null;

        var metrics = await _analyticsService.GetEfficiencyMetricsAsync(timeWindow, cancellationToken).ConfigureAwait(false);
        return Ok(metrics);
    }

    /// <summary>
    ///     Gets historical trends for swarm metrics.
    /// </summary>
    /// <param name="timeWindowHours">Time window in hours (default: 24).</param>
    /// <param name="dataPoints">Number of data points to return (default: 24).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Trend data.</returns>
    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(
        [FromQuery] int timeWindowHours = 24,
        [FromQuery] int dataPoints = 24,
        CancellationToken cancellationToken = default)
    {
        if (timeWindowHours < 1 || timeWindowHours > 168)
        {
            return BadRequest("Time window must be between 1 and 168 hours (7 days)");
        }

        if (dataPoints < 2 || dataPoints > 168)
        {
            return BadRequest("Data points must be between 2 and 168");
        }

        var timeWindow = TimeSpan.FromHours(timeWindowHours);
        var trends = await _analyticsService.GetTrendsAsync(timeWindow, dataPoints, cancellationToken).ConfigureAwait(false);
        return Ok(trends);
    }

    /// <summary>
    ///     Gets recommendations for optimizing swarm performance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recommendations.</returns>
    [HttpGet("recommendations")]
    public async Task<IActionResult> GetRecommendations(CancellationToken cancellationToken = default)
    {
        var recommendations = await _analyticsService.GetRecommendationsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(recommendations);
    }
}
