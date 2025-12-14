// <copyright file="IpRangeClassifierTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.Common.Security;
using System.Net;
using Xunit;

namespace slskd.Tests.Unit.Common.Security;

public class IpRangeClassifierTests
{
    [Theory]
    [InlineData("192.168.1.1", IpRangeClassifier.IpClassification.PrivateRfc1918)]
    [InlineData("10.0.0.1", IpRangeClassifier.IpClassification.PrivateRfc1918)]
    [InlineData("172.16.0.1", IpRangeClassifier.IpClassification.PrivateRfc1918)]
    [InlineData("172.31.255.255", IpRangeClassifier.IpClassification.PrivateRfc1918)]
    [InlineData("8.8.8.8", IpRangeClassifier.IpClassification.Public)]
    [InlineData("127.0.0.1", IpRangeClassifier.IpClassification.Loopback)]
    [InlineData("169.254.1.1", IpRangeClassifier.IpClassification.LinkLocal)]
    [InlineData("224.0.0.1", IpRangeClassifier.IpClassification.Multicast)]
    [InlineData("169.254.169.254", IpRangeClassifier.IpClassification.CloudMetadata)]
    [InlineData("255.255.255.255", IpRangeClassifier.IpClassification.Broadcast)]
    [InlineData("0.0.0.0", IpRangeClassifier.IpClassification.Reserved)]
    [InlineData("240.0.0.0", IpRangeClassifier.IpClassification.Reserved)]
    public void Classify_Ipv4Addresses_ReturnsCorrectClassification(string ipString, IpRangeClassifier.IpClassification expected)
    {
        // Act
        var result = IpRangeClassifier.Classify(ipString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2001:db8::1", IpRangeClassifier.IpClassification.Public)]
    [InlineData("::1", IpRangeClassifier.IpClassification.Loopback)]
    [InlineData("fe80::1", IpRangeClassifier.IpClassification.LinkLocal)]
    [InlineData("fc00::1", IpRangeClassifier.IpClassification.PrivateUla)]
    [InlineData("fd00::1", IpRangeClassifier.IpClassification.PrivateUla)]
    [InlineData("ff00::1", IpRangeClassifier.IpClassification.Multicast)]
    public void Classify_Ipv6Addresses_ReturnsCorrectClassification(string ipString, IpRangeClassifier.IpClassification expected)
    {
        // Act
        var result = IpRangeClassifier.Classify(ipString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("not.an.ip")]
    [InlineData("256.256.256.256")]
    public void Classify_InvalidAddresses_ReturnsInvalid(string ipString)
    {
        // Act
        var result = IpRangeClassifier.Classify(ipString);

        // Assert
        Assert.Equal(IpRangeClassifier.IpClassification.Invalid, result);
    }

    [Theory]
    [InlineData("192.168.1.1", true)]
    [InlineData("10.0.0.1", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("fc00::1", true)]
    [InlineData("8.8.8.8", false)]
    [InlineData("169.254.169.254", false)]
    public void IsPrivate_VariousAddresses_ReturnsCorrectResult(string ipString, bool expected)
    {
        // Act
        var result = IpRangeClassifier.IsPrivate(ipString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("169.254.1.1", true)]
    [InlineData("224.0.0.1", true)]
    [InlineData("169.254.169.254", true)]
    [InlineData("fe80::1", true)]
    [InlineData("ff00::1", true)]
    [InlineData("192.168.1.1", false)]
    [InlineData("8.8.8.8", false)]
    public void IsBlocked_VariousAddresses_ReturnsCorrectResult(string ipString, bool expected)
    {
        // Act
        var result = IpRangeClassifier.IsBlocked(ipString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("192.168.1.1", true)] // Private
    [InlineData("10.0.0.1", true)] // Private
    [InlineData("8.8.8.8", true)] // Public
    [InlineData("fc00::1", true)] // Private ULA
    [InlineData("127.0.0.1", false)] // Loopback (blocked)
    [InlineData("169.254.169.254", false)] // Cloud metadata (blocked)
    [InlineData("224.0.0.1", false)] // Multicast (blocked)
    public void IsSafeForTunneling_VariousAddresses_ReturnsCorrectResult(string ipString, bool expected)
    {
        // Act
        var result = IpRangeClassifier.IsSafeForTunneling(ipString);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Classify_NullIPAddress_ReturnsInvalid()
    {
        // Act
        var result = IpRangeClassifier.Classify((IPAddress)null!);

        // Assert
        Assert.Equal(IpRangeClassifier.IpClassification.Invalid, result);
    }

    [Fact]
    public void IsPrivate_NullIPAddress_ReturnsFalse()
    {
        // Act
        var result = IpRangeClassifier.IsPrivate((IPAddress)null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsBlocked_NullIPAddress_ReturnsFalse()
    {
        // Act
        var result = IpRangeClassifier.IsBlocked((IPAddress)null!);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(IpRangeClassifier.IpClassification.Public, "Public internet address")]
    [InlineData(IpRangeClassifier.IpClassification.PrivateRfc1918, "Private RFC1918 address (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16)")]
    [InlineData(IpRangeClassifier.IpClassification.Loopback, "Loopback address (127.0.0.0/8, ::1)")]
    [InlineData(IpRangeClassifier.IpClassification.CloudMetadata, "Cloud metadata service IP (169.254.169.254, etc.)")]
    [InlineData(IpRangeClassifier.IpClassification.Invalid, "Invalid or unparseable address")]
    public void GetDescription_VariousClassifications_ReturnsCorrectDescription(IpRangeClassifier.IpClassification classification, string expected)
    {
        // Act
        var result = IpRangeClassifier.GetDescription(classification);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Classify_AllRfc1918Ranges_ClassifiedAsPrivate()
    {
        // Test various addresses in RFC1918 ranges
        var rfc1918Addresses = new[]
        {
            "10.0.0.1",     // 10.0.0.0/8
            "10.255.255.255",
            "172.16.0.1",   // 172.16.0.0/12
            "172.31.255.255",
            "192.168.0.1",  // 192.168.0.0/16
            "192.168.255.255"
        };

        foreach (var address in rfc1918Addresses)
        {
            var classification = IpRangeClassifier.Classify(address);
            Assert.Equal(IpRangeClassifier.IpClassification.PrivateRfc1918, classification);
            Assert.True(IpRangeClassifier.IsPrivate(address));
        }
    }

    [Fact]
    public void Classify_NonRfc1918PrivateAddresses_NotClassifiedAsPrivate()
    {
        // Test addresses that are not in RFC1918 ranges
        var nonPrivateAddresses = new[]
        {
            "9.255.255.255",    // Just outside 10.0.0.0/8
            "11.0.0.0",        // Just outside 10.0.0.0/8
            "172.15.255.255",  // Just outside 172.16.0.0/12
            "172.32.0.0",      // Just outside 172.16.0.0/12
            "192.167.255.255", // Just outside 192.168.0.0/16
            "192.169.0.0"      // Just outside 192.168.0.0/16
        };

        foreach (var address in nonPrivateAddresses)
        {
            Assert.False(IpRangeClassifier.IsPrivate(address));
        }
    }

    [Fact]
    public void Classify_CloudMetadataServices_Blocked()
    {
        // Test known cloud metadata service IPs
        var metadataIPs = new[]
        {
            "169.254.169.254"  // AWS, Azure, GCP, DigitalOcean
        };

        foreach (var ip in metadataIPs)
        {
            var classification = IpRangeClassifier.Classify(ip);
            Assert.Equal(IpRangeClassifier.IpClassification.CloudMetadata, classification);
            Assert.True(IpRangeClassifier.IsBlocked(ip));
            Assert.False(IpRangeClassifier.IsSafeForTunneling(ip));
        }
    }

    [Fact]
    public void Classify_LocalhostAndLinkLocal_Blocked()
    {
        // Test localhost and link-local addresses
        var blockedAddresses = new[]
        {
            "127.0.0.1",       // IPv4 localhost
            "127.255.255.255", // IPv4 localhost range
            "169.254.1.1",     // IPv4 link-local
            "169.254.255.255", // IPv4 link-local
            "::1",             // IPv6 localhost
            "fe80::1",         // IPv6 link-local
            "fe80::ffff",      // IPv6 link-local
        };

        foreach (var address in blockedAddresses)
        {
            Assert.True(IpRangeClassifier.IsBlocked(address));
            Assert.False(IpRangeClassifier.IsSafeForTunneling(address));
        }
    }

    [Fact]
    public void Classify_MulticastAddresses_Blocked()
    {
        // Test multicast addresses
        var multicastAddresses = new[]
        {
            "224.0.0.1",       // IPv4 multicast
            "239.255.255.255", // IPv4 multicast
            "ff00::1",         // IPv6 multicast
            "ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff" // IPv6 multicast
        };

        foreach (var address in multicastAddresses)
        {
            var classification = IpRangeClassifier.Classify(address);
            Assert.Equal(IpRangeClassifier.IpClassification.Multicast, classification);
            Assert.True(IpRangeClassifier.IsBlocked(address));
            Assert.False(IpRangeClassifier.IsSafeForTunneling(address));
        }
    }
}
