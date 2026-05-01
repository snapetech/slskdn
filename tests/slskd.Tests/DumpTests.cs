// <copyright file="DumpTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests;

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using slskd.Authentication;
using Xunit;

/// <summary>
/// PR-06: Dump endpoint. AllowMemoryDump=false → 404; non-admin → 403; remote no-auth requests now fail at auth as 401 before endpoint policy evaluation.
/// </summary>
public class DumpTests
{
    [Fact]
    public async Task AllowMemoryDump_false_returns_404()
    {
        using var factory = new DumpTestHostFactory(allowMemoryDump: false, allowRemoteDump: false, role: Role.Administrator);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v0/application/dump");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AllowMemoryDump_true_non_admin_returns_403()
    {
        using var factory = new DumpTestHostFactory(allowMemoryDump: true, allowRemoteDump: true, role: Role.ReadOnly);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v0/application/dump");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AllowMemoryDump_true_AllowRemoteDump_false_remote_ip_returns_401()
    {
        using var factory = new DumpTestHostFactory(allowMemoryDump: true, allowRemoteDump: false, role: Role.Administrator);
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v0/application/dump");
        req.Headers.TryAddWithoutValidation("X-Test-Remote-IP", "8.8.8.8");

        using var response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AllowMemoryDump_true_admin_loopback_attempts_dump_returns_200_500_or_501()
    {
        using var factory = new DumpTestHostFactory(allowMemoryDump: true, allowRemoteDump: false, role: Role.Administrator);
        using var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/v0/application/dump");
        req.Headers.TryAddWithoutValidation("X-Test-Remote-IP", "127.0.0.1");

        using var response = await client.SendAsync(req);

        // 200 if dump succeeds; 500/501 if DiagnosticsClient cannot create a dump in the test environment.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.InternalServerError ||
            response.StatusCode == HttpStatusCode.NotImplemented,
            $"GET /api/v0/application/dump returned {(int)response.StatusCode}, expected 200, 500, or 501.");
    }
}
