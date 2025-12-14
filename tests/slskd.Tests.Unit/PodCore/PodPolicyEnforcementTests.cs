// <copyright file="PodPolicyEnforcementTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.PodCore;
using System;
using System.Collections.Generic;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodPolicyEnforcementTests
{
    [Fact]
    public void ValidatePod_PrivateServiceGateway_RequiresMaxMembersLimit()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 5, // Invalid - should be ≤ 3
                GatewayPeerId = "peer-1",
                AllowedDestinations = new List<AllowedDestination>(),
                AllowPrivateRanges = true,
                AllowPublicDestinations = false
            }
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePod(pod, currentMembers));

        Assert.Contains("MaxMembers", exception.Message);
        Assert.Contains("3", exception.Message);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_ValidMaxMembers_Succeeds()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3, // Valid
                GatewayPeerId = "peer-1",
                AllowedDestinations = new List<AllowedDestination>(),
                AllowPrivateRanges = true,
                AllowPublicDestinations = false
            }
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        Assert.Null(Record.Exception(() => PodValidation.ValidatePod(pod, currentMembers)));
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_ExceedsCurrentMembers_Fails()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 2,
                GatewayPeerId = "peer-1",
                AllowedDestinations = new List<AllowedDestination>(),
                AllowPrivateRanges = true,
                AllowPublicDestinations = false
            }
        };

        // Current members exceed the limit
        var currentMembers = new List<PodMember>
        {
            new PodMember { PeerId = "peer-1" },
            new PodMember { PeerId = "peer-2" },
            new PodMember { PeerId = "peer-3" }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePod(pod, currentMembers));

        Assert.Contains("current members", exception.Message.ToLowerInvariant());
        Assert.Contains("exceeds", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_WithoutPolicy_Fails()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = null // Missing policy
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePod(pod, currentMembers));

        Assert.Contains("PrivateServicePolicy", exception.Message);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_DisabledPolicy_Fails()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = false, // Disabled
                MaxMembers = 3,
                GatewayPeerId = "peer-1"
            }
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePod(pod, currentMembers));

        Assert.Contains("Enabled", exception.Message);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_WithoutGatewayPeerId_Fails()
    {
        // Arrange
        var pod = new Pod
        {
            PodId = "test-pod",
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3,
                GatewayPeerId = "", // Empty
                AllowedDestinations = new List<AllowedDestination>()
            }
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePod(pod, currentMembers));

        Assert.Contains("GatewayPeerId", exception.Message);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_EmptyAllowedDestinations_WarnsButAllows()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(), // Empty - should warn but allow
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        Assert.Null(Record.Exception(() =>
            PodValidation.ValidatePrivateServicePolicy(policy, currentMembers)));
    }

    [Fact]
    public void ValidatePrivateServicePolicy_InvalidHostPattern_Fails()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination
                {
                    HostPattern = "*.example.com", // Invalid - broad wildcard
                    Port = 80,
                    Protocol = "tcp"
                }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePrivateServicePolicy(policy, currentMembers));

        Assert.Contains("HostPattern", exception.Message);
        Assert.Contains("wildcard", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public void ValidatePrivateServicePolicy_ValidHostPatterns_Succeed()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination
                {
                    HostPattern = "api.example.com", // Valid - exact match
                    Port = 443,
                    Protocol = "tcp"
                },
                new AllowedDestination
                {
                    HostPattern = "*.api.example.com", // Valid - single suffix wildcard
                    Port = 80,
                    Protocol = "tcp"
                }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        Assert.Null(Record.Exception(() =>
            PodValidation.ValidatePrivateServicePolicy(policy, currentMembers)));
    }

    [Fact]
    public void ValidatePrivateServicePolicy_InvalidPort_Fails()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination
                {
                    HostPattern = "api.example.com",
                    Port = 70000, // Invalid - too high
                    Protocol = "tcp"
                }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePrivateServicePolicy(policy, currentMembers));

        Assert.Contains("Port", exception.Message);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_RegisteredServiceWithInvalidPattern_Fails()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService
                {
                    Name = "Test Service",
                    Description = "Test description",
                    Kind = ServiceKind.WebInterface,
                    DestinationHost = "*.example.com", // Invalid pattern
                    DestinationPort = 80,
                    Protocol = "tcp"
                }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidatePrivateServicePolicy(policy, currentMembers));

        Assert.Contains("DestinationHost", exception.Message);
        Assert.Contains("pattern", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public void ValidatePrivateServicePolicy_ValidRegisteredServices_Succeed()
    {
        // Arrange
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService
                {
                    Name = "API Service",
                    Description = "REST API",
                    Kind = ServiceKind.WebInterface,
                    DestinationHost = "api.internal.company.com",
                    DestinationPort = 443,
                    Protocol = "tcp"
                },
                new RegisteredService
                {
                    Name = "Database",
                    Description = "PostgreSQL DB",
                    Kind = ServiceKind.Database,
                    DestinationHost = "db.internal.company.com",
                    DestinationPort = 5432,
                    Protocol = "tcp"
                }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var currentMembers = new List<PodMember>();

        // Act & Assert
        Assert.Null(Record.Exception(() =>
            PodValidation.ValidatePrivateServicePolicy(policy, currentMembers)));
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_RequiresPolicy()
    {
        // Arrange
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };
        PodPrivateServicePolicy? policy = null;
        int memberCount = 0;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidateCapabilities(capabilities, policy, memberCount));

        Assert.Contains("PrivateServicePolicy", exception.Message);
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_ValidPolicy_Succeeds()
    {
        // Arrange
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        int memberCount = 0;

        // Act & Assert
        Assert.Null(Record.Exception(() =>
            PodValidation.ValidateCapabilities(capabilities, policy, memberCount)));
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_ExceedsMaxMembers_Fails()
    {
        // Arrange
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 5, // Invalid
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        int memberCount = 0;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidateCapabilities(capabilities, policy, memberCount));

        Assert.Contains("MaxMembers", exception.Message);
        Assert.Contains("3", exception.Message);
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_CurrentMembersExceedLimit_Fails()
    {
        // Arrange
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 2,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        int memberCount = 3; // Exceeds limit

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            PodValidation.ValidateCapabilities(capabilities, policy, memberCount));

        Assert.Contains("member count", exception.Message.ToLowerInvariant());
        Assert.Contains("exceeds", exception.Message.ToLowerInvariant());
    }

    [Fact]
    public void IsValidHostPattern_ValidPatterns_ReturnTrue()
    {
        // Arrange
        var validPatterns = new[]
        {
            "example.com",
            "api.example.com",
            "*.example.com",
            "sub.*.example.com",
            "192.168.1.1",
            "10.0.0.0",
            "::1",
            "2001:db8::1"
        };

        // Act & Assert
        foreach (var pattern in validPatterns)
        {
            Assert.True(PodValidation.IsValidHostPattern(pattern), $"Pattern '{pattern}' should be valid");
        }
    }

    [Fact]
    public void IsValidHostPattern_InvalidPatterns_ReturnFalse()
    {
        // Arrange
        var invalidPatterns = new[]
        {
            "", // Empty
            "*.*", // Too broad
            "*.com", // Too broad
            "*example.com", // Invalid wildcard
            "exam*ple.com", // Invalid wildcard
            "exam?ple.com", // Invalid character
            "exam ple.com", // Space
            "exam\tple.com", // Tab
            "exam\nple.com", // Newline
            "exam\rple.com", // Carriage return
            "exam\x00ple.com", // Null byte
            new string('a', 256) // Too long
        };

        // Act & Assert
        foreach (var pattern in invalidPatterns)
        {
            Assert.False(PodValidation.IsValidHostPattern(pattern), $"Pattern '{pattern}' should be invalid");
        }
    }

    [Fact]
    public void IsValidPort_ValidPorts_ReturnTrue()
    {
        // Arrange
        var validPorts = new[] { 1, 80, 443, 8080, 65535 };

        // Act & Assert
        foreach (var port in validPorts)
        {
            Assert.True(PodValidation.IsValidPort(port), $"Port {port} should be valid");
        }
    }

    [Fact]
    public void IsValidPort_InvalidPorts_ReturnFalse()
    {
        // Arrange
        var invalidPorts = new[] { 0, -1, 65536, 100000 };

        // Act & Assert
        foreach (var port in invalidPorts)
        {
            Assert.False(PodValidation.IsValidPort(port), $"Port {port} should be invalid");
        }
    }

    [Fact]
    public void IsPrivateAddress_PrivateRanges_ReturnTrue()
    {
        // Arrange
        var privateAddresses = new[]
        {
            "192.168.1.1",
            "10.0.0.1",
            "172.16.0.1",
            "172.31.255.255",
            "fc00::1",
            "fd00::1",
            "fe80::1"
        };

        // Act & Assert
        foreach (var address in privateAddresses)
        {
            Assert.True(PodValidation.IsPrivateAddress(address), $"Address '{address}' should be private");
        }
    }

    [Fact]
    public void IsPrivateAddress_PublicRanges_ReturnFalse()
    {
        // Arrange
        var publicAddresses = new[]
        {
            "8.8.8.8",
            "1.1.1.1",
            "2001:4860:4860::8888",
            "169.254.1.1", // Link-local, not private
            "224.0.0.1", // Multicast
            "127.0.0.1" // Loopback
        };

        // Act & Assert
        foreach (var address in publicAddresses)
        {
            Assert.False(PodValidation.IsPrivateAddress(address), $"Address '{address}' should not be private");
        }
    }

    [Fact]
    public void IsBlockedAddress_DangerousAddresses_ReturnTrue()
    {
        // Arrange
        var blockedAddresses = new[]
        {
            "127.0.0.1", // Loopback
            "169.254.169.254", // Cloud metadata
            "169.254.1.1", // Link-local
            "224.0.0.1", // Multicast
            "255.255.255.255", // Broadcast
            "::1", // IPv6 loopback
            "fe80::1", // IPv6 link-local
            "ff00::1" // IPv6 multicast
        };

        // Act & Assert
        foreach (var address in blockedAddresses)
        {
            Assert.True(PodValidation.IsBlockedAddress(address), $"Address '{address}' should be blocked");
        }
    }

    [Fact]
    public void IsBlockedAddress_SafeAddresses_ReturnFalse()
    {
        // Arrange
        var safeAddresses = new[]
        {
            "192.168.1.1", // Private
            "8.8.8.8", // Public
            "10.0.0.1", // Private
            "2001:db8::1" // Public IPv6
        };

        // Act & Assert
        foreach (var address in safeAddresses)
        {
            Assert.False(PodValidation.IsBlockedAddress(address), $"Address '{address}' should not be blocked");
        }
    }

    [Fact]
    public void IsKnownProxyPort_CommonProxyPorts_ReturnTrue()
    {
        // Arrange
        var proxyPorts = new[] { 3128, 8080, 8118, 9050, 1080 };

        // Act & Assert
        foreach (var port in proxyPorts)
        {
            Assert.True(PodValidation.IsKnownProxyPort(port), $"Port {port} should be recognized as proxy port");
        }
    }

    [Fact]
    public void IsKnownProxyPort_NonProxyPorts_ReturnFalse()
    {
        // Arrange
        var nonProxyPorts = new[] { 80, 443, 22, 5432, 3306 };

        // Act & Assert
        foreach (var port in nonProxyPorts)
        {
            Assert.False(PodValidation.IsKnownProxyPort(port), $"Port {port} should not be recognized as proxy port");
        }
    }
}

