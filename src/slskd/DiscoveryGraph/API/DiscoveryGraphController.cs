// <copyright file="DiscoveryGraphController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.DiscoveryGraph.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;

[ApiController]
[Route("api/v{version:apiVersion}/discovery-graph")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class DiscoveryGraphController : ControllerBase
{
    private readonly IDiscoveryGraphService _discoveryGraphService;

    public DiscoveryGraphController(IDiscoveryGraphService discoveryGraphService)
    {
        _discoveryGraphService = discoveryGraphService;
    }

    [HttpPost]
    public async Task<IActionResult> Build([FromBody] DiscoveryGraphRequest request, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest("Discovery Graph request is required.");
        }

        request.Scope = string.IsNullOrWhiteSpace(request.Scope) ? "songid_run" : request.Scope.Trim();
        request.RecordingId = string.IsNullOrWhiteSpace(request.RecordingId) ? null : request.RecordingId.Trim();
        request.ReleaseId = string.IsNullOrWhiteSpace(request.ReleaseId) ? null : request.ReleaseId.Trim();
        request.ArtistId = string.IsNullOrWhiteSpace(request.ArtistId) ? null : request.ArtistId.Trim();
        request.Title = string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim();
        request.Artist = string.IsNullOrWhiteSpace(request.Artist) ? null : request.Artist.Trim();
        request.Album = string.IsNullOrWhiteSpace(request.Album) ? null : request.Album.Trim();
        request.CompareNodeId = string.IsNullOrWhiteSpace(request.CompareNodeId) ? null : request.CompareNodeId.Trim();
        request.CompareLabel = string.IsNullOrWhiteSpace(request.CompareLabel) ? null : request.CompareLabel.Trim();

        var graph = await _discoveryGraphService.BuildAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(graph);
    }
}
