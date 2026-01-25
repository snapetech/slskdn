// <copyright file="ApiSmokeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Smoke tests for the API. GET /api/v0/session/enabled must return 200 or 204 (current behavior).
/// </summary>
public class ApiSmokeTests : IClassFixture<TestHostFactory>
{
    private readonly TestHostFactory _factory;

    public ApiSmokeTests(TestHostFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Session_Enabled_Returns_200_Or_204()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/v0/session/enabled");

        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == (HttpStatusCode)204,
            $"GET /api/v0/session/enabled returned {(int)response.StatusCode} {response.StatusCode}, expected 200 or 204.");
    }
}
