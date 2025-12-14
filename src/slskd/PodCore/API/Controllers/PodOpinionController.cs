// <copyright file="PodOpinionController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore.API.Controllers;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
///     Pod opinion management.
/// </summary>
[Route("api/v0/podcore/{podId}/opinions")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
public class PodOpinionController : ControllerBase
{
    private readonly IPodOpinionService _opinionService;
    private readonly IPodOpinionAggregator _opinionAggregator;
    private readonly ILogger<PodOpinionController> _logger;

    public PodOpinionController(
        IPodOpinionService opinionService,
        IPodOpinionAggregator opinionAggregator,
        ILogger<PodOpinionController> logger)
    {
        _opinionService = opinionService;
        _opinionAggregator = opinionAggregator;
        _logger = logger;
    }

    /// <summary>
    ///     Publishes an opinion on a content variant.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="opinion">The opinion to publish.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The publish result.</returns>
    /// <response code="200">The opinion was published.</response>
    /// <response code="400">The request is malformed or opinion is invalid.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost]
    [ProducesResponseType(typeof(OpinionPublishResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> PublishOpinion(
        [FromRoute] string podId,
        [FromBody] PodVariantOpinion opinion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (opinion == null)
        {
            return BadRequest("Opinion data is required");
        }

        try
        {
            var result = await _opinionService.PublishOpinionAsync(podId, opinion, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing opinion for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while publishing the opinion");
        }
    }

    /// <summary>
    ///     Gets all opinions for a content item in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of opinions.</returns>
    /// <response code="200">The opinions were retrieved.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("content/{contentId}")]
    [ProducesResponseType(typeof(IEnumerable<PodVariantOpinion>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetContentOpinions(
        [FromRoute] string podId,
        [FromRoute] string contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        try
        {
            var opinions = await _opinionService.GetOpinionsAsync(podId, contentId, cancellationToken);
            return Ok(opinions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting opinions for pod {PodId} content {ContentId}", podId, contentId);
            return StatusCode(500, "An error occurred while getting opinions");
        }
    }

    /// <summary>
    ///     Gets opinions for a specific content variant.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="variantHash">The variant hash.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The list of opinions for the variant.</returns>
    /// <response code="200">The opinions were retrieved.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("content/{contentId}/variant/{variantHash}")]
    [ProducesResponseType(typeof(IEnumerable<PodVariantOpinion>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetVariantOpinions(
        [FromRoute] string podId,
        [FromRoute] string contentId,
        [FromRoute] string variantHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        if (string.IsNullOrWhiteSpace(variantHash))
        {
            return BadRequest("Variant hash is required");
        }

        try
        {
            var opinions = await _opinionService.GetVariantOpinionsAsync(podId, contentId, variantHash, cancellationToken);
            return Ok(opinions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting variant opinions for pod {PodId} content {ContentId} variant {VariantHash}",
                podId, contentId, variantHash);
            return StatusCode(500, "An error occurred while getting variant opinions");
        }
    }

    /// <summary>
    ///     Gets aggregated opinion statistics for a content item.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The opinion statistics.</returns>
    /// <response code="200">The statistics were retrieved.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("content/{contentId}/stats")]
    [ProducesResponseType(typeof(OpinionStatistics), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetOpinionStatistics(
        [FromRoute] string podId,
        [FromRoute] string contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        try
        {
            var stats = await _opinionService.GetOpinionStatisticsAsync(podId, contentId, cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting opinion statistics for pod {PodId} content {ContentId}", podId, contentId);
            return StatusCode(500, "An error occurred while getting opinion statistics");
        }
    }

    /// <summary>
    ///     Refreshes opinions for a pod from the DHT.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The refresh result.</returns>
    /// <response code="200">The opinions were refreshed.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(OpinionRefreshResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RefreshOpinions(
        [FromRoute] string podId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        try
        {
            var result = await _opinionService.RefreshOpinionsAsync(podId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing opinions for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while refreshing opinions");
        }
    }

    /// <summary>
    ///     Gets aggregated opinions with affinity weighting and consensus metrics.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The aggregated opinions.</returns>
    /// <response code="200">The aggregated opinions were retrieved.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("content/{contentId}/aggregated")]
    [ProducesResponseType(typeof(AggregatedOpinions), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetAggregatedOpinions(
        [FromRoute] string podId,
        [FromRoute] string contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        try
        {
            var aggregated = await _opinionAggregator.GetAggregatedOpinionsAsync(podId, contentId, cancellationToken);
            return Ok(aggregated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting aggregated opinions for pod {PodId} content {ContentId}", podId, contentId);
            return StatusCode(500, "An error occurred while getting aggregated opinions");
        }
    }

    /// <summary>
    ///     Gets member affinity scores for a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The member affinity scores.</returns>
    /// <response code="200">The affinity scores were retrieved.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("members/affinity")]
    [ProducesResponseType(typeof(Dictionary<string, MemberAffinity>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetMemberAffinities(
        [FromRoute] string podId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        try
        {
            var affinities = await _opinionAggregator.GetMemberAffinitiesAsync(podId, cancellationToken);
            return Ok(affinities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting member affinities for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while getting member affinities");
        }
    }

    /// <summary>
    ///     Gets consensus recommendations for content variants.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="contentId">The content ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The consensus recommendations.</returns>
    /// <response code="200">The recommendations were retrieved.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpGet("content/{contentId}/recommendations")]
    [ProducesResponseType(typeof(IEnumerable<VariantRecommendation>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetConsensusRecommendations(
        [FromRoute] string podId,
        [FromRoute] string contentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        if (string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("Content ID is required");
        }

        try
        {
            var recommendations = await _opinionAggregator.GetConsensusRecommendationsAsync(podId, contentId, cancellationToken);
            return Ok(recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting consensus recommendations for pod {PodId} content {ContentId}", podId, contentId);
            return StatusCode(500, "An error occurred while getting consensus recommendations");
        }
    }

    /// <summary>
    ///     Updates member affinity scores based on recent activity.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The update result.</returns>
    /// <response code="200">The affinities were updated.</response>
    /// <response code="400">The request is malformed.</response>
    /// <response code="500">An unexpected error occurred.</response>
    [HttpPost("members/affinity/update")]
    [ProducesResponseType(typeof(AffinityUpdateResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> UpdateMemberAffinities(
        [FromRoute] string podId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(podId))
        {
            return BadRequest("Pod ID is required");
        }

        try
        {
            var result = await _opinionAggregator.UpdateMemberAffinitiesAsync(podId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating member affinities for pod {PodId}", podId);
            return StatusCode(500, "An error occurred while updating member affinities");
        }
    }
}
