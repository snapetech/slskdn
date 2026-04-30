// <copyright file="PodPrivateServicePolicyTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Xunit;
using slskd.PodCore;

namespace slskd.Tests.Unit.PodCore;

public class PodPrivateServicePolicyTests
{
    [Fact]
    public void EnforceDestinationPolicy_BlocksUnauthorizedAccess()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "gateway-peer",
            AllowedDestinations = new List<AllowedDestination>()
        };

        var result = PodValidation.ValidatePrivateServicePolicy(
            policy,
            new List<PodMember> { new PodMember { PeerId = "gateway-peer" } });

        Assert.False(result.IsValid);
        Assert.Contains("without registered services or allowed destinations", result.Error);
    }

    [Fact]
    public void ValidateServicePermissions_AllowsAuthorizedRequests()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "gateway-peer",
            RegisteredServices = new List<RegisteredService>
            {
                new RegisteredService
                {
                    Name = "lidarr",
                    Host = "127.0.0.1",
                    Port = 8686
                }
            }
        };

        var result = PodValidation.ValidatePrivateServicePolicy(
            policy,
            new List<PodMember> { new PodMember { PeerId = "gateway-peer" } });

        Assert.True(result.IsValid, result.Error);
    }

    [Fact]
    public void AuditPolicyViolations_LogsSecurityEvents()
    {
        var policy = new PodPrivateServicePolicy
        {
            Enabled = true,
            GatewayPeerId = "missing-gateway",
            AllowedDestinations = new List<AllowedDestination>
            {
                new AllowedDestination { HostPattern = "127.0.0.1", Port = 8686 }
            }
        };

        var result = PodValidation.ValidatePrivateServicePolicy(
            policy,
            new List<PodMember> { new PodMember { PeerId = "other-peer" } });

        Assert.False(result.IsValid);
        Assert.Contains("GatewayPeerId must be a pod member", result.Error);
    }
}
