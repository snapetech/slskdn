// <copyright file="LibraryBloomDiffController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;
using slskd.Integrations.MusicBrainz.Bloom;

[ApiController]
[Route("api/v{version:apiVersion}/musicbrainz/library-bloom")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class LibraryBloomDiffController : ControllerBase
{
    private readonly ILibraryBloomDiffService _libraryBloomDiffService;

    public LibraryBloomDiffController(ILibraryBloomDiffService libraryBloomDiffService)
    {
        _libraryBloomDiffService = libraryBloomDiffService;
    }

    [HttpPost("snapshots/preview")]
    [ProducesResponseType(typeof(LibraryBloomSnapshot), 200)]
    public async Task<IActionResult> PreviewSnapshot(
        [FromBody] LibraryBloomSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var snapshot = await _libraryBloomDiffService.CreateSnapshotAsync(
            request ?? new LibraryBloomSnapshotRequest(),
            cancellationToken).ConfigureAwait(false);
        return Ok(snapshot);
    }

    [HttpPost("diffs")]
    [ProducesResponseType(typeof(LibraryBloomDiffResult), 200)]
    public async Task<IActionResult> Compare(
        [FromBody] LibraryBloomDiffRequest request,
        CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest("diff request is required");
        }

        var result = await _libraryBloomDiffService.CompareAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsCompatible ? Ok(result) : BadRequest(result);
    }

    [HttpPost("wishlist")]
    [ProducesResponseType(typeof(LibraryBloomWishlistPromotionResult), 200)]
    public async Task<IActionResult> PromoteSuggestions(
        [FromBody] LibraryBloomWishlistPromotionRequest request,
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

        var result = await _libraryBloomDiffService.PromoteSuggestionsToWishlistAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }
}
