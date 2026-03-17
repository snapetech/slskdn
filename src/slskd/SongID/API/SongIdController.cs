// <copyright file="SongIdController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SongID.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Authentication;
using slskd.Core.Security;

[ApiController]
[Route("api/v{version:apiVersion}/songid")]
[ApiVersion("0")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class SongIdController : ControllerBase
{
    private readonly ISongIdService _songIdService;

    public SongIdController(ISongIdService songIdService)
    {
        _songIdService = songIdService;
    }

    [HttpPost("runs")]
    public async Task<IActionResult> CreateRun([FromBody] SongIdRunRequest request, CancellationToken cancellationToken)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest("SongID source is required.");
        }

        var run = await _songIdService.QueueAnalyzeAsync(request.Source, cancellationToken).ConfigureAwait(false);
        return Accepted(run);
    }

    [HttpGet("runs")]
    public IActionResult ListRuns([FromQuery] int limit = 10)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        return Ok(_songIdService.List(limit));
    }

    [HttpGet("runs/{id:guid}")]
    public IActionResult GetRun(Guid id)
    {
        if (Program.IsRelayAgent)
        {
            return Forbid();
        }

        var run = _songIdService.Get(id);
        return run == null ? NotFound() : Ok(run);
    }
}

public sealed class SongIdRunRequest
{
    public string Source { get; set; } = string.Empty;
}
