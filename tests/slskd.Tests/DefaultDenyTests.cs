// <copyright file="DefaultDenyTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-02: Default-deny auth. Anonymous to protected → 401; [AllowAnonymous] routes → 200.
/// </summary>
public class DefaultDenyTests : IClassFixture<TestHostFactory>
{
    private readonly TestHostFactory _factory;

    public DefaultDenyTests(TestHostFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_GET_protected_route_returns_401()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v0/session");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AllowAnonymous_GET_session_enabled_returns_200_or_204()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v0/session/enabled");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK || (int)response.StatusCode == 204,
            $"GET /api/v0/session/enabled returned {(int)response.StatusCode}, expected 200 or 204.");
    }

    [Fact]
    public async Task AllowAnonymous_POST_session_login_with_valid_creds_returns_200()
    {
        using var client = _factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v0/session", new { username = "slskd", password = "slskd" });

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"POST /api/v0/session with valid creds returned {(int)response.StatusCode}, expected 200.");
    }
}
