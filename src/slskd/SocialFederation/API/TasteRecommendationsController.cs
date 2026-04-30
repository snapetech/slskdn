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
}
