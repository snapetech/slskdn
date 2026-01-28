// <copyright file="SolidController.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid.API;

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using slskd.Core.Security;

/// <summary>
///     Solid API controller (WebID resolution, Client ID document).
/// </summary>
[ApiController]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}/solid")]
[Produces("application/json")]
[Consumes("application/json")]
[Authorize(Policy = AuthPolicy.Any)]
[ValidateCsrfForCookiesOnly]
public sealed class SolidController : ControllerBase
{
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly slskd.Solid.ISolidWebIdResolver _resolver;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SolidController"/> class.
    /// </summary>
    public SolidController(IOptionsMonitor<slskd.Options> options, slskd.Solid.ISolidWebIdResolver resolver)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    private bool Enabled => _options.CurrentValue.Feature.Solid;

    /// <summary>
    ///     Gets Solid integration status.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public IActionResult Status()
    {
        if (!Enabled) return NotFound();
        return Ok(new
        {
            enabled = true,
            clientId = _options.CurrentValue.Solid.ClientIdUrl ?? "/solid/clientid.jsonld",
            redirectPath = _options.CurrentValue.Solid.RedirectPath
        });
    }

    /// <summary>
    ///     Request body for resolving a WebID.
    /// </summary>
    public sealed class ResolveWebIdRequest
    {
        /// <summary>
        ///     The WebID URI to resolve.
        /// </summary>
        [Required]
        public string WebId { get; set; } = "";
    }

    /// <summary>
    ///     Resolves a WebID and extracts OIDC issuer information.
    /// </summary>
    [HttpPost("resolve-webid")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ResolveWebId([FromBody] ResolveWebIdRequest req, CancellationToken ct)
    {
        if (!Enabled) return NotFound();

        if (!Uri.TryCreate(req.WebId, UriKind.Absolute, out var webId))
        {
            return Problem(title: "Invalid WebID", detail: "WebId must be an absolute URI.", statusCode: 400);
        }

        try
        {
            var p = await _resolver.ResolveAsync(webId, ct).ConfigureAwait(false);
            return Ok(new { webId = p.WebId.ToString(), oidcIssuers = Array.ConvertAll(p.OidcIssuers, u => u.ToString()) });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Solid fetch blocked", detail: ex.Message, statusCode: 400);
        }
        catch (Exception ex)
        {
            return Problem(title: "Failed to resolve WebID", detail: ex.Message, statusCode: 500);
        }
    }
}
