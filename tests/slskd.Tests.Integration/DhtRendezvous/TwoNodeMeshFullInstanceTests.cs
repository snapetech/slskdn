// <copyright file="TwoNodeMeshFullInstanceTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Integration.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Common.Security.API;
using slskd.DhtRendezvous.API;
using slskd.DhtRendezvous.Security;
using slskd.Mesh;
using slskd.Tests.Integration.Harness;
using Xunit;

[Trait("Category", "L2-Integration")]
[Trait("Category", "DhtRendezvous")]
[Trait("Category", "FullInstance")]
public class TwoNodeMeshFullInstanceTests
{
    [Fact]
    public async Task TwoFullInstances_CanFormOverlayMeshConnection()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        await using var alpha = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-alpha-{Guid.NewGuid():N}"[..19]);
        await using var beta = new SlskdnFullInstanceRunner(
            loggerFactory.CreateLogger<SlskdnFullInstanceRunner>(),
            $"mesh-beta-{Guid.NewGuid():N}"[..18]);

        await alpha.StartAsync(
            disableAuthentication: true,
            soulseekUsername: "mesh-alpha",
            soulseekPassword: "mesh-alpha-pass");
        await beta.StartAsync(
            disableAuthentication: true,
            soulseekUsername: "mesh-beta",
            soulseekPassword: "mesh-beta-pass");

        Assert.True(alpha.OverlayPort.HasValue);
        Assert.True(beta.OverlayPort.HasValue);

        using var alphaClient = new HttpClient { BaseAddress = new Uri(alpha.ApiUrl) };
        using var betaClient = new HttpClient { BaseAddress = new Uri(beta.ApiUrl) };

        var connectResponse = await alphaClient.PostAsJsonAsync(
            "/api/v0/overlay/connect",
            new ConnectOverlayPeerRequest
            {
                Address = "127.0.0.1",
                Port = beta.OverlayPort.Value,
            });

        connectResponse.EnsureSuccessStatusCode();
        var connectBody = await connectResponse.Content.ReadFromJsonAsync<OverlayConnectResultResponse>();
        Assert.NotNull(connectBody);
        Assert.True(connectBody!.Connected);
        Assert.Equal(beta.OverlayPort.Value, connectBody.Port);

        string? overlayFailureDetails = null;
        await WaitForAsync(
            async () =>
            {
                var alphaConnections = await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
                var betaConnections = await betaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");

                var alphaConnected = alphaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-beta", StringComparison.OrdinalIgnoreCase)) == true;
                var betaConnected = betaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-alpha", StringComparison.OrdinalIgnoreCase)) == true;

                overlayFailureDetails = await BuildFailureDetailsAsync(alphaClient, betaClient, alphaConnections, betaConnections);
                return alphaConnected && betaConnected;
            },
            TimeSpan.FromSeconds(20),
            () => "full-instance overlay mesh neighbors did not appear on both nodes\n" + overlayFailureDetails);

        string? peerStatsFailureDetails = null;
        await WaitForAsync(
            async () =>
            {
                var alphaPeerStats = await alphaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
                var betaPeerStats = await betaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
                peerStatsFailureDetails = await BuildFailureDetailsAsync(alphaClient, betaClient, alphaPeerStats: alphaPeerStats, betaPeerStats: betaPeerStats);
                return alphaPeerStats is { TotalPeers: >= 1, OnionRoutingPeers: >= 1 }
                    && betaPeerStats is { TotalPeers: >= 1, OnionRoutingPeers: >= 1 };
            },
            TimeSpan.FromSeconds(20),
            () => "mesh peer inventory did not reflect the full-instance overlay connection\n" + peerStatsFailureDetails);

        await Task.Delay(OverlayTimeouts.MessageRead + TimeSpan.FromSeconds(5));

        string? idleFailureDetails = null;
        await WaitForAsync(
            async () =>
            {
                var alphaConnections = await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
                var betaConnections = await betaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");

                var alphaStillConnected = alphaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-beta", StringComparison.OrdinalIgnoreCase)) == true;
                var betaStillConnected = betaConnections?.Exists(peer =>
                    string.Equals(peer.Username, "mesh-alpha", StringComparison.OrdinalIgnoreCase)) == true;

                idleFailureDetails = await BuildFailureDetailsAsync(alphaClient, betaClient, alphaConnections, betaConnections);
                return alphaStillConnected && betaStillConnected;
            },
            TimeSpan.FromSeconds(5),
            () => "overlay mesh neighbors disconnected after one message-read timeout\n" + idleFailureDetails);
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, TimeSpan timeout, Func<string> failureMessageFactory)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await predicate())
            {
                return;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < TimeSpan.FromMilliseconds(250) ? remaining : TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(failureMessageFactory());
    }

    private static async Task<string> BuildFailureDetailsAsync(
        HttpClient alphaClient,
        HttpClient betaClient,
        List<MeshPeerInfoResponse>? alphaConnections = null,
        List<MeshPeerInfoResponse>? betaConnections = null,
        PeerStatistics? alphaPeerStats = null,
        PeerStatistics? betaPeerStats = null)
    {
        alphaConnections ??= await alphaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
        betaConnections ??= await betaClient.GetFromJsonAsync<List<MeshPeerInfoResponse>>("/api/v0/overlay/connections");
        alphaPeerStats ??= await alphaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
        betaPeerStats ??= await betaClient.GetFromJsonAsync<PeerStatistics>("/api/v0/security/peers/stats");
        var alphaOverlayStats = await alphaClient.GetFromJsonAsync<OverlayStatsResponse>("/api/v0/overlay/stats");
        var betaOverlayStats = await betaClient.GetFromJsonAsync<OverlayStatsResponse>("/api/v0/overlay/stats");
        var alphaDhtStatus = await alphaClient.GetFromJsonAsync<DhtStatusResponse>("/api/v0/dht/status");
        var betaDhtStatus = await betaClient.GetFromJsonAsync<DhtStatusResponse>("/api/v0/dht/status");

        return JsonSerializer.Serialize(new
        {
            alpha = new { connections = alphaConnections, peerStats = alphaPeerStats, overlay = alphaOverlayStats, dht = alphaDhtStatus },
            beta = new { connections = betaConnections, peerStats = betaPeerStats, overlay = betaOverlayStats, dht = betaDhtStatus },
        }, new JsonSerializerOptions { WriteIndented = true });
    }
}
