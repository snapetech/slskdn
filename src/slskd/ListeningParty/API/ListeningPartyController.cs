// <copyright file="ListeningPartyController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.ListeningParty.API;

using System.IO;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.Authentication;
using slskd.Core.Security;
using slskd.Sharing;
using slskd.Streaming;

/// <summary>
///     Pod listen-along controls.
/// </summary>
[Route("api/v{version:apiVersion}/listening-party")]
[ApiVersion("0")]
[ApiController]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class ListeningPartyController : ControllerBase
{
    private const int ListedPartyMaxConcurrentStreams = 50;

    private readonly IContentLocator _locator;
    private readonly IListeningPartyService _listeningParty;
    private readonly IStreamSessionLimiter _limiter;
    private readonly IOptionsMonitor<Options> _options;

    public ListeningPartyController(
        IContentLocator locator,
        IListeningPartyService listeningParty,
        IStreamSessionLimiter limiter,
        IOptionsMonitor<Options> options)
    {
        _locator = locator;
        _listeningParty = listeningParty;
        _limiter = limiter;
        _options = options;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ListeningPartyAnnouncement>), 200)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var parties = await _listeningParty.ListDirectoryAsync(cancellationToken);
        return Ok(parties);
    }

    [HttpGet("{podId}/{channelId}")]
    [ProducesResponseType(typeof(ListeningPartyEvent), 200)]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Get([FromRoute] string podId, [FromRoute] string channelId, CancellationToken cancellationToken)
    {
        var state = await _listeningParty.GetStateAsync(podId, channelId, cancellationToken);
        return state == null ? NoContent() : Ok(state);
    }

    [HttpPost("{podId}/{channelId}")]
    [ProducesResponseType(typeof(ListeningPartyEvent), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Publish(
        [FromRoute] string podId,
        [FromRoute] string channelId,
        [FromBody] ListeningPartyEvent request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return BadRequest("Listen-along event is required.");
        }

        try
        {
            var published = await _listeningParty.PublishAsync(
                request with
                {
                    PodId = podId,
                    ChannelId = channelId,
                },
                cancellationToken);

            return Ok(published);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("radio/{partyId}/{contentId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> StreamListedParty(
        [FromRoute] string partyId,
        [FromRoute] string contentId,
        CancellationToken cancellationToken)
    {
        partyId = partyId?.Trim() ?? string.Empty;
        contentId = contentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(partyId) || string.IsNullOrWhiteSpace(contentId))
        {
            return BadRequest("PartyId and ContentId are required.");
        }

        if (!_options.CurrentValue.Feature.Streaming)
        {
            return NotFound();
        }

        var rangeHeader = Request.Headers.Range.FirstOrDefault();
        if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.IndexOf(',') >= 0)
        {
            return BadRequest("Multiple byte ranges are not supported.");
        }

        var state = await _listeningParty.GetStateByPartyIdAsync(partyId, cancellationToken);
        if (state is not { Listed: true, AllowMeshStreaming: true } ||
            !string.Equals(state.ContentId, contentId, StringComparison.Ordinal))
        {
            return NotFound();
        }

        var resolved = _locator.Resolve(contentId, cancellationToken);
        if (resolved == null)
        {
            return NotFound();
        }

        var limiterKey = $"listening-party:{partyId}";
        if (!_limiter.TryAcquire(limiterKey, ListedPartyMaxConcurrentStreams))
        {
            return StatusCode(429, "Too many concurrent radio streams.");
        }

        Stream? stream = null;
        try
        {
#pragma warning disable CA2000 // Ownership is transferred to ReleaseOnDisposeStream/FileResult on success and disposed in finally on failure.
            stream = new FileStream(resolved.AbsolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Stream? ownedStream = stream;
            var wrapped = new ReleaseOnDisposeStream(ownedStream, () => _limiter.Release(limiterKey));
#pragma warning restore CA2000
            ownedStream = null;
            stream = null;
            return File(wrapped, resolved.ContentType, enableRangeProcessing: true);
        }
        catch (IOException)
        {
            _limiter.Release(limiterKey);
            return NotFound();
        }
        finally
        {
            stream?.Dispose();
        }
    }
}
