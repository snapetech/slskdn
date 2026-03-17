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

        var graph = await _discoveryGraphService.BuildAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(graph);
    }
}
