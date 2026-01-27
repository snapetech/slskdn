// <copyright file="AnalyticsControllerIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Integration.Transfers.MultiSource;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Integration tests for swarm analytics endpoints.
/// </summary>
public class AnalyticsControllerIntegrationTests : IClassFixture<slskd.Tests.Integration.StubWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AnalyticsControllerIntegrationTests(slskd.Tests.Integration.StubWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PerformanceEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/performance");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PeerRankingsEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/peers/rankings?limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PeerRankingsEndpoint_ReturnsBadRequest_ForInvalidLimit()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/peers/rankings?limit=0");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EfficiencyEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/efficiency");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TrendsEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/trends?timeWindowHours=24&dataPoints=24");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TrendsEndpoint_ReturnsBadRequest_ForInvalidTimeWindow()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/trends?timeWindowHours=0&dataPoints=24");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TrendsEndpoint_ReturnsBadRequest_ForInvalidDataPoints()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/trends?timeWindowHours=24&dataPoints=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RecommendationsEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v0/swarm/analytics/recommendations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
