// <copyright file="PrivateGatewayMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.PodCore;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PrivateGatewayMeshServiceTests
{
    private readonly Mock<ILogger<PrivateGatewayMeshService>> _loggerMock;
    private readonly Mock<IPodService> _podServiceMock;
    private readonly PrivateGatewayMeshService _service;

    public PrivateGatewayMeshServiceTests()
    {
        _loggerMock = new Mock<ILogger<PrivateGatewayMeshService>>();
        _podServiceMock = new Mock<IPodService>();

        // Use a mock service provider since we don't need real DI for basic tests
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPodService)))
            .Returns(_podServiceMock.Object);

        _service = new PrivateGatewayMeshService(
            _loggerMock.Object,
            _podServiceMock.Object,
            serviceProviderMock.Object);
    }

    [Fact]
    public void ServiceName_ReturnsCorrectName()
    {
        Assert.Equal("private-gateway", _service.ServiceName);
    }

    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsMethodNotFound()
    {
        // Arrange
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "UnknownMethod",
            Payload = Array.Empty<byte>()
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:test",
            RemotePublicKey = "test-key"
        };

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.MethodNotFound, result.StatusCode);
        Assert.Contains("Unknown method", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_PodNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new OpenTunnelRequest
        {
            PodId = "pod:notfound",
            DestinationHost = "example.com",
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:test"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:notfound"))
            .ReturnsAsync((Pod?)null);

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.ServiceNotFound, result.StatusCode);
        Assert.Contains("Pod not found", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_VpnNotEnabled_ReturnsUnavailable()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "Test Pod"
            // No PrivateServiceCapability
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "example.com",
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:test"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.Contains("Private gateway not enabled", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_NotPodMember_ReturnsForbidden()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "example.com",
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:outsider" // Not a member
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member1", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Only pod members can open tunnels", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_DestinationNotAllowed_ReturnsForbidden()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "allowed.com", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "notallowed.com", // Not in allowlist
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Destination not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_InvalidHostname_ReturnsInvalidPayload()
    {
        // Arrange
        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "invalid hostname with spaces", // Invalid hostname
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("Invalid DestinationHost format", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_DangerousHostname_ReturnsInvalidPayload()
    {
        // Arrange
        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "localhost", // Dangerous hostname
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("Destination hostname is not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_InvalidPort_ReturnsInvalidPayload()
    {
        // Arrange
        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "example.com",
            DestinationPort = 99999 // Invalid port
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("DestinationPort must be between 1 and 65535", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_PrivateAddressNotAllowed_ReturnsForbidden()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowPrivateRanges = false, // Private ranges not allowed
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "*.local", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "192.168.1.100", // Private IP
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Resolved IP address is in private range", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_BlockedAddress_ReturnsForbidden()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "*", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "169.254.169.254", // AWS metadata service (blocked)
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Destination address is blocked", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_RateLimitExceeded_ReturnsUnavailable()
    {
        // Arrange - Create a pod with strict rate limits
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                MaxNewTunnelsPerMinutePerPeer = 0, // No new tunnels allowed
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "example.com",
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.Contains("rate limit exceeded", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_MemberCountExceeded_ReturnsUnavailable()
    {
        // Arrange - Pod with too many members
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                MaxMembers = 2, // Only 2 members allowed
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var members = new List<PodMember>();
        for (int i = 0; i < 5; i++) // 5 members, exceeds limit
        {
            members.Add(new PodMember { PeerId = $"peer:member{i}", Role = "member" });
        }

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "example.com",
            DestinationPort = 80
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member0"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test")).ReturnsAsync(members);

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.Contains("exceeds maximum member limit", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_PublicIpRejectedInMvp_ReturnsForbidden()
    {
        // Arrange - Pod with AllowPublicDestinations = false (default)
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowPublicDestinations = false, // MVP default
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "8.8.8.8", // Public IP
            DestinationPort = 53,
            RequestNonce = Guid.NewGuid().ToString(),
            RequestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Public internet destinations are not allowed in MVP", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_InvalidRequestBinding_ReturnsForbidden()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "pod:test",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:gateway",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "192.168.1.100", Port = 80 }
                }
            }
        };

        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "192.168.1.100",
            DestinationPort = 80,
            RequestNonce = "short", // Invalid - too short
            RequestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        _podServiceMock.Setup(x => x.GetPodAsync("pod:test")).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync("pod:test"))
            .ReturnsAsync(new List<PodMember>
            {
                new PodMember { PeerId = "peer:member", Role = "member" }
            });

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("RequestNonce", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_WildcardRejected_ReturnsInvalidPayload()
    {
        // Arrange
        var request = new OpenTunnelRequest
        {
            PodId = "pod:test",
            DestinationHost = "*.example.com", // Wildcard not allowed in MVP
            DestinationPort = 80,
            RequestNonce = Guid.NewGuid().ToString(),
            RequestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext
        {
            RemotePeerId = "peer:member"
        };

        // Act
        var result = await _service.HandleCallAsync(call, context);

        // Assert
        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("Wildcards are not allowed", result.ErrorMessage);
    }
}
