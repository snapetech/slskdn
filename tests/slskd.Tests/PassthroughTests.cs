// <copyright file="PassthroughTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-03: No-auth (Passthrough) loopback vs remote. Loopback → authorized; remote → 401 (when AllowRemoteNoAuth=false).
/// </summary>
public class PassthroughTests : IClassFixture<NoAuthTestHostFactory>
{
    private readonly NoAuthTestHostFactory _factory;

    public PassthroughTests(NoAuthTestHostFactory factory) => _factory = factory;

    [Fact]
    public async Task NoAuth_loopback_authorized()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v0/session");
        req.Headers.TryAddWithoutValidation("X-Test-Remote-IP", "127.0.0.1");

        using var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NoAuth_remote_returns_401()
    {
        using var client = _factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v0/session");
        req.Headers.TryAddWithoutValidation("X-Test-Remote-IP", "192.168.1.1");

        using var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
