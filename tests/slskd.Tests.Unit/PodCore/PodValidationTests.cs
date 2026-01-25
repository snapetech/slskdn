// <copyright file="PodValidationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodValidationTests
{
    [Theory]
    [InlineData("dm")]
    [InlineData("general")]
    [InlineData("random")]
    [InlineData("music")]
    [InlineData("chat")]
    [InlineData("discussions")]
    [InlineData("a")]
    public void IsValidChannelId_ValidChannelIds_Accept(string channelId)
    {
        Assert.True(PodValidation.IsValidChannelId(channelId));
    }

    [Theory]
    [InlineData("dm:general")] // Contains colon
    [InlineData("")] // Empty
    [InlineData("channel with spaces")] // Contains spaces
    [InlineData("channel@domain")] // @ not in [a-z0-9\-_]
    [InlineData("channel\nline")] // Newline
    public void IsValidChannelId_InvalidChannelIds_Reject(string channelId)
    {
        Assert.False(PodValidation.IsValidChannelId(channelId));
    }

    [Theory]
    [InlineData("peermeshself")]
    [InlineData("bridgeusername")]
    [InlineData("peer.mesh.node1")]
    [InlineData("bridge_user_name")]
    [InlineData("peer-mesh-very-long-peer-id-with-dashes")]
    public void IsValidPeerId_ValidPeerIds_Accept(string peerId)
    {
        Assert.True(PodValidation.IsValidPeerId(peerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("peer:")] // Colon not in allowed set
    [InlineData(":mesh:self")]
    [InlineData("unknown:peer:type")]
    [InlineData("peer mesh self")] // Spaces
    public void IsValidPeerId_InvalidPeerIds_Reject(string peerId)
    {
        Assert.False(PodValidation.IsValidPeerId(peerId));
    }

    [Theory]
    [InlineData("pod:0123456789abcdef0123456789abcdef")] // 32 hex
    [InlineData("pod:abcdef1234567890abcdef1234567890")]
    [InlineData("pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void IsValidPodId_ValidPodIds_Accept(string podId)
    {
        Assert.True(PodValidation.IsValidPodId(podId));
    }

    [Theory]
    [InlineData("invalid-pod-id")]
    [InlineData("pod")]
    [InlineData("pod:")]
    [InlineData(":abc123")]
    [InlineData("pod abc123")]
    [InlineData("container:abc123")]
    [InlineData("pod:abc")] // Too few hex chars (needs 32)
    public void IsValidPodId_InvalidPodIds_Reject(string podId)
    {
        Assert.False(PodValidation.IsValidPodId(podId));
    }

    [Theory]
    [InlineData("Test Pod")]
    [InlineData("Music Discussion")]
    [InlineData("Private Chat")]
    [InlineData("A")]
    [InlineData("Very Long Pod Name With Many Words And Spaces")]
    public void IsValidPodName_ValidPodNames_Accept(string podName)
    {
        Assert.True(PodValidation.IsValidPodName(podName));
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData("   ")] // Only spaces
    public void IsValidPodName_InvalidPodNames_Reject(string podName)
    {
        Assert.False(PodValidation.IsValidPodName(podName));
    }

    [Fact]
    public void ValidatePodId_WithValidPodId_ReturnsTrue()
    {
        var podId = "pod:abcdef1234567890abcdef1234567890";
        Assert.True(PodValidation.IsValidPodId(podId));
    }

    [Fact]
    public void ValidatePodId_WithInvalidPodId_ReturnsFalse()
    {
        var podId = "invalid-pod-id";
        Assert.False(PodValidation.IsValidPodId(podId));
    }

    [Fact]
    public void ValidatePeerId_WithValidPeerId_ReturnsTrue()
    {
        var peerId = "peer-mesh-self";
        Assert.True(PodValidation.IsValidPeerId(peerId));
    }

    [Fact]
    public void ValidatePeerId_WithInvalidPeerId_ReturnsFalse()
    {
        var peerId = "invalid:peer:id";
        Assert.False(PodValidation.IsValidPeerId(peerId));
    }

    [Fact]
    public void ValidateChannelId_WithValidChannelId_ReturnsTrue()
    {
        var channelId = "general";
        Assert.True(PodValidation.IsValidChannelId(channelId));
    }

    [Fact]
    public void ValidateChannelId_WithInvalidChannelId_ReturnsFalse()
    {
        var channelId = "general:chat"; // Contains colon
        Assert.False(PodValidation.IsValidChannelId(channelId));
    }

    [Fact]
    public void ValidatePodName_WithValidPodName_ReturnsTrue()
    {
        var podName = "Test Pod";
        Assert.True(PodValidation.IsValidPodName(podName));
    }

    [Fact]
    public void ValidatePodName_WithInvalidPodName_ReturnsFalse()
    {
        var podName = "";
        Assert.False(PodValidation.IsValidPodName(podName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePodName_WithNullOrEmpty_ReturnsFalse(string? podName)
    {
        Assert.False(PodValidation.IsValidPodName(podName!));
    }

    [Fact]
    public void ValidatePod_ValidPodWithoutVpn_ReturnsValid()
    {
        var pod = new Pod
        {
            PodId = "pod:1234567890abcdef1234567890abcdef",
            Name = "Test Pod",
            Tags = new List<string> { "test" },
            Channels = new List<PodChannel> { new PodChannel { ChannelId = "general", Name = "General" } }
        };

        var result = PodValidation.ValidatePod(pod, new List<PodMember>());
        Assert.True(result.IsValid);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_NoDestinations_ReturnsInvalid()
    {
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "peergateway", Role = "owner" }
        };

        var pod = new Pod
        {
            PodId = "pod:1234567890abcdef1234567890abcdef",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peergateway",
                RegisteredServices = new List<RegisteredService>(),
                AllowedDestinations = new List<AllowedDestination>()
            }
        };

        var result = PodValidation.ValidatePod(pod, members);
        Assert.False(result.IsValid);
        Assert.Contains("allowed destination", result.Error);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_TooManyMembers_ReturnsInvalid()
    {
        var members = new List<PodMember>();
        for (int i = 0; i < 5; i++)
            members.Add(new PodMember { PeerId = $"peermember{i}", Role = "member" });

        var pod = new Pod
        {
            PodId = "pod:1234567890abcdef1234567890abcdef",
            Name = "VPN Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                GatewayPeerId = "peergateway",
                MaxMembers = 3,
                RegisteredServices = new List<RegisteredService>(),
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "printer.local", Port = 9100 }
                }
            }
        };

        var result = PodValidation.ValidatePod(pod, members);
        Assert.False(result.IsValid);
        Assert.Contains("maximum 3", result.Error);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_ValidPolicy_ReturnsValid()
    {
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "peergateway", Role = "owner" }
        };

        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "peergateway",
            RegisteredServices = new List<RegisteredService>(),
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "printer.local", Port = 9100 },
                new AllowedDestination { HostPattern = "nas.home.arpa", Port = 22 }
            },
            MaxConcurrentTunnelsPerPeer = 2,
            IdleTimeout = TimeSpan.FromMinutes(2)
        };

        var result = PodValidation.ValidatePrivateServicePolicy(policy, members);
        Assert.True(result.IsValid);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_InvalidHostPattern_ReturnsInvalid()
    {
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "peergateway", Role = "owner" }
        };

        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "peergateway",
            RegisteredServices = new List<RegisteredService>(),
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "invalid host with spaces", Port = 80 }
            }
        };

        var result = PodValidation.ValidatePrivateServicePolicy(policy, members);
        Assert.False(result.IsValid);
        Assert.Contains("Invalid HostPattern format", result.Error);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_InvalidPort_ReturnsInvalid()
    {
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "peergateway", Role = "owner" }
        };

        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "peergateway",
            RegisteredServices = new List<RegisteredService>(),
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "example.com", Port = 99999 }
            }
        };

        var result = PodValidation.ValidatePrivateServicePolicy(policy, members);
        Assert.False(result.IsValid);
        Assert.Contains("Port must be between 1 and 65535", result.Error);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_NonMemberGateway_ReturnsInvalid()
    {
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "peermember", Role = "member" }
        };

        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "peernonmember",
            RegisteredServices = new List<RegisteredService>(),
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "printer.local", Port = 9100 }
            }
        };

        var result = PodValidation.ValidatePrivateServicePolicy(policy, members);
        Assert.False(result.IsValid);
        Assert.Contains("GatewayPeerId must be a pod member", result.Error);
    }

    [Fact]
    public void ValidateAllowedDestination_ValidDestination_ReturnsValid()
    {
        var destination = new AllowedDestination
        {
            HostPattern = "printer.local",
            Port = 9100,
            Protocol = "tcp",
            AllowPublic = false
        };

        var result = PodValidation.ValidateAllowedDestination(destination);
        Assert.True(result.IsValid);
        Assert.Empty(result.Error);
    }

    [Fact]
    public void ValidateAllowedDestination_WildcardPattern_ReturnsInvalid()
    {
        var destination = new AllowedDestination
        {
            HostPattern = "*.home.arpa",
            Port = 22,
            Protocol = "tcp",
            AllowPublic = false
        };

        var result = PodValidation.ValidateAllowedDestination(destination);
        Assert.False(result.IsValid);
        Assert.Contains("Wildcards", result.Error);
    }

    [Fact]
    public void ValidateAllowedDestination_PublicNotAllowed_ReturnsInvalid()
    {
        var destination = new AllowedDestination
        {
            HostPattern = "example.com",
            Port = 443,
            Protocol = "tcp",
            AllowPublic = true
        };

        var result = PodValidation.ValidateAllowedDestination(destination);
        Assert.False(result.IsValid);
        Assert.Contains("Public destinations are not allowed", result.Error);
    }

    [Fact]
    public void ValidateAllowedDestination_InvalidProtocol_ReturnsInvalid()
    {
        var destination = new AllowedDestination
        {
            HostPattern = "example.com",
            Port = 80,
            Protocol = "udp",
            AllowPublic = false
        };

        var result = PodValidation.ValidateAllowedDestination(destination);
        Assert.False(result.IsValid);
        Assert.Contains("Only TCP protocol is currently supported", result.Error);
    }
}
