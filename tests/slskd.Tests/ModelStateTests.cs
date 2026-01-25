// <copyright file="ModelStateTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// PR-07: ModelState. When EnforceSecurity and filter enabled, invalid login payload → 400 (ValidationProblemDetails).
/// </summary>
public class ModelStateTests
{
    [Fact]
    public async Task EnforceSecurity_true_invalid_login_payload_returns_400()
    {
        using var factory = new ModelStateTestHostFactory();
        using var client = factory.CreateClient();

        // {} misses [Required] Username and Password → ModelState invalid → 400
        using var response = await client.PostAsJsonAsync("/api/v0/session", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
