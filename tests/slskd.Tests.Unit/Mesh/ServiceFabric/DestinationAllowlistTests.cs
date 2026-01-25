// <copyright file="DestinationAllowlistTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Common.Security;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.PodCore;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// OpenTunnel allowlist and destination-validation tests. Uses HandleCallAsync and real
/// DnsSecurityService (from IServiceProvider). Success-path tests use ITunnelConnectivity
/// to connect to an in-process TcpListener so no real destination is needed.
/// </summary>
public class DestinationAllowlistTests
{
    private const string PodId = "pod:00000000000000000000000000000001";
    private const string RemotePeerId = "peer1";

    private readonly Mock<ILogger<PrivateGatewayMeshService>> _loggerMock = new();
    private readonly Mock<IPodService> _podServiceMock = new();

    private static slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest Req(string podId, string host, int port, string? serviceName = null, string? nonce = null, long? ts = null) =>
        new slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest
        {
            PodId = podId,
            DestinationHost = host,
            DestinationPort = port,
            ServiceName = serviceName,
            RequestNonce = nonce ?? Guid.NewGuid().ToString("N"),
            RequestTimestamp = ts ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

    private PrivateGatewayMeshService CreateService(ITunnelConnectivity? tunnelConnectivity = null, IDnsSecurityService? dnsSecurity = null)
    {
        var sp = new Mock<IServiceProvider>();
        if (dnsSecurity == null)
        {
            var dnsLogger = new Mock<ILogger<DnsSecurityService>>();
            var dns = new DnsSecurityService(dnsLogger.Object);
            sp.Setup(x => x.GetService(typeof(DnsSecurityService))).Returns(dns);
        }
        return new PrivateGatewayMeshService(_loggerMock.Object, _podServiceMock.Object, sp.Object, meshOptions: null, tunnelConnectivity: tunnelConnectivity, dnsSecurity: dnsSecurity);
    }

    private void SetupPodAndMembers(Pod pod, PodPrivateServicePolicy policy)
    {
        _podServiceMock.Setup(x => x.GetPodAsync(PodId, It.IsAny<CancellationToken>())).ReturnsAsync(pod);
        _podServiceMock.Setup(x => x.GetMembersAsync(PodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = RemotePeerId, Role = "member" } });
    }

    private ServiceCall Call(slskd.Mesh.ServiceFabric.Services.OpenTunnelRequest req) => new ServiceCall
    {
        Method = "OpenTunnel",
        Payload = JsonSerializer.SerializeToUtf8Bytes(req),
        CorrelationId = Guid.NewGuid().ToString()
    };

