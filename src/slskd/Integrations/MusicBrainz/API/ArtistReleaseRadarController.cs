// <copyright file="ArtistReleaseRadarController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;
using slskd.Integrations.MusicBrainz.Radar;

[ApiController]
[Route("api/v{version:apiVersion}/musicbrainz/release-radar")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class ArtistReleaseRadarController : ControllerBase
{
    private readonly IArtistReleaseRadarService _radarService;

    public ArtistReleaseRadarController(IArtistReleaseRadarService radarService)
    {
        _radarService = radarService;
    }

    [HttpPost("subscriptions")]
    [ProducesResponseType(typeof(ArtistRadarSubscription), 200)]
    public async Task<IActionResult> Subscribe(
        [FromBody] ArtistRadarSubscription subscription,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (subscription == null || string.IsNullOrWhiteSpace(subscription.ArtistId))
        {
            return BadRequest("artistId is required");
        }

        var result = await _radarService.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(IReadOnlyList<ArtistRadarSubscription>), 200)]
    public async Task<IActionResult> GetSubscriptions(CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var subscriptions = await _radarService.GetSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(subscriptions);
    }

    [HttpPost("observations")]
    [ProducesResponseType(typeof(ArtistRadarObservationResult), 200)]
    public async Task<IActionResult> RecordObservation(
        [FromBody] ArtistRadarObservation observation,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (observation == null)
        {
            return BadRequest("observation is required");
        }

        var result = await _radarService.RecordObservationAsync(observation, cancellationToken).ConfigureAwait(false);
        if (!result.Accepted)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("notifications")]
    [ProducesResponseType(typeof(IReadOnlyList<ArtistRadarNotification>), 200)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var notifications = await _radarService.GetNotificationsAsync(unreadOnly, cancellationToken).ConfigureAwait(false);
        return Ok(notifications);
    }
}
