// <copyright file="AnonymousControlPlaneControllerAuthTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Security;

using System;
using System.Linq;
using System.Reflection;
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
            typeof(CanonicalController),
            typeof(DedupeController),
            typeof(DescriptorRetrieverController),
            typeof(PodDhtController),
            typeof(PodDiscoveryController),
            typeof(PodVerificationController),
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

    [Fact]
    public void ExplicitlyPublicProtocolActions_AreTheOnlyAnonymousActionsOnReviewedControllers()
    {
        // HARDENING-2026-04-20 H3: DescriptorRetrieverController no longer has AllowAnonymous methods.
        // The class-level [Authorize(Policy = AuthPolicy.Any)] now governs every action.
        AssertAnonymousActions(typeof(DescriptorRetrieverController));

        AssertAnonymousActions(
            typeof(PodDhtController),
            nameof(PodDhtController.GetPodMetadata));

        AssertAnonymousActions(
            typeof(PodDiscoveryController),
            nameof(PodDiscoveryController.DiscoverPodsByName),
            nameof(PodDiscoveryController.DiscoverPodsByTag),
            nameof(PodDiscoveryController.DiscoverPodsByTags),
            nameof(PodDiscoveryController.DiscoverAllPods));

        AssertAnonymousActions(
            typeof(PodVerificationController),
            nameof(PodVerificationController.VerifyMembership),
            nameof(PodVerificationController.VerifyMessage),
            nameof(PodVerificationController.CheckRole));
    }

    private static void AssertAnonymousActions(Type controllerType, params string[] expectedAnonymousActionNames)
    {
        var publicInstanceActions = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.IsPublic && !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes().Any(attribute => attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal)))
            .ToArray();

        var actualAnonymousActionNames = publicInstanceActions
            .Where(method => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any())
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = expectedAnonymousActionNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actualAnonymousActionNames);
    }
}
