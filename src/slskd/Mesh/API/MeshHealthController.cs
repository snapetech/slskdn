// <copyright file="MeshHealthController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Mesh.Health;

namespace slskd.Mesh.API;

[ApiController]
[Route("api/v0/mesh/health")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class MeshHealthController : ControllerBase
{
    private readonly IMeshHealthService health;

    public MeshHealthController(IMeshHealthService health)
    {
        this.health = health;
    }

    [HttpGet]
    [Authorize(Policy = AuthPolicy.Any)]
    public ActionResult<MeshHealthSnapshot> Get()
    {
        var snap = health.GetSnapshot();
        return Ok(snap);
    }
}
