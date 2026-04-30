// <copyright file="MusicBrainzOverlayController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;
using slskd.Integrations.MusicBrainz.Overlay;

[ApiController]
[Route("api/v{version:apiVersion}/musicbrainz/overlays")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class MusicBrainzOverlayController : ControllerBase
{
    private readonly IArtistReleaseGraphService _releaseGraphService;
    private readonly IMusicBrainzOverlayService _overlayService;

    public MusicBrainzOverlayController(
        IArtistReleaseGraphService releaseGraphService,
        IMusicBrainzOverlayService overlayService)
    {
        _releaseGraphService = releaseGraphService;
        _overlayService = overlayService;
    }

    [HttpPost("edits")]
    [ProducesResponseType(typeof(MusicBrainzOverlayValidationResult), 200)]
    public async Task<IActionResult> StoreEdit(
        [FromBody] MusicBrainzOverlayEdit edit,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var result = await _overlayService.StoreAsync(edit, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("edits/{editId}/export-review")]
    [ProducesResponseType(typeof(MusicBrainzOverlayExportReview), 200)]
    public async Task<IActionResult> GetExportReview(string editId, CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var review = await _overlayService.GetExportReviewAsync(editId, cancellationToken).ConfigureAwait(false);
        return review == null ? NotFound() : Ok(review);
    }

    [HttpPost("edits/{editId}/approve-export")]
    [ProducesResponseType(typeof(MusicBrainzOverlayExportApprovalResult), 200)]
    public async Task<IActionResult> ApproveExport(
        string editId,
        [FromBody] MusicBrainzOverlayExportApprovalRequest approvalRequest,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var result = await _overlayService.ApproveExportAsync(
            editId,
            approvalRequest ?? new MusicBrainzOverlayExportApprovalRequest(),
            cancellationToken).ConfigureAwait(false);
        if (result.Errors.Contains("Edit not found."))
        {
            return NotFound(result);
        }

        return result.IsApproved ? Ok(result) : BadRequest(result);
    }

    [HttpGet("artist/{artistId}/release-graph")]
    [ProducesResponseType(typeof(MusicBrainzOverlayReleaseGraphResponse), 200)]
    public async Task<IActionResult> GetEffectiveReleaseGraph(
        string artistId,
        [FromQuery] bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        artistId = artistId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(artistId))
        {
            return BadRequest("artistId is required");
        }

        var graph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, forceRefresh, cancellationToken).ConfigureAwait(false);
        if (graph == null)
        {
            return NotFound();
        }

        var application = await _overlayService.ApplyToArtistReleaseGraphAsync(graph, cancellationToken).ConfigureAwait(false);
        return Ok(new MusicBrainzOverlayReleaseGraphResponse
        {
            Original = application.Original,
            Effective = application.Effective,
            Provenance = application.Provenance,
        });
    }
}
