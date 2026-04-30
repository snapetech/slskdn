// <copyright file="ExternalVisualizerController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Player.API;

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Core.Security;

[ApiController]
[ApiVersion("0")]
[Authorize(Policy = AuthPolicy.Any)]
[Route("api/v{version:apiVersion}/player/external-visualizer")]
[ValidateCsrfForCookiesOnly]
public sealed class ExternalVisualizerController : ControllerBase
{
    private readonly IExternalVisualizerLauncher _launcher;

    public ExternalVisualizerController(IExternalVisualizerLauncher launcher)
    {
        _launcher = launcher;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ExternalVisualizerStatus), 200)]
    public IActionResult Get()
    {
        return Ok(_launcher.GetStatus());
    }

    [HttpPost("launch")]
    [ProducesResponseType(typeof(ExternalVisualizerLaunchResult), 200)]
    [ProducesResponseType(typeof(ExternalVisualizerLaunchResult), 400)]
    public IActionResult Launch()
    {
        var result = _launcher.Launch();
        return result.Started ? Ok(result) : BadRequest(result);
    }
}
