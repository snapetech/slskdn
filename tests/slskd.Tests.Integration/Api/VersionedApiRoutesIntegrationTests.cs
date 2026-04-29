// <copyright file="VersionedApiRoutesIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Integration.Api;

using System.Net;
using slskd.Tests.Integration;
using Xunit;

public class VersionedApiRoutesIntegrationTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly StubWebApplicationFactory _factory;

    public VersionedApiRoutesIntegrationTests(StubWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task JobsList_VersionedRoute_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v0/jobs?limit=20&sortBy=created_at&sortOrder=desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MediaCoreContentIdStats_VersionedRoute_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v0/mediacore/contentid/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MediaCoreHashAlgorithms_VersionedRoute_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v0/mediacore/perceptualhash/algorithms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MediaCoreConflictStrategies_VersionedRoute_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v0/mediacore/portability/strategies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserNotesList_VersionedRoute_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v0/users/notes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
