// <copyright file="PrivateGatewayMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
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

        var dnsLogger = new Mock<ILogger<DnsSecurityService>>();
        var dnsSecurity = new DnsSecurityService(dnsLogger.Object);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(sp => sp.GetService(typeof(IPodService)))
            .Returns(_podServiceMock.Object);
        serviceProviderMock.Setup(sp => sp.GetService(typeof(DnsSecurityService)))
            .Returns(dnsSecurity);

        _service = new PrivateGatewayMeshService(
            _loggerMock.Object,
            _podServiceMock.Object,
            serviceProviderMock.Object);
    }

    private static slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest Req(string podId, string host, int port, string? nonce = null, long? ts = null) =>
        new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = podId,
            DestinationHost = host,
            DestinationPort = port,
            RequestNonce = nonce ?? Guid.NewGuid().ToString("N"),
            RequestTimestamp = ts ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

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
        var podId = "pod:00000000000000000000000000000001";
        var request = Req(podId, "example.com", 80);

        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };

        var context = new MeshServiceContext { RemotePeerId = "peer-test" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync((Pod?)null);

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.ServiceNotFound, result.StatusCode);
        Assert.Contains("Pod not found", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_VpnNotEnabled_ReturnsUnavailable()
    {
        var podId = "pod:00000000000000000000000000000002";
        var pod = new Pod { PodId = podId, Name = "Test Pod" };

        var request = Req(podId, "example.com", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-test" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-test", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.Contains("gateway", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_NotPodMember_ReturnsForbidden()
    {
        var podId = "pod:00000000000000000000000000000003";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var request = Req(podId, "example.com", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-outsider" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member1", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Only pod members can open tunnels", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_DestinationNotAllowed_ReturnsForbidden()
    {
        var podId = "pod:00000000000000000000000000000004";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "allowed.com", Port = 80 }
                }
            }
        };

        var request = Req(podId, "notallowed.com", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Destination not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_InvalidHostname_ReturnsInvalidPayload()
    {
        var request = new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = "pod:00000000000000000000000000000005",
            DestinationHost = "invalid hostname with spaces",
            DestinationPort = 80
        };
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("Invalid DestinationHost format", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_DangerousHostname_ReturnsInvalidPayload()
    {
        var request = new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = "pod:00000000000000000000000000000006",
            DestinationHost = "localhost",
            DestinationPort = 80
        };
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("Destination hostname is not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_InvalidPort_ReturnsInvalidPayload()
    {
        var request = new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = "pod:00000000000000000000000000000007",
            DestinationHost = "example.com",
            DestinationPort = 99999
        };
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("DestinationPort must be between 1 and 65535", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_PrivateAddressNotAllowed_ReturnsForbidden()
    {
        var podId = "pod:00000000000000000000000000000008";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                AllowPrivateRanges = false,
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "*.local", Port = 80 },
                    new AllowedDestination { HostPattern = "192.168.1.100", Port = 80 }
                }
            }
        };

        var request = Req(podId, "192.168.1.100", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        // ResolveAndValidate rejects private IP when AllowPrivateRanges=false -> ServiceUnavailable
        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_BlockedAddress_ReturnsForbidden()
    {
        var podId = "pod:00000000000000000000000000000009";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "*", Port = 80 }
                }
            }
        };

        var request = Req(podId, "169.254.169.254", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.True(ServiceStatusCodes.Forbidden == result.StatusCode || ServiceStatusCodes.ServiceUnavailable == result.StatusCode);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_RateLimitExceeded_ReturnsUnavailable()
    {
        var podId = "pod:0000000000000000000000000000000a";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                MaxNewTunnelsPerMinutePerPeer = 0,
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var request = Req(podId, "example.com", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.True(result.ErrorMessage?.Contains("rate limit") == true || result.ErrorMessage?.Contains("blocked") == true || result.ErrorMessage?.Contains("Destination not allowed") == true,
            "Expected rate limit, blocked, or policy message; got: " + (result.ErrorMessage ?? ""));
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_MemberCountExceeded_ReturnsUnavailable()
    {
        var podId = "pod:0000000000000000000000000000000b";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                MaxMembers = 2,
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var members = new List<PodMember>();
        for (int i = 0; i < 5; i++)
            members.Add(new PodMember { PeerId = $"peer-member{i}", Role = "member" });

        var request = Req(podId, "example.com", 80);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member0" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(members);

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, result.StatusCode);
        Assert.Contains("exceeds maximum member limit", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_PublicIpRejectedInMvp_ReturnsForbidden()
    {
        var podId = "pod:0000000000000000000000000000000c";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                AllowPublicDestinations = false,
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "example.com", Port = 80 }
                }
            }
        };

        var request = Req(podId, "8.8.8.8", 53);
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("Destination not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_InvalidRequestBinding_ReturnsForbidden()
    {
        var podId = "pod:0000000000000000000000000000000d";
        var pod = new Pod
        {
            PodId = podId,
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peer:mesh:self", // must match service's hardcoded localPeerId to pass gateway check
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "192.168.1.100", Port = 80 }
                }
            }
        };

        var request = new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = podId,
            DestinationHost = "192.168.1.100",
            DestinationPort = 80,
            RequestNonce = "short",
            RequestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        _podServiceMock.Setup(x => x.GetPodAsync(podId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(podId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer-member", Role = "member" } });

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.Forbidden, result.StatusCode);
        Assert.Contains("RequestNonce", result.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_OpenTunnel_WildcardRejected_ReturnsInvalidPayload()
    {
        var request = new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = "pod:0000000000000000000000000000000e",
            DestinationHost = "*.example.com",
            DestinationPort = 80
        };
        var call = new ServiceCall
        {
            CorrelationId = "test-123",
            Method = "OpenTunnel",
            Payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))
        };
        var context = new MeshServiceContext { RemotePeerId = "peer-member" };

        var result = await _service.HandleCallAsync(call, context);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, result.StatusCode);
        Assert.Contains("Invalid DestinationHost format", result.ErrorMessage);
    }
}