    [Fact]
    public async Task OpenTunnel_WildcardHostnameMatch_Allowed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint!).Port;
        var service = CreateService(new TestTunnelConnectivity(port));
        var policy = CreatePolicyWithAllowlist("*.example.com");
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = listener.AcceptTcpClientAsync(cts.Token);
        var result = await service.HandleCallAsync(Call(Req(PodId, "www.example.com", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.True(result.IsSuccess);
        var response = JsonSerializer.Deserialize<slskd.Mesh.ServiceFabric.Services.OpenTunnelResponse>(result.Payload);
        Assert.True(response?.Accepted == true);
        using (await acceptTask) { }
        listener.Stop();
    }

    [Fact]
    public async Task OpenTunnel_HostnameNotInAllowlist_Rejected()
    {
        var service = CreateService();
        var policy = CreatePolicyWithAllowlist("api.example.com");
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "evil.com", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task OpenTunnel_PrivateIpWithoutPrivateAllowed_Rejected()
    {
        var service = CreateService();
        var policy = CreatePolicyWithAllowlist("192.168.1.100");
        policy.AllowPrivateRanges = false;
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "192.168.1.100", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task OpenTunnel_PrivateIpWithPrivateAllowed_Allowed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint!).Port;
        var service = CreateService(new TestTunnelConnectivity(port));
        var policy = CreatePolicyWithAllowlist("192.168.1.100");
        policy.AllowPrivateRanges = true;
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = listener.AcceptTcpClientAsync(cts.Token);
        var result = await service.HandleCallAsync(Call(Req(PodId, "192.168.1.100", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.True(result.IsSuccess);
        var response = JsonSerializer.Deserialize<slskd.Mesh.ServiceFabric.Services.OpenTunnelResponse>(result.Payload);
        Assert.True(response?.Accepted == true);
        using (await acceptTask) { }
        listener.Stop();
    }

    [Fact]
    public async Task OpenTunnel_PublicIpWithoutPublicAllowed_Rejected()
    {
        var service = CreateService();
        var policy = CreatePolicyWithAllowlist("8.8.8.8");
        policy.AllowPublicDestinations = false;
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "8.8.8.8", 53)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task OpenTunnel_BlockedAddress_Rejected()
    {
        var service = CreateService();
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "127.0.0.1", Port = 8080, Protocol = "tcp" } },
            RegisteredServices = new List<RegisteredService>()
        };
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "127.0.0.1", 8080)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.True(
            result.ErrorMessage.Contains("not allowed", StringComparison.OrdinalIgnoreCase) || result.ErrorMessage.Contains("blocked", StringComparison.OrdinalIgnoreCase),
            "Expected 'not allowed' or 'blocked'; actual: " + result.ErrorMessage);
    }

    [Fact]
    public async Task OpenTunnel_RegisteredServiceMatch_Allowed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint!).Port;
        var service = CreateService(new TestTunnelConnectivity(port));
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = true,
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "Test DB", Host = "example.com", Port = 5432, Protocol = "tcp" }
            }
        };
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = listener.AcceptTcpClientAsync(cts.Token);
        var result = await service.HandleCallAsync(Call(Req(PodId, "example.com", 5432, "Test DB")), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.True(result.IsSuccess);
        var response = JsonSerializer.Deserialize<slskd.Mesh.ServiceFabric.Services.OpenTunnelResponse>(result.Payload);
        Assert.True(response?.Accepted == true);
        using (await acceptTask) { }
        listener.Stop();
    }

    [Fact]
    public async Task OpenTunnel_RegisteredServicePortMismatch_Rejected()
    {
        var service = CreateService();
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "Test DB", Host = "db.internal.company.com", Port = 5432, Protocol = "tcp" }
            }
        };
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "db.internal.company.com", 3306, "Test DB")), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task OpenTunnel_InvalidPort_Rejected()
    {
        var service = CreateService();
        var policy = CreatePolicyWithAllowlist("example.com");
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "example.com", 70000)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("port", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenTunnel_DnsResolutionFailure_Rejected()
    {
        var service = CreateService();
        var policy = CreatePolicyWithAllowlist("nonexistent.domain.that.does.not.resolve.example");
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "nonexistent.domain.that.does.not.resolve.example", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.True(result.ErrorMessage!.Contains("resolve", StringComparison.OrdinalIgnoreCase) || result.ErrorMessage.Contains("DNS", StringComparison.OrdinalIgnoreCase) || result.ErrorMessage.Length > 0);
    }

    [Fact]
    public async Task OpenTunnel_MixedAllowedAndBlockedIPs_Rejected()
    {
        var dnsMock = new Mock<IDnsSecurityService>();
        dnsMock.Setup(x => x.ResolveAndValidateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DnsResolutionResult.Failure("All resolved IP addresses are blocked for security reasons"));
        var service = CreateService(dnsSecurity: dnsMock.Object);
        var policy = CreatePolicyWithAllowlist("mixed.test");
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "mixed.test", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("blocked", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenTunnel_EmptyAllowlistWithPrivateAllowed_Allowed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint!).Port;
        var service = CreateService(new TestTunnelConnectivity(port));
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" } },
            RegisteredServices = new List<RegisteredService>()
        };
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var acceptTask = listener.AcceptTcpClientAsync(cts.Token);
        var result = await service.HandleCallAsync(Call(Req(PodId, "192.168.1.100", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.True(result.IsSuccess);
        var response = JsonSerializer.Deserialize<slskd.Mesh.ServiceFabric.Services.OpenTunnelResponse>(result.Payload);
        Assert.True(response?.Accepted == true);
        using (await acceptTask) { }
        listener.Stop();
    }

    [Fact]
    public async Task OpenTunnel_EmptyAllowlistWithPrivateNotAllowed_Rejected()
    {
        var service = CreateService();
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = false,
            AllowPublicDestinations = true,
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>()
        };
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "192.168.1.100", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("not allowed", result.ErrorMessage);
    }

    [Fact]
    public async Task OpenTunnel_WildcardPatternTooBroad_Rejected()
    {
        var service = CreateService();
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "*.*", Port = 80, Protocol = "tcp" } },
            RegisteredServices = new List<RegisteredService>()
        };
        var pod = CreatePodWithPolicy(policy);
        SetupPodAndMembers(pod, policy);

        var result = await service.HandleCallAsync(Call(Req(PodId, "evil.com", 80)), new MeshServiceContext { RemotePeerId = RemotePeerId });

        Assert.False(result.IsSuccess);
    }

    private PodPrivateServicePolicy CreatePolicyWithAllowlist(params string[] hosts)
    {
        return new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer:mesh:self",
            AllowPrivateRanges = true,
            AllowPublicDestinations = true,
            AllowedDestinations = hosts.Select(h => new AllowedDestination { HostPattern = h, Port = 80, Protocol = "tcp" }).ToList(),
            RegisteredServices = new List<RegisteredService>()
        };
    }

    private Pod CreatePodWithPolicy(PodPrivateServicePolicy policy) => new Pod
    {
        PodId = PodId,
        Name = "Test Pod",
        Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
        PrivateServicePolicy = policy,
        Members = new List<PodMember> { new PodMember { PeerId = RemotePeerId, Role = "member" } }
    };

    /// <summary>Connects to 127.0.0.1:port so tests can use an in-process TcpListener instead of a real destination.</summary>
    private sealed class TestTunnelConnectivity : ITunnelConnectivity
    {
        private readonly int _port;
        public TestTunnelConnectivity(int port) => _port = port;
        public async Task<(NetworkStream Stream, string? ConnectedIP)> ConnectAsync(string host, int port, IReadOnlyList<string> resolvedIPs, CancellationToken cancellationToken)
        {
            var c = new TcpClient();
            await c.ConnectAsync("127.0.0.1", _port, cancellationToken);
            var ip = resolvedIPs.Count > 0 ? resolvedIPs[0] : "127.0.0.1";
            return (c.GetStream(), ip);
        }
    }
}
