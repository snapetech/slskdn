// <copyright file="PodPolicyEnforcementTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

public class PodPolicyEnforcementTests
{
    private const string ValidPodId = "pod:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ValidatePod_PrivateServiceGateway_RequiresMaxMembersLimit()
    {
        var pod = new Pod
        {
            PodId = ValidPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 5, // Invalid - MVP allows max 3
                GatewayPeerId = "peer-1",
                AllowedDestinations = new List<AllowedDestination>(),
                AllowPrivateRanges = true,
                AllowPublicDestinations = false
            }
        };

        var (isValid, error) = PodValidation.ValidatePod(pod, new List<PodMember>());

        Assert.False(isValid);
        Assert.Contains("3", error);
        Assert.Contains("maximum", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_ValidMaxMembers_SucceedsWithEmptyMembers_CreateThenJoin()
    {
        // Create-then-join: ValidatePrivateServicePolicy(policy, []) allows empty members; gateway joins first.
        var pod = new Pod
        {
            PodId = ValidPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3,
                GatewayPeerId = "peer-1",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" }
                },
                RegisteredServices = new List<RegisteredService>(),
                AllowPrivateRanges = true,
                AllowPublicDestinations = false
            }
        };

        var (isValid, _) = PodValidation.ValidatePod(pod, new List<PodMember>());

        Assert.True(isValid);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_ExceedsCurrentMembers_Fails()
    {
        // ValidatePod(pod, members) -> ValidateCapabilities(..., memberCount: 4) fails at memberCount > policy.MaxMembers.
        var pod = new Pod
        {
            PodId = ValidPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3,
                GatewayPeerId = "peer-gateway",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" }
                },
                RegisteredServices = new List<RegisteredService>(),
                AllowPrivateRanges = true,
                AllowPublicDestinations = false
            }
        };
        var members = new List<PodMember>
        {
            new PodMember { PeerId = "peer-gateway" },
            new PodMember { PeerId = "peer-2" },
            new PodMember { PeerId = "peer-3" },
            new PodMember { PeerId = "peer-4" }
        };

        var (isValid, error) = PodValidation.ValidatePod(pod, members);

        Assert.False(isValid);
        Assert.Contains("members", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maximum", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3", error);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_WithoutPolicy_Fails()
    {
        var pod = new Pod
        {
            PodId = ValidPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = null
        };

        var (isValid, error) = PodValidation.ValidatePod(pod, new List<PodMember>());

        Assert.False(isValid);
        Assert.Contains("policy", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_DisabledPolicy_Fails()
    {
        var pod = new Pod
        {
            PodId = ValidPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = false,
                MaxMembers = 3,
                GatewayPeerId = "peer-1"
            }
        };

        var (isValid, error) = PodValidation.ValidatePod(pod, new List<PodMember>());

        Assert.False(isValid);
        Assert.Contains("Enabled", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePod_PrivateServiceGateway_WithoutGatewayPeerId_Fails()
    {
        var pod = new Pod
        {
            PodId = ValidPodId,
            Name = "Test Pod",
            Capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway },
            PrivateServicePolicy = new PodPrivateServicePolicy
            {
                Enabled = true,
                MaxMembers = 3,
                GatewayPeerId = "",
                AllowedDestinations = new List<AllowedDestination>
                {
                    new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" }
                }
            }
        };

        var (isValid, error) = PodValidation.ValidatePod(pod, new List<PodMember>());

        Assert.False(isValid);
        Assert.Contains("gateway", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_EmptyAllowedDestinations_WithRegisteredService_Succeeds()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "Svc", Host = "api.example.com", Port = 443, Protocol = "tcp" }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        var members = new List<PodMember> { new PodMember { PeerId = "peer-1" } };

        var (isValid, error) = PodValidation.ValidatePrivateServicePolicy(policy, members);

        Assert.True(isValid);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_InvalidHostPattern_Fails()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "*.example.com", Port = 80, Protocol = "tcp" }
            },
            RegisteredServices = new List<RegisteredService>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        var members = new List<PodMember> { new PodMember { PeerId = "peer-1" } };

        var (isValid, error) = PodValidation.ValidatePrivateServicePolicy(policy, members);

        Assert.False(isValid);
        Assert.Contains("wildcard", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_ValidExactHostPatterns_Succeed()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" },
                new AllowedDestination { HostPattern = "db.example.com", Port = 5432, Protocol = "tcp" }
            },
            RegisteredServices = new List<RegisteredService>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        var members = new List<PodMember> { new PodMember { PeerId = "peer-1" } };

        var (isValid, error) = PodValidation.ValidatePrivateServicePolicy(policy, members);

        Assert.True(isValid);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_InvalidPort_Fails()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "api.example.com", Port = 70000, Protocol = "tcp" }
            },
            RegisteredServices = new List<RegisteredService>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        var members = new List<PodMember> { new PodMember { PeerId = "peer-1" } };

        var (isValid, error) = PodValidation.ValidatePrivateServicePolicy(policy, members);

        Assert.False(isValid);
        Assert.Contains("Port", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_RegisteredServiceWithInvalidPattern_Fails()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "Svc", Host = "*.example.com", Port = 80, Protocol = "tcp" }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        var members = new List<PodMember> { new PodMember { PeerId = "peer-1" } };

        var (isValid, error) = PodValidation.ValidatePrivateServicePolicy(policy, members);

        Assert.False(isValid);
        Assert.Contains("Wildcards", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidatePrivateServicePolicy_ValidRegisteredServices_Succeed()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService { Name = "API", Description = "REST", Kind = ServiceKind.WebInterface, Host = "api.internal.company.com", Port = 443, Protocol = "tcp" },
                new RegisteredService { Name = "DB", Description = "PostgreSQL", Kind = ServiceKind.Database, Host = "db.internal.company.com", Port = 5432, Protocol = "tcp" }
            },
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };
        var members = new List<PodMember> { new PodMember { PeerId = "peer-1" } };

        var (isValid, error) = PodValidation.ValidatePrivateServicePolicy(policy, members);

        Assert.True(isValid);
        Assert.Empty(error);
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_RequiresPolicy()
    {
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };

        var (isValid, error) = PodValidation.ValidateCapabilities(capabilities, null, memberCount: 0);

        Assert.False(isValid);
        Assert.Contains("policy", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_WithZeroMembers_Succeeds_CreateThenJoin()
    {
        // Create-then-join: ValidatePrivateServicePolicy(policy, []) allows empty; gateway joins first.
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 3,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "api.example.com", Port = 443, Protocol = "tcp" }
            },
            RegisteredServices = new List<RegisteredService>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var (isValid, _) = PodValidation.ValidateCapabilities(capabilities, policy, memberCount: 0);

        Assert.True(isValid);
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_ExceedsMaxMembers_Fails()
    {
        var capabilities = new List<PodCapability> { PodCapability.PrivateServiceGateway };
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            MaxMembers = 5,
            GatewayPeerId = "peer-1",
            AllowedDestinations = new List<AllowedDestination>(),
            AllowPrivateRanges = true,
            AllowPublicDestinations = false
        };

        var (isValid, error) = PodValidation.ValidateCapabilities(capabilities, policy, memberCount: 0);

        Assert.False(isValid);
        Assert.Contains("3", error);
        Assert.Contains("maximum", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateCapabilities_PrivateServiceGateway_CurrentMembersExceedLimit_Fails()
    {
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

        var (isValid, error) = PodValidation.ValidateCapabilities(capabilities, policy, memberCount: 3);

        Assert.False(isValid);
        Assert.Contains("member", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maximum", error, StringComparison.OrdinalIgnoreCase);
    }
}
