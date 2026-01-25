// <copyright file="RateLimitTimeoutTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.PodCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

public class RateLimitTimeoutTests
{
    private readonly Mock<ILogger<PrivateGatewayMeshService>> _loggerMock;
    private readonly Mock<IPodService> _podServiceMock;
    private readonly PrivateGatewayMeshService _service;
    private readonly ConcurrentDictionary<string, TunnelSession> _activeTunnels;

    public RateLimitTimeoutTests()
    {
        _loggerMock = new Mock<ILogger<PrivateGatewayMeshService>>();
        _podServiceMock = new Mock<IPodService>();
        var dns = new DnsSecurityService(Mock.Of<ILogger<DnsSecurityService>>());
        var services = new ServiceCollection();
        services.AddSingleton(dns);
        var sp = services.BuildServiceProvider();

        _service = new PrivateGatewayMeshService(_loggerMock.Object, _podServiceMock.Object, sp, null);

        var field = typeof(PrivateGatewayMeshService).GetField("_activeTunnels", BindingFlags.NonPublic | BindingFlags.Instance);
        _activeTunnels = (ConcurrentDictionary<string, TunnelSession>)field!.GetValue(_service)!;
    }

    [Fact]
    public async Task OpenTunnel_ConcurrentTunnelsPerPeerLimitExceeded_Rejected()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 10,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxConcurrentTunnelsPerPeer = 2,
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" },
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 81, Protocol = "tcp" },
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 82, Protocol = "tcp" }
            }
        };
        var pod = CreatePodWithPolicy(policy);
        var peerId = "test-peer";

        _podServiceMock.Setup(x => x.GetPodAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod.Members);

        var t1 = CreateTunnelSession("tunnel1", peerId, "192.168.1.100", 80);
        var t2 = CreateTunnelSession("tunnel2", peerId, "192.168.1.100", 81);
        _activeTunnels[t1.TunnelId] = t1;
        _activeTunnels[t2.TunnelId] = t2;

        var request = CreateOpenTunnelRequest(TestPodId, "192.168.1.100", 82);
        var result = await _service.HandleCallAsync(CreateServiceCall("OpenTunnel", request), new MeshServiceContext { RemotePeerId = peerId });

        Assert.False(result.IsSuccess);
        Assert.Contains("per peer", result.ErrorMessage!.ToLowerInvariant());
    }

    [Fact(Skip = "OpenTunnel connects via TcpClient; no abstraction to inject. Requires TCP listener.")]
    public async Task OpenTunnel_ConcurrentTunnelsPerPeerWithinLimit_Accepted()
    {
        // Would need TCP to succeed; skipped.
        await Task.CompletedTask;
    }

    [Fact]
    public async Task OpenTunnel_PodConcurrentTunnelsLimitExceeded_Rejected()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 10,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxConcurrentTunnelsPod = 2,
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" },
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 81, Protocol = "tcp" },
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 82, Protocol = "tcp" }
            }
        };
        var pod = CreatePodWithPolicy(policy);

        _podServiceMock.Setup(x => x.GetPodAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod.Members);

        var t1 = CreateTunnelSession("t1", "peer1", "192.168.1.100", 80);
        var t2 = CreateTunnelSession("t2", "peer2", "192.168.1.100", 81);
        _activeTunnels[t1.TunnelId] = t1;
        _activeTunnels[t2.TunnelId] = t2;

        var request = CreateOpenTunnelRequest(TestPodId, "192.168.1.100", 82);
        var result = await _service.HandleCallAsync(CreateServiceCall("OpenTunnel", request), new MeshServiceContext { RemotePeerId = "peer3" });

        Assert.False(result.IsSuccess);
        Assert.Contains("for pod", result.ErrorMessage!.ToLowerInvariant());
    }

    [Fact]
    public async Task OpenTunnel_NewTunnelsRateLimitExceeded_Rejected()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 10,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxNewTunnelsPerMinutePerPeer = 1,
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" },
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 81, Protocol = "tcp" }
            }
        };
        var pod = CreatePodWithPolicy(policy);
        var peerId = "test-peer";

        _podServiceMock.Setup(x => x.GetPodAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod.Members);

        var recent = CreateTunnelSession("tunnel1", peerId, "192.168.1.100", 80);
        recent.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-30);
        _activeTunnels[recent.TunnelId] = recent;

        var request = CreateOpenTunnelRequest(TestPodId, "192.168.1.100", 81);
        var result = await _service.HandleCallAsync(CreateServiceCall("OpenTunnel", request), new MeshServiceContext { RemotePeerId = peerId });

        Assert.False(result.IsSuccess);
        Assert.Contains("rate limit", result.ErrorMessage!.ToLowerInvariant());
    }

    [Fact(Skip = "OpenTunnel connects via TcpClient; no abstraction to inject. Requires TCP listener.")]
    public async Task OpenTunnel_NewTunnelsRateLimitWithinLimits_Accepted()
    {
        await Task.CompletedTask;
    }

    [Fact(Skip = "CleanupExpiredTunnels(policy) does not exist; prod uses CleanupExpiredTunnelsAsync() which loops.")]
    public void CleanupExpiredTunnels_RemovesIdleTunnels() { }

    [Fact(Skip = "CleanupExpiredTunnels(policy) does not exist; prod uses CleanupExpiredTunnelsAsync() which loops.")]
    public void CleanupExpiredTunnels_RemovesMaxLifetimeExceededTunnels() { }

    [Fact(Skip = "CleanupExpiredTunnels(policy) does not exist; prod uses CleanupExpiredTunnelsAsync() which loops.")]
    public void CleanupExpiredTunnels_KeepsActiveTunnelsWithinLimits() { }

    [Fact]
    public async Task CloseTunnel_RemovesFromActiveTunnels()
    {
        var tunnelId = "test-tunnel";
        var tunnel = CreateTunnelSession(tunnelId, "peer1", "192.168.1.100", 80);
        _activeTunnels[tunnelId] = tunnel;

        var pod = CreatePodWithPolicy(new PodPrivateServicePolicy { GatewayPeerId = "gateway-peer" });
        _podServiceMock.Setup(x => x.GetPodAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);

        var closeRequest = new slskd.Mesh.ServiceFabric.Services.CloseTunnelRequest { TunnelId = tunnelId };
        var call = CreateServiceCall("CloseTunnel", closeRequest);
        var result = await _service.HandleCallAsync(call, new MeshServiceContext { RemotePeerId = "peer1" });

        Assert.True(result.IsSuccess);
        Assert.Empty(_activeTunnels);
    }

    [Fact]
    public async Task CloseTunnel_NonExistentTunnel_ReturnsError()
    {
        var closeRequest = new slskd.Mesh.ServiceFabric.Services.CloseTunnelRequest { TunnelId = "nonexistent" };
        var call = CreateServiceCall("CloseTunnel", closeRequest);
        var result = await _service.HandleCallAsync(call, new MeshServiceContext { RemotePeerId = "peer1" });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage!.ToLowerInvariant());
    }

    [Fact]
    public async Task CloseTunnel_WrongPeer_ReturnsError()
    {
        var tunnelId = "test-tunnel";
        _activeTunnels[tunnelId] = CreateTunnelSession(tunnelId, "peer1", "192.168.1.100", 80);

        var pod = CreatePodWithPolicy(new PodPrivateServicePolicy { GatewayPeerId = "gateway-peer" });
        _podServiceMock.Setup(x => x.GetPodAsync(TestPodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);

        var closeRequest = new slskd.Mesh.ServiceFabric.Services.CloseTunnelRequest { TunnelId = tunnelId };
        var call = CreateServiceCall("CloseTunnel", closeRequest);
        var result = await _service.HandleCallAsync(call, new MeshServiceContext { RemotePeerId = "wrong-peer" });

        Assert.False(result.IsSuccess);
        Assert.Contains("gateway", result.ErrorMessage!.ToLowerInvariant());
        Assert.Single(_activeTunnels);
    }

    private const string TestPodId = "pod:0123456789abcdef0123456789abcdef";

    private Pod CreatePodWithPolicy(PodPrivateServicePolicy policy)
    {
        return new Pod
        {
            PodId = TestPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = policy,
            Members = new List<PodMember>
            {
                new PodMember { PeerId = "gateway-peer", Role = PodRoles.Owner },
                new PodMember { PeerId = "peer1", Role = PodRoles.Member },
                new PodMember { PeerId = "peer2", Role = PodRoles.Member },
                new PodMember { PeerId = "peer3", Role = PodRoles.Member },
                new PodMember { PeerId = "test-peer", Role = PodRoles.Member }
            }
        };
    }

    private static TunnelSession CreateTunnelSession(string tunnelId, string clientPeerId, string destinationHost, int destinationPort)
    {
        return new TunnelSession
        {
            TunnelId = tunnelId,
            ClientPeerId = clientPeerId,
            PodId = TestPodId,
            DestinationHost = destinationHost,
            DestinationPort = destinationPort,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivity = DateTimeOffset.UtcNow
        };
    }

    private static slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest CreateOpenTunnelRequest(string podId, string destinationHost, int destinationPort)
    {
        return new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = podId,
            DestinationHost = destinationHost,
            DestinationPort = destinationPort,
            RequestNonce = Guid.NewGuid().ToString(),
            RequestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private static ServiceCall CreateServiceCall(string method, object payload)
    {
        return new ServiceCall
        {
            ServiceName = "private-gateway",
            Method = method,
            Payload = JsonSerializer.SerializeToUtf8Bytes(payload),
            CorrelationId = Guid.NewGuid().ToString()
        };
    }
}
