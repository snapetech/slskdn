// <copyright file="RateLimitingTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-09: HTTP rate limiting — burst over limit → 429; normal rate does not.
/// </summary>
public class RateLimitingTests
{
    [Fact]
    public async Task Burst_over_ApiPermitLimit_returns_429_on_excess_requests()
    {
        using var factory = new RateLimitingTestHostFactory();
        using var client = factory.CreateClient();

        // ApiPermitLimit=2: first two succeed, third is rejected
        using var r1 = await client.GetAsync("/api/v0/session/enabled");
        using var r2 = await client.GetAsync("/api/v0/session/enabled");
        using var r3 = await client.GetAsync("/api/v0/session/enabled");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal((HttpStatusCode)429, r3.StatusCode);
    }

    [Fact]
    public async Task Requests_within_ApiPermitLimit_all_return_200()
    {
        using var factory = new RateLimitingTestHostFactory();
        using var client = factory.CreateClient();

        using var r1 = await client.GetAsync("/api/v0/session/enabled");
        using var r2 = await client.GetAsync("/api/v0/session/enabled");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    [Fact]
    public async Task Burst_federation_inbox_over_FederationPermitLimit_returns_429()
    {
        using var factory = new FedMeshRateLimitTestHostFactory();
        using var client = factory.CreateClient();

        // FederationPermitLimit=2: path contains /inbox and POST
        using var r1 = await client.PostAsync("/api/v0/actors/x/inbox", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        using var r2 = await client.PostAsync("/api/v0/actors/x/inbox", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        using var r3 = await client.PostAsync("/api/v0/actors/x/inbox", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal((HttpStatusCode)429, r3.StatusCode);
    }

    [Fact]
    public async Task Burst_mesh_gateway_over_MeshGatewayPermitLimit_returns_429()
    {
        using var factory = new FedMeshRateLimitTestHostFactory();
        using var client = factory.CreateClient();

        // MeshGatewayPermitLimit=2: path starts with /mesh/
        using var r1 = await client.GetAsync("/mesh/ok");
        using var r2 = await client.GetAsync("/mesh/ok");
        using var r3 = await client.GetAsync("/mesh/ok");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal((HttpStatusCode)429, r3.StatusCode);
    }
}
