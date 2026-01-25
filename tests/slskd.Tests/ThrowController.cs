// <copyright file="ThrowController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd;

/// <summary>
/// Test-only controller that throws to exercise the exception handler. PR-05.
/// </summary>
[ApiController]
[Route("api/v0/throw")]
[AllowAnonymous]
public class ThrowController : ControllerBase
{
    /// <summary>
    /// GET /api/v0/throw — throws so exception handler can be asserted (no leak, traceId).
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        throw new InvalidOperationException("secret-internal-message");
    }

    /// <summary>
    /// GET /api/v0/throw/notimplemented — throws FeatureNotImplementedException for §11 (501 mapping).
    /// </summary>
    [HttpGet("notimplemented")]
    public IActionResult GetNotImplemented()
    {
        throw new FeatureNotImplementedException("Test feature is not implemented.");
    }
}
