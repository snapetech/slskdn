// <copyright file="ActivityDeliveryServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#nullable enable

namespace slskd.Tests.Unit.SocialFederation;

using System;
using System.Net.Http;
using System.Reflection;
using slskd.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.SocialFederation;
using Xunit;

public class ActivityDeliveryServiceTests
{
    [Fact]
    public void Constructor_WithSmallHourlyLimit_DoesNotThrow()
    {
        var ex = Record.Exception(() => CreateService(maxActivitiesPerHour: 2));

        Assert.Null(ex);
    }

    [Fact]
    public void IsRateLimited_WithMultipleRecentDeliveriesForSameInbox_RespectsConfiguredLimit()
    {
        using var service = CreateService(maxActivitiesPerHour: 2);
        var recordDelivery = typeof(ActivityDeliveryService).GetMethod("RecordDelivery", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var isRateLimited = typeof(ActivityDeliveryService).GetMethod("IsRateLimited", BindingFlags.Instance | BindingFlags.NonPublic)!;

        recordDelivery.Invoke(service, ["https://remote.example/inbox"]);
        recordDelivery.Invoke(service, ["https://remote.example/inbox"]);

        var limited = (bool)isRateLimited.Invoke(service, ["https://remote.example/inbox"])!;

        Assert.True(limited);
    }

    private static ActivityDeliveryService CreateService(int maxActivitiesPerHour)
    {
        var httpClient = new HttpClient();
        var federationOptions = CreateOptions(new SocialFederationOptions
        {
            BaseUrl = "https://local.example",
        });
        var publishingOptions = CreateOptions(new FederationPublishingOptions
        {
            MaxActivitiesPerHour = maxActivitiesPerHour,
            DeliveryTimeoutSeconds = 30,
            MaxDeliveryRetries = 0,
        });

        return new ActivityDeliveryService(
            httpClient,
            federationOptions,
            publishingOptions,
            Mock.Of<IActivityPubKeyStore>(),
            Mock.Of<ILogger<ActivityDeliveryService>>());
    }

    private static IOptionsMonitor<T> CreateOptions<T>(T value)
        where T : class
    {
        var mock = new Mock<IOptionsMonitor<T>>();
        mock.SetupGet(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }
}
