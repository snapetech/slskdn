// <copyright file="DestinationAllowlistUnitTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.PodCore;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// Unit tests for destination allowlist logic (MatchesDestination, ValidateDestinationAgainstPolicy).
/// These replicate the behavior from PrivateGatewayMeshService for testing without service infrastructure.
/// OpenTunnel integration tests remain in DestinationAllowlistTests (excluded; need PrivateGatewayMeshService, DnsSecurityService, etc.).
/// </summary>
public class DestinationAllowlistUnitTests
{
    [Fact]
    public void MatchesDestination_ExactHostnameMatch_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "api.example.com", 443));
    }

    [Fact]
    public void MatchesDestination_WildcardHostnameMatch_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "*.api.example.com", Port = 443, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "service.api.example.com", 443));
    }

    [Fact]
    public void MatchesDestination_HostnameNotInAllowlist_ReturnsFalse()
    {
        var allowed = new AllowedDestination { HostPattern = "api.example.com", Port = 80, Protocol = "tcp" };
        Assert.False(TestMatchesDestination(allowed, "evil.com", 80));
    }

    [Fact]
    public void MatchesDestination_PortMismatch_ReturnsFalse()
    {
        var allowed = new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" };
        Assert.False(TestMatchesDestination(allowed, "api.example.com", 80));
    }

    [Fact]
    public void MatchesDestination_CaseInsensitiveMatch_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "API.EXAMPLE.COM", Port = 443, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "api.example.com", 443));
    }

    [Fact]
    public void MatchesDestination_WildcardAtStart_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "*.example.com", Port = 80, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "sub.example.com", 80));
    }

    [Fact]
    public void MatchesDestination_WildcardInMiddle_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "api.*.example.com", Port = 80, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "api.service.example.com", 80));
    }

    [Fact]
    public void MatchesDestination_MultipleWildcards_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "*.*.example.com", Port = 80, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "api.service.example.com", 80));
    }

    [Fact]
    public void MatchesDestination_WildcardNoMatch_ReturnsFalse()
    {
        var allowed = new AllowedDestination { HostPattern = "*.example.com", Port = 80, Protocol = "tcp" };
        Assert.False(TestMatchesDestination(allowed, "evil.com", 80));
    }

    [Fact]
    public void MatchesDestination_IpAddressExactMatch_ReturnsTrue()
    {
        var allowed = new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" };
        Assert.True(TestMatchesDestination(allowed, "192.168.1.100", 80));
    }

    [Fact]
    public void MatchesDestination_IpAddressMismatch_ReturnsFalse()
    {
        var allowed = new AllowedDestination { HostPattern = "192.168.1.100", Port = 80, Protocol = "tcp" };
        Assert.False(TestMatchesDestination(allowed, "192.168.1.101", 80));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_PrivateIpAllowed_ReturnsTrue()
    {
        var policy = new PodPrivateServicePolicy { AllowPrivateRanges = true, AllowPublicDestinations = false, AllowedDestinations = new List<AllowedDestination>() };
        Assert.True(ValidateDestinationAgainstPolicy("192.168.1.100", 80, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_PrivateIpNotAllowed_ReturnsFalse()
    {
        var policy = new PodPrivateServicePolicy { AllowPrivateRanges = false, AllowPublicDestinations = false, AllowedDestinations = new List<AllowedDestination>() };
        Assert.False(ValidateDestinationAgainstPolicy("192.168.1.100", 80, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_PublicIpAllowed_ReturnsTrue()
    {
        var policy = new PodPrivateServicePolicy { AllowPrivateRanges = false, AllowPublicDestinations = true, AllowedDestinations = new List<AllowedDestination>() };
        Assert.True(ValidateDestinationAgainstPolicy("8.8.8.8", 53, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_PublicIpNotAllowed_ReturnsFalse()
    {
        var policy = new PodPrivateServicePolicy { AllowPrivateRanges = false, AllowPublicDestinations = false, AllowedDestinations = new List<AllowedDestination>() };
        Assert.False(ValidateDestinationAgainstPolicy("8.8.8.8", 53, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_BlockedAddress_ReturnsFalse()
    {
        var policy = new PodPrivateServicePolicy { AllowPrivateRanges = true, AllowPublicDestinations = true, AllowedDestinations = new List<AllowedDestination>() };
        Assert.False(ValidateDestinationAgainstPolicy("127.0.0.1", 80, policy));
        Assert.False(ValidateDestinationAgainstPolicy("169.254.169.254", 80, policy));
        Assert.False(ValidateDestinationAgainstPolicy("169.254.1.1", 80, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_RegisteredServiceMatch_ReturnsTrue()
    {
        var policy = new PodPrivateServicePolicy
        {
            AllowPrivateRanges = false,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "Test DB", Host = "db.internal.company.com", Port = 5432, Protocol = "tcp" }
            }
        };
        Assert.True(ValidateDestinationAgainstPolicy("db.internal.company.com", 5432, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_RegisteredServicePortMismatch_ReturnsFalse()
    {
        var policy = new PodPrivateServicePolicy
        {
            AllowPrivateRanges = false,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "Test DB", Host = "db.internal.company.com", Port = 5432, Protocol = "tcp" }
            }
        };
        Assert.False(ValidateDestinationAgainstPolicy("db.internal.company.com", 3306, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_InAllowlist_ReturnsTrue()
    {
        var policy = new PodPrivateServicePolicy
        {
            AllowPrivateRanges = false,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" } },
            RegisteredServices = new List<RegisteredService>()
        };
        Assert.True(ValidateDestinationAgainstPolicy("api.example.com", 443, policy));
    }

    [Fact]
    public void ValidateDestinationAgainstPolicy_NotInAllowlist_ReturnsFalse()
    {
        var policy = new PodPrivateServicePolicy
        {
            AllowPrivateRanges = false,
            AllowPublicDestinations = false,
            AllowedDestinations = new List<AllowedDestination> { new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" } },
            RegisteredServices = new List<RegisteredService>()
        };
        Assert.False(ValidateDestinationAgainstPolicy("evil.com", 80, policy));
    }

    // Helpers that replicate PrivateGatewayMeshService allowlist logic

    private static bool TestMatchesDestination(AllowedDestination allowed, string host, int port)
    {
        if (allowed.Port != port) return false;
        if (allowed.HostPattern.Contains('*'))
        {
            var pattern = "^" + Regex.Escape(allowed.HostPattern).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(host, pattern, RegexOptions.IgnoreCase);
        }
        return string.Equals(allowed.HostPattern, host, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateDestinationAgainstPolicy(string host, int port, PodPrivateServicePolicy policy)
    {
        foreach (var allowed in policy.AllowedDestinations ?? [])
        {
            if (TestMatchesDestination(allowed, host, port)) return true;
        }
        foreach (var service in policy.RegisteredServices ?? [])
        {
            if (string.Equals(service.Host, host, StringComparison.OrdinalIgnoreCase) && service.Port == port)
                return true;
        }
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsBlockedAddress(ip)) return false;
            if (IsPrivateAddress(ip)) return policy.AllowPrivateRanges;
            return policy.AllowPublicDestinations;
        }
        return false;
    }

    private static bool IsPrivateAddress(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return (b[0] == 10) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            return (b[0] & 0xfe) == 0xfc;
        }
        return false;
    }

    private static bool IsBlockedAddress(IPAddress ip)
    {
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 127) return true;
            if (b[0] == 169 && b[1] == 254) return true;
            if (b[0] >= 224 && b[0] <= 239) return true;
        }
        else if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var b = ip.GetAddressBytes();
            if (ip.Equals(IPAddress.IPv6Loopback)) return true;
            if ((b[0] & 0xff) == 0xfe && (b[1] & 0xc0) == 0x80) return true;
            if (b[0] == 0xff) return true;
        }
        return false;
    }
}
