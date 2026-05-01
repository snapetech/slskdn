// <copyright file="TasteRecommendationsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;
using slskd.SocialFederation;

[ApiController]
[Route("api/v{version:apiVersion}/taste-recommendations")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class TasteRecommendationsController : ControllerBase
{
    private readonly ITasteRecommendationService _recommendationService;

    public TasteRecommendationsController(ITasteRecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TasteRecommendationResult), 200)]
    public async Task<IActionResult> GetRecommendations(
        [FromBody] TasteRecommendationRequest? request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var result = await _recommendationService
            .GetRecommendationsAsync(request ?? new TasteRecommendationRequest(), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("wishlist")]
    [ProducesResponseType(typeof(TasteRecommendationWishlistPromotionResult), 200)]
    public async Task<IActionResult> PromoteToWishlist(
        [FromBody] TasteRecommendationWishlistPromotionRequest? request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest("promotion request is required");
        }

        var result = await _recommendationService.PromoteToWishlistAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Created && result.WishlistItemId == null)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("release-radar")]
    [ProducesResponseType(typeof(TasteRecommendationRadarSubscriptionResult), 200)]
    public async Task<IActionResult> SubscribeReleaseRadar(
        [FromBody] TasteRecommendationRadarSubscriptionRequest? request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest("radar subscription request is required");
        }

        var result = await _recommendationService.SubscribeArtistRadarAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Created)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("graph-preview")]
    [ProducesResponseType(typeof(TasteRecommendationGraphPreviewResult), 200)]
    public async Task<IActionResult> PreviewDiscoveryGraph(
        [FromBody] TasteRecommendationGraphPreviewRequest? request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest("graph preview request is required");
        }

        var result = await _recommendationService.PreviewDiscoveryGraphAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Available)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
