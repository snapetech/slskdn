// <copyright file="SolidClientIdDocumentService.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

/// <summary>
///     Service for generating Solid-OIDC Client ID documents.
/// </summary>
public sealed class SolidClientIdDocumentService : ISolidClientIdDocumentService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly IOptionsMonitor<slskd.Options> _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SolidClientIdDocumentService"/> class.
    /// </summary>
    public SolidClientIdDocumentService(IOptionsMonitor<slskd.Options> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public async Task WriteClientIdDocumentAsync(HttpContext http, CancellationToken ct)
    {
        // Derive base URL safely from the current request
        var req = http.Request;
        var baseUrl = $"{req.Scheme}://{req.Host}";

        var solid = _options.CurrentValue.Solid;
        var clientId = string.IsNullOrWhiteSpace(solid.ClientIdUrl)
            ? $"{baseUrl}/solid/clientid.jsonld"
            : solid.ClientIdUrl!;

        var redirectUri = $"{baseUrl}{solid.RedirectPath}";

        // Solid-OIDC requires JSON-LD with this context
        var doc = new
        {
            @context = "https://www.w3.org/ns/solid/oidc-context.jsonld",
            client_id = clientId,
            client_name = "slskdn",
            application_type = "web",
            redirect_uris = new[] { redirectUri },
            // keep scope minimal; expand later
            scope = "openid webid",
        };

        http.Response.ContentType = "application/ld+json";
        await http.Response.WriteAsync(JsonSerializer.Serialize(doc, JsonOpts), ct).ConfigureAwait(false);
    }
}
