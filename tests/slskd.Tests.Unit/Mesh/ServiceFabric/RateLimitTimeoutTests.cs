// <copyright file="RateLimitTimeoutTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric.Services;
using slskd.PodCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

public class RateLimitTimeoutTests : IDisposable
{
    private readonly Mock<ILogger<PrivateGatewayMeshService>> _loggerMock;
    private readonly Mock<IPodService> _podServiceMock;
    private readonly Mock<DnsSecurityService> _dnsSecurityServiceMock;
    private readonly PrivateGatewayMeshService _service;
    private readonly List<TunnelSession> _activeTunnels;

    public RateLimitTimeoutTests()
    {
        _loggerMock = new Mock<ILogger<PrivateGatewayMeshService>>();
        _podServiceMock = new Mock<IPodService>();
        _dnsSecurityServiceMock = new Mock<DnsSecurityService>();
        _service = new PrivateGatewayMeshService(_loggerMock.Object, _podServiceMock.Object, null!, _dnsSecurityServiceMock.Object);

        // Access private field for testing
        var activeTunnelsField = typeof(PrivateGatewayMeshService).GetField("_activeTunnels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _activeTunnels = (List<TunnelSession>)activeTunnelsField!.GetValue(_service)!;
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public async Task OpenTunnel_ConcurrentTunnelsPerPeerLimitExceeded_Rejected()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "gateway-peer",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxConcurrentTunnelsPerPeer = 2, // Limit to 2 tunnels per peer
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" }
            }
        };

        var pod = CreatePodWithPolicy(policy);
        var peerId = "test-peer";

        // Setup mocks
        _podServiceMock.Setup(x => x.GetPodAndPolicyAsync("test-pod", It.IsAny<CancellationToken>()))
            .ReturnsAsync((pod, policy));

