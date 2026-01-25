// <copyright file="HubEnforceAuthTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-02: When EnforceSecurity, anonymous connect to SearchHub and RelayHub (negotiate) → 401.
/// </summary>
public class HubEnforceAuthTests
{
    [Fact]
    public async Task Anonymous_negotiate_SearchHub_returns_401_when_EnforceSecurity()
    {
        using var factory = new HubEnforceAuthTestHostFactory();
        using var client = factory.CreateClient();

        // SignalR negotiate: POST to hub URL; auth runs before hub, unauthenticated → 401
        using var response = await client.PostAsync(
            "/hub/search",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_negotiate_RelayHub_returns_401_when_EnforceSecurity()
    {
        using var factory = new HubEnforceAuthTestHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync(
            "/hub/relay",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
