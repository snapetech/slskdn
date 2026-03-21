// <copyright file="PublicProtocolAnonymousActionTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Security;

using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using slskd.Core.API;
using slskd.Identity.API;
using slskd.SocialFederation.API;
using slskd.Streaming;
using Xunit;

public class PublicProtocolAnonymousActionTests
{
    [Fact]
    public void SessionController_OnlyPublicBootstrapActions_AreAnonymous()
    {
        AssertAnonymousActions(
            typeof(SessionController),
            nameof(SessionController.Enabled),
            nameof(SessionController.Login));
    }

    [Fact]
    public void ProfileController_OnlyPublicProfileLookup_IsAnonymous()
    {
        AssertAnonymousActions(
            typeof(ProfileController),
            nameof(ProfileController.GetProfile));
    }

    [Fact]
    public void StreamingController_RemainsAnonymousForTokenBackedTransport()
    {
        AssertControllerAllowsAnonymous(typeof(StreamsController));
        AssertAnonymousActions(typeof(StreamsController), nameof(StreamsController.Get));
    }

    [Fact]
    public void FederationControllers_RemainAnonymousForProtocolDiscoveryAndDelivery()
    {
        AssertControllerAllowsAnonymous(typeof(ActivityPubController));
        AssertAnonymousActions(
            typeof(ActivityPubController),
            nameof(ActivityPubController.GetActor),
            nameof(ActivityPubController.GetInbox),
            nameof(ActivityPubController.PostToInbox),
            nameof(ActivityPubController.GetOutbox),
            nameof(ActivityPubController.PostToOutbox));

        AssertControllerAllowsAnonymous(typeof(WebFingerController));
        AssertAnonymousActions(
            typeof(WebFingerController),
            nameof(WebFingerController.GetWebFinger));
    }

    private static void AssertControllerAllowsAnonymous(Type controllerType)
    {
        var hasAllowAnonymous = controllerType
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Any();

        Assert.True(hasAllowAnonymous, $"{controllerType.Name} should allow anonymous access at controller scope.");
    }

    private static void AssertAnonymousActions(Type controllerType, params string[] expectedAnonymousActionNames)
    {
        var publicInstanceActions = controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.IsPublic && !method.IsSpecialName)
            .Where(method => method.GetCustomAttributes().Any(attribute => attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal)))
            .ToArray();

        var actualAnonymousActionNames = publicInstanceActions
            .Where(method => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any()
                || controllerType.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Any())
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var expected = expectedAnonymousActionNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actualAnonymousActionNames);
    }
}