        _dnsSecurityServiceMock.Setup(x => x.ResolveAndCacheHostnameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PodPrivateServicePolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse("192.168.1.100") });

        // Create existing tunnels to reach the limit
        var existingTunnel1 = CreateTunnelSession("tunnel1", peerId, "192.168.1.100", 80);
        var existingTunnel2 = CreateTunnelSession("tunnel2", peerId, "192.168.1.100", 81);
        _activeTunnels.Add(existingTunnel1);
        _activeTunnels.Add(existingTunnel2);

        // Act - Try to open a third tunnel
        var request = CreateOpenTunnelRequest("test-pod", "192.168.1.100", 82, peerId);
        var context = new MeshServiceContext { RemotePeerId = peerId };
        var result = await _service.HandleOpenTunnelAsync(CreateServiceCall(request), context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("concurrent tunnels", result.ErrorMessage.ToLowerInvariant());
    }

    [Fact]
    public async Task OpenTunnel_ConcurrentTunnelsPerPeerWithinLimit_Accepted()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "gateway-peer",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxConcurrentTunnelsPerPeer = 3,
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" }
            }
        };

        var pod = CreatePodWithPolicy(policy);
        var peerId = "test-peer";

        // Setup mocks
        _podServiceMock.Setup(x => x.GetPodAndPolicyAsync("test-pod", It.IsAny<CancellationToken>()))
            .ReturnsAsync((pod, policy));

        _dnsSecurityServiceMock.Setup(x => x.ResolveAndCacheHostnameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PodPrivateServicePolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse("192.168.1.100") });

        // Create existing tunnels (below limit)
        var existingTunnel1 = CreateTunnelSession("tunnel1", peerId, "192.168.1.100", 80);
        var existingTunnel2 = CreateTunnelSession("tunnel2", peerId, "192.168.1.100", 81);
        _activeTunnels.Add(existingTunnel1);
        _activeTunnels.Add(existingTunnel2);

        // Act - Try to open a third tunnel (at limit)
        var request = CreateOpenTunnelRequest("test-pod", "192.168.1.100", 82, peerId);
        var context = new MeshServiceContext { RemotePeerId = peerId };
        var result = await _service.HandleOpenTunnelAsync(CreateServiceCall(request), context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(3, _activeTunnels.Count(t => t.ClientPeerId == peerId));
    }

    [Fact]
    public async Task OpenTunnel_PodConcurrentTunnelsLimitExceeded_Rejected()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "gateway-peer",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxConcurrentTunnelsPod = 2, // Total limit for pod
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" }
            }
        };

        var pod = CreatePodWithPolicy(policy);

        // Setup mocks
        _podServiceMock.Setup(x => x.GetPodAndPolicyAsync("test-pod", It.IsAny<CancellationToken>()))
            .ReturnsAsync((pod, policy));

        _dnsSecurityServiceMock.Setup(x => x.ResolveAndCacheHostnameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PodPrivateServicePolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse("192.168.1.100") });

        // Create existing tunnels to reach the pod limit
        var existingTunnel1 = CreateTunnelSession("tunnel1", "peer1", "192.168.1.100", 80);
        var existingTunnel2 = CreateTunnelSession("tunnel2", "peer2", "192.168.1.100", 81);
        _activeTunnels.Add(existingTunnel1);
        _activeTunnels.Add(existingTunnel2);

        // Act - Try to open another tunnel
        var request = CreateOpenTunnelRequest("test-pod", "192.168.1.100", 82, "peer3");
        var context = new MeshServiceContext { RemotePeerId = "peer3" };
        var result = await _service.HandleOpenTunnelAsync(CreateServiceCall(request), context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("pod tunnels", result.ErrorMessage.ToLowerInvariant());
    }

    [Fact]
    public async Task OpenTunnel_NewTunnelsRateLimitExceeded_Rejected()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "gateway-peer",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxNewTunnelsPerMinutePerPeer = 1, // Only 1 new tunnel per minute per peer
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" }
            }
        };

        var pod = CreatePodWithPolicy(policy);
        var peerId = "test-peer";

        // Setup mocks
        _podServiceMock.Setup(x => x.GetPodAndPolicyAsync("test-pod", It.IsAny<CancellationToken>()))
            .ReturnsAsync((pod, policy));

        _dnsSecurityServiceMock.Setup(x => x.ResolveAndCacheHostnameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PodPrivateServicePolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse("192.168.1.100") });

        // Create a recent tunnel (within the rate limit window)
        var recentTunnel = CreateTunnelSession("tunnel1", peerId, "192.168.1.100", 80);
        recentTunnel.CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-30); // 30 seconds ago
        _activeTunnels.Add(recentTunnel);

        // Act - Try to open another tunnel too soon
        var request = CreateOpenTunnelRequest("test-pod", "192.168.1.100", 81, peerId);
        var context = new MeshServiceContext { RemotePeerId = peerId };
        var result = await _service.HandleOpenTunnelAsync(CreateServiceCall(request), context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("rate limit", result.ErrorMessage.ToLowerInvariant());
    }

    [Fact]
    public async Task OpenTunnel_NewTunnelsRateLimitWithinLimits_Accepted()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "gateway-peer",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            MaxNewTunnelsPerMinutePerPeer = 2,
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" }
            }
        };

        var pod = CreatePodWithPolicy(policy);
        var peerId = "test-peer";

        // Setup mocks
        _podServiceMock.Setup(x => x.GetPodAndPolicyAsync("test-pod", It.IsAny<CancellationToken>()))
            .ReturnsAsync((pod, policy));

        _dnsSecurityServiceMock.Setup(x => x.ResolveAndCacheHostnameAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PodPrivateServicePolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { IPAddress.Parse("192.168.1.100") });

        // Create an old tunnel (outside the rate limit window)
        var oldTunnel = CreateTunnelSession("tunnel1", peerId, "192.168.1.100", 80);
        oldTunnel.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2); // 2 minutes ago
        _activeTunnels.Add(oldTunnel);

        // Act - Try to open another tunnel
        var request = CreateOpenTunnelRequest("test-pod", "192.168.1.100", 81, peerId);
        var context = new MeshServiceContext { RemotePeerId = peerId };
        var result = await _service.HandleOpenTunnelAsync(CreateServiceCall(request), context);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CleanupExpiredTunnels_RemovesIdleTunnels()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            IdleTimeout = TimeSpan.FromMinutes(5)
        };

        // Create tunnels - some idle, some active
        var idleTunnel = CreateTunnelSession("idle", "peer1", "192.168.1.100", 80);
        idleTunnel.LastActivity = DateTimeOffset.UtcNow.AddMinutes(-10); // Idle for 10 minutes

        var activeTunnel = CreateTunnelSession("active", "peer2", "192.168.1.100", 81);
        activeTunnel.LastActivity = DateTimeOffset.UtcNow.AddMinutes(-1); // Active recently

        _activeTunnels.Add(idleTunnel);
        _activeTunnels.Add(activeTunnel);

        // Act
        _service.TestCleanupExpiredTunnels(policy);

        // Assert
        Assert.Single(_activeTunnels); // Only active tunnel should remain
        Assert.Contains(_activeTunnels, t => t.TunnelId == "active");
        Assert.DoesNotContain(_activeTunnels, t => t.TunnelId == "idle");
    }

    [Fact]
    public void CleanupExpiredTunnels_RemovesMaxLifetimeExceededTunnels()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            IdleTimeout = TimeSpan.FromHours(1), // Long idle timeout
            MaxLifetime = TimeSpan.FromMinutes(30) // Short max lifetime
        };

        // Create tunnels - one old, one new
        var oldTunnel = CreateTunnelSession("old", "peer1", "192.168.1.100", 80);
        oldTunnel.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-45); // Created 45 minutes ago
        oldTunnel.LastActivity = DateTimeOffset.UtcNow; // But active recently

        var newTunnel = CreateTunnelSession("new", "peer2", "192.168.1.100", 81);
        newTunnel.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15); // Created 15 minutes ago

        _activeTunnels.Add(oldTunnel);
        _activeTunnels.Add(newTunnel);

        // Act
        _service.TestCleanupExpiredTunnels(policy);

        // Assert
        Assert.Single(_activeTunnels); // Only new tunnel should remain
        Assert.Contains(_activeTunnels, t => t.TunnelId == "new");
        Assert.DoesNotContain(_activeTunnels, t => t.TunnelId == "old");
    }

    [Fact]
    public void CleanupExpiredTunnels_KeepsActiveTunnelsWithinLimits()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            IdleTimeout = TimeSpan.FromMinutes(5),
            MaxLifetime = TimeSpan.FromHours(1)
        };

        // Create active tunnels within limits
        var tunnel1 = CreateTunnelSession("tunnel1", "peer1", "192.168.1.100", 80);
        tunnel1.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30);
        tunnel1.LastActivity = DateTimeOffset.UtcNow.AddMinutes(-2);

        var tunnel2 = CreateTunnelSession("tunnel2", "peer2", "192.168.1.100", 81);
        tunnel2.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-45);
        tunnel2.LastActivity = DateTimeOffset.UtcNow.AddMinutes(-3);

        _activeTunnels.Add(tunnel1);
        _activeTunnels.Add(tunnel2);

        // Act
        _service.TestCleanupExpiredTunnels(policy);

        // Assert
        Assert.Equal(2, _activeTunnels.Count); // Both should remain
    }

    [Fact]
    public async Task CloseTunnel_RemovesFromActiveTunnels()
    {
        // Arrange
        var tunnelId = "test-tunnel";
        var tunnel = CreateTunnelSession(tunnelId, "peer1", "192.168.1.100", 80);
        _activeTunnels.Add(tunnel);

        // Act
        var closeRequest = new CloseTunnelRequest { TunnelId = tunnelId };
        var serviceCall = new ServiceCall
        {
            ServiceName = "private-gateway",
            Method = "CloseTunnel",
            Payload = JsonSerializer.SerializeToUtf8Bytes(closeRequest),
            CorrelationId = Guid.NewGuid().ToString()
        };
        var context = new MeshServiceContext { RemotePeerId = "peer1" };

        var result = await _service.HandleCloseTunnelAsync(serviceCall, context);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(_activeTunnels);
    }

    [Fact]
    public async Task CloseTunnel_NonExistentTunnel_ReturnsError()
    {
        // Arrange
        var closeRequest = new CloseTunnelRequest { TunnelId = "nonexistent" };
        var serviceCall = new ServiceCall
        {
            ServiceName = "private-gateway",
            Method = "CloseTunnel",
            Payload = JsonSerializer.SerializeToUtf8Bytes(closeRequest),
            CorrelationId = Guid.NewGuid().ToString()
        };
        var context = new MeshServiceContext { RemotePeerId = "peer1" };

        // Act
        var result = await _service.HandleCloseTunnelAsync(serviceCall, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage.ToLowerInvariant());
    }

    [Fact]
    public async Task CloseTunnel_WrongPeer_ReturnsError()
    {
        // Arrange
        var tunnelId = "test-tunnel";
        var tunnel = CreateTunnelSession(tunnelId, "peer1", "192.168.1.100", 80);
        _activeTunnels.Add(tunnel);

        var closeRequest = new CloseTunnelRequest { TunnelId = tunnelId };
        var serviceCall = new ServiceCall
        {
            ServiceName = "private-gateway",
            Method = "CloseTunnel",
            Payload = JsonSerializer.SerializeToUtf8Bytes(closeRequest),
            CorrelationId = Guid.NewGuid().ToString()
        };
        var context = new MeshServiceContext { RemotePeerId = "wrong-peer" }; // Wrong peer

        // Act
        var result = await _service.HandleCloseTunnelAsync(serviceCall, context);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("not authorized", result.ErrorMessage.ToLowerInvariant());
        Assert.Single(_activeTunnels); // Tunnel should still exist
    }

    // Helper methods

    private Pod CreatePodWithPolicy(PodPrivateServicePolicy policy)
    {
        return new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = policy,
            Members = new List<PodMember>
            {
                new PodMember { PeerId = "gateway-peer", Role = PodMemberRole.Admin },
                new PodMember { PeerId = "peer1", Role = PodMemberRole.Member }
            }
        };
    }

    private TunnelSession CreateTunnelSession(string tunnelId, string clientPeerId, string destinationHost, int destinationPort)
    {
        return new TunnelSession
        {
            TunnelId = tunnelId,
            ClientPeerId = clientPeerId,
            PodId = "test-pod",
            DestinationHost = destinationHost,
            DestinationPort = destinationPort,
            CreatedAt = DateTimeOffset.UtcNow,
            LastActivity = DateTimeOffset.UtcNow
        };
    }

    private OpenTunnelRequest CreateOpenTunnelRequest(string podId, string destinationHost, int destinationPort, string clientPeerId)
    {
        return new OpenTunnelRequest
        {
            PodId = podId,
            DestinationHost = destinationHost,
            DestinationPort = destinationPort,
            RequestNonce = Guid.NewGuid().ToString(),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ClientPeerId = clientPeerId
        };
    }

    private ServiceCall CreateServiceCall(OpenTunnelRequest request)
    {
        return new ServiceCall
        {
            ServiceName = "private-gateway",
            Method = "OpenTunnel",
            Payload = JsonSerializer.SerializeToUtf8Bytes(request),
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    // Mock DTOs for testing
    internal record OpenTunnelRequest
    {
        public string PodId { get; init; } = string.Empty;
        public string DestinationHost { get; init; } = string.Empty;
        public int DestinationPort { get; init; }
        public string? ClientPeerId { get; init; }
        public string RequestNonce { get; init; } = string.Empty;
        public long TimestampUnixMs { get; init; }
    }

    internal record CloseTunnelRequest
    {
        public string TunnelId { get; init; } = string.Empty;
    }
}

// Extension methods for testing private methods
internal static class PrivateGatewayMeshServiceExtensions
{
    public static void TestCleanupExpiredTunnels(this PrivateGatewayMeshService service, PodPrivateServicePolicy policy)
    {
        var method = typeof(PrivateGatewayMeshService).GetMethod("CleanupExpiredTunnels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method!.Invoke(service, new object[] { policy });
    }
}


