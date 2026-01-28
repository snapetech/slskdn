// <copyright file="SolidWebIdResolver.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Storage;

/// <summary>
///     Service for resolving WebID profiles and extracting OIDC issuers.
/// </summary>
public sealed class SolidWebIdResolver : ISolidWebIdResolver
{
    private readonly IHttpClientFactory _http;
    private readonly ISolidFetchPolicy _policy;
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<SolidWebIdResolver> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SolidWebIdResolver"/> class.
    /// </summary>
    public SolidWebIdResolver(
        IHttpClientFactory http,
        ISolidFetchPolicy policy,
        IOptionsMonitor<slskd.Options> options,
        ILogger<SolidWebIdResolver> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<SolidWebIdProfile> ResolveAsync(Uri webId, CancellationToken ct)
    {
        await _policy.ValidateAsync(webId, ct).ConfigureAwait(false);

        var opts = _options.CurrentValue.Solid;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds)));

        var client = _http.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, webId);
        req.Headers.Accept.ParseAdd("text/turtle, application/ld+json;q=0.9, application/rdf+xml;q=0.8");

        using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        await using var s = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        using var limited = new MemoryStream();
        await CopyWithLimitAsync(s, limited, opts.MaxFetchBytes, cts.Token).ConfigureAwait(false);
        limited.Position = 0;

        var g = new Graph();
        // dotNetRDF: use RdfReader for auto-detection, or parse based on content-type
        var ctType = resp.Content.Headers.ContentType?.MediaType ?? "";
        using var reader = new StreamReader(limited);
        
        if (ctType.Contains("turtle", StringComparison.OrdinalIgnoreCase) || ctType.Contains("text/turtle", StringComparison.OrdinalIgnoreCase))
        {
            var parser = new TurtleParser();
            parser.Load(g, reader);
        }
        else if (ctType.Contains("json", StringComparison.OrdinalIgnoreCase) || ctType.Contains("ld+json", StringComparison.OrdinalIgnoreCase))
        {
            // JsonLdParser uses Load(ITripleStore, string) - read content as string
            limited.Position = 0;
            reader.DiscardBufferedData();
            var content = await reader.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            var store = new TripleStore();
            var jsonParser = new JsonLdParser();
            jsonParser.Load(store, content);
            // Copy triples from store to graph
            foreach (var graph in store.Graphs)
            {
                g.Merge(graph);
            }
        }
        else
        {
            // Try Turtle as fallback
            limited.Position = 0;
            reader.DiscardBufferedData();
            var parser = new TurtleParser();
            parser.Load(g, reader);
        }

        var solid = "http://www.w3.org/ns/solid/terms#";
        var oidcIssuer = g.CreateUriNode(UriFactory.Create(solid + "oidcIssuer"));

        var issuers = new List<Uri>();
        var webIdNode = g.CreateUriNode(webId);
        foreach (var t in g.GetTriplesWithSubjectPredicate(webIdNode, oidcIssuer))
        {
            if (t.Object is IUriNode u)
            {
                issuers.Add(u.Uri);
            }
        }

        return new SolidWebIdProfile(webId, issuers.ToArray());
    }

    private static async Task CopyWithLimitAsync(Stream src, Stream dst, int maxBytes, CancellationToken ct)
    {
        var buf = new byte[8192];
        var total = 0;
        while (true)
        {
            var n = await src.ReadAsync(buf.AsMemory(0, buf.Length), ct).ConfigureAwait(false);
            if (n == 0) break;
            total += n;
            if (total > maxBytes) throw new InvalidOperationException("Solid fetch blocked: response too large.");
            await dst.WriteAsync(buf.AsMemory(0, n), ct).ConfigureAwait(false);
        }
    }
}
