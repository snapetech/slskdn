// <copyright file="CorsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-04: CORS. Disabled = no CORS headers; enabled with allowlist = only allowed origin gets header.
/// </summary>
public class CorsTests
{
    [Fact]
    public async Task Cors_disabled_no_CORS_headers()
    {
        // TestHostFactory has no UseCors
        using var factory = new TestHostFactory();
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v0/session/enabled");
        req.Headers.TryAddWithoutValidation("Origin", "https://evil.example.com");

        using var response = await client.SendAsync(req);

        Assert.True(response.IsSuccessStatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Cors_enabled_allowed_origin_preflight_returns_allow_origin()
    {
        using var factory = new CorsTestHostFactory();
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/v0/session/enabled");
        req.Headers.TryAddWithoutValidation("Origin", CorsTestHostFactory.AllowedOrigin);
        req.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(req);

        Assert.True(response.StatusCode == HttpStatusCode.OK || (int)response.StatusCode == 204);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values));
        Assert.Contains(CorsTestHostFactory.AllowedOrigin, values);
    }

    [Fact]
    public async Task Cors_enabled_disallowed_origin_preflight_no_allow_origin()
    {
        using var factory = new CorsTestHostFactory();
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Options, "/api/v0/session/enabled");
        req.Headers.TryAddWithoutValidation("Origin", "https://disallowed.example.com");
        req.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "GET");

        using var response = await client.SendAsync(req);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }
}
