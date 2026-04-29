// <copyright file="SolidClientIdDocumentService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Solid;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Service for generating Solid-OIDC Client ID documents.
/// </summary>
public sealed class SolidClientIdDocumentService : ISolidClientIdDocumentService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<SolidClientIdDocumentService> _logger;
    private int _missingClientIdLogged;

    public SolidClientIdDocumentService(
        IOptionsMonitor<slskd.Options> options,
        ILogger<SolidClientIdDocumentService> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task WriteClientIdDocumentAsync(HttpContext http, CancellationToken ct)
    {
        var solid = _options.CurrentValue.Solid;

        // Solid-OIDC requires the client_id URL to be the canonical, dereferenceable URL the IdP will
        // fetch. Deriving it from Request.Host leaks whatever host the caller happened to reach us on
        // (LAN IP on a home deploy, internal hostname on a reverse-proxied deploy). Require it to be
        // configured explicitly — empty == feature off, matching Solid.AllowedHosts' fail-closed shape.
        if (string.IsNullOrWhiteSpace(solid.ClientIdUrl))
        {
            if (System.Threading.Interlocked.Exchange(ref _missingClientIdLogged, 1) == 0)
            {
                _logger.LogWarning(
                    "[Solid] /solid/clientid.jsonld request refused: solid.clientIdUrl is not configured. " +
                    "Set it to the public HTTPS URL at which this document is dereferenceable; " +
                    "until then, Solid client registration is disabled.");
            }

            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // Derive redirect_uri from the configured client_id so both strings share an origin, as required
        // by Solid-OIDC, and so we never emit the request host into the document.
        if (!Uri.TryCreate(solid.ClientIdUrl, UriKind.Absolute, out var clientIdUri))
        {
            _logger.LogError("[Solid] solid.clientIdUrl is not a valid absolute URL: {Value}", solid.ClientIdUrl);
            http.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        var redirectPath = string.IsNullOrWhiteSpace(solid.RedirectPath) ? "/solid/callback" : solid.RedirectPath;
        var redirectUri = new UriBuilder(clientIdUri) { Path = redirectPath, Query = string.Empty, Fragment = string.Empty }.Uri.ToString();

        var doc = new Dictionary<string, object?>
        {
            ["@context"] = "https://www.w3.org/ns/solid/oidc-context.jsonld",
            ["client_id"] = solid.ClientIdUrl,
            ["client_name"] = "slskdn",
            ["application_type"] = "web",
            ["redirect_uris"] = new[] { redirectUri },

            // keep scope minimal; expand later
            ["scope"] = "openid webid",
        };

        http.Response.ContentType = "application/ld+json";
        await http.Response.WriteAsync(JsonSerializer.Serialize(doc, JsonOpts), ct).ConfigureAwait(false);
    }
}
