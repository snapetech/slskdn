// <copyright file="SecurityRoutesIntegrationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Integration.Security;

using System.Net;
using slskd.Tests.Integration;
using Xunit;

public class SecurityRoutesIntegrationTests : IClassFixture<StubWebApplicationFactory>
{
    private readonly StubWebApplicationFactory _factory;

    public SecurityRoutesIntegrationTests(StubWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SecurityDashboard_VersionedRoute_ShouldSucceed()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v0/security/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
