// <copyright file="AnonymousControlPlaneControllerAuthTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Security;

using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using slskd.Audio.API;
using slskd.Core.Security;
using slskd.MediaCore.API.Controllers;
using slskd.PodCore.API.Controllers;
using slskd.VirtualSoulfind.v2.API;
using Xunit;

public class AnonymousControlPlaneControllerAuthTests
{
    [Fact]
    public void HighRiskControllers_RequireAuthenticatedAccess()
    {
        var controllerTypes = new[]
        {
            typeof(AnalyzerMigrationController),
            typeof(VirtualSoulfindV2Controller),
            typeof(ContentDescriptorPublisherController),
            typeof(MetadataPortabilityController),
            typeof(ContentIdController),
            typeof(IpldController),
            typeof(MediaCoreStatsController),
            typeof(PerceptualHashController),
            typeof(FuzzyMatcherController),
            typeof(PodJoinLeaveController),
            typeof(PodMessageRoutingController),
            typeof(PodMessageSigningController),
        };

        foreach (var controllerType in controllerTypes)
        {
            var authorize = controllerType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .SingleOrDefault();

            var hasAllowAnonymous = controllerType
                .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
                .Any();

            Assert.False(hasAllowAnonymous, $"{controllerType.Name} must not allow anonymous access.");
            Assert.NotNull(authorize);
            Assert.Equal(AuthPolicy.Any, authorize!.Policy);
        }
    }
}
