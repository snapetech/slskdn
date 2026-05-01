// <copyright file="ArtistReleaseRadarServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Integrations.MusicBrainz.Radar;
using slskd.PodCore;
using slskd.SocialFederation;

public sealed class ArtistReleaseRadarServiceTests
{
    [Fact]
    public async Task SubscribeAsync_NormalizesSubscription()
    {
        var service = CreateService();

        var subscription = await service.SubscribeAsync(new ArtistRadarSubscription
        {
            ArtistId = " artist-1 ",
            ArtistName = " Scene Artist ",
            Scope = string.Empty,
        });

        Assert.Equal("artist-1", subscription.ArtistId);
        Assert.Equal("Scene Artist", subscription.ArtistName);
        Assert.Equal("trusted", subscription.Scope);
        Assert.Equal("artist-radar:artist-1", subscription.Id);
    }

    [Fact]
    public async Task RecordObservationAsync_RejectsObservationWithoutSongIdConfirmation()
    {
        var service = CreateService();

        var result = await service.RecordObservationAsync(CreateObservation(songIdConfirmed: false));

        Assert.False(result.Accepted);
        Assert.Equal("Observation is not SongID-confirmed.", result.RejectionReason);
    }

    [Fact]
    public async Task RecordObservationAsync_RejectsNonMusicWorkRef()
    {
        var service = CreateService();
        var observation = CreateObservation();
        observation.WorkRef.Domain = "books";

        var result = await service.RecordObservationAsync(observation);

        Assert.False(result.Accepted);
        Assert.Equal("WorkRef domain must be music.", result.RejectionReason);
    }

    [Fact]
    public async Task RecordObservationAsync_CreatesNotificationForSubscribedArtist()
    {
        var service = CreateService();
        await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1", ArtistName = "Scene Artist" });

        var result = await service.RecordObservationAsync(CreateObservation());

        Assert.True(result.Accepted);
        var notification = Assert.Single(result.Notifications);
        Assert.Equal("artist-1", notification.ArtistId);
        Assert.Equal("recording-1", notification.RecordingId);
        Assert.Equal("realm-a", notification.SourceRealm);
    }

    [Fact]
    public async Task RecordObservationAsync_SuppressesDuplicateObservationFromSameRealm()
    {
        var service = CreateService();
        await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1" });

        var first = await service.RecordObservationAsync(CreateObservation());
        var second = await service.RecordObservationAsync(CreateObservation(sourceActor: "actor-b"));

        Assert.Single(first.Notifications);
        Assert.Empty(second.Notifications);
    }

    [Fact]
    public async Task RecordObservationAsync_RespectsMutedReleaseGroup()
    {
        var service = CreateService();
        await service.SubscribeAsync(new ArtistRadarSubscription
        {
            ArtistId = "artist-1",
            MutedReleaseGroupIds = new List<string> { "release-group-1" },
        });

        var result = await service.RecordObservationAsync(CreateObservation());

        Assert.True(result.Accepted);
        Assert.Empty(result.Notifications);
    }

    [Fact]
    public async Task RecordObservationAsync_AppliesRealmScope()
    {
        var service = CreateService();
        await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1", Scope = "realm:realm-b" });

        var unmatched = await service.RecordObservationAsync(CreateObservation(sourceRealm: "realm-a"));
        var matched = await service.RecordObservationAsync(CreateObservation(recordingId: "recording-2", sourceRealm: "realm-b"));

        Assert.Empty(unmatched.Notifications);
        Assert.Single(matched.Notifications);
    }

    [Fact]
    public async Task RecordObservationAsync_AppliesActorScope()
    {
        var service = CreateService();
        await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1", Scope = "actor:actor-b" });

        var unmatched = await service.RecordObservationAsync(CreateObservation(sourceActor: "actor-a"));
        var matched = await service.RecordObservationAsync(CreateObservation(recordingId: "recording-2", sourceActor: "actor-b"));

        Assert.Empty(unmatched.Notifications);
        Assert.Single(matched.Notifications);
    }

    [Fact]
    public async Task Service_RestoresSubscriptionsNotificationsAndSeenObservationKeys()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"artist-release-radar-{Guid.NewGuid():N}.json");
        try
        {
            var firstService = CreateService(storagePath);
            await firstService.SubscribeAsync(new ArtistRadarSubscription
            {
                ArtistId = "artist-1",
                ArtistName = "Scene Artist",
                MutedReleaseGroupIds = new List<string> { "muted-release-group" },
            });
            var firstObservation = await firstService.RecordObservationAsync(CreateObservation());

            var restoredService = CreateService(storagePath);
            var subscriptions = await restoredService.GetSubscriptionsAsync();
            var notifications = await restoredService.GetNotificationsAsync();
            var duplicateObservation = await restoredService.RecordObservationAsync(CreateObservation(sourceActor: "actor-b"));

            var subscription = Assert.Single(subscriptions);
            Assert.Equal("artist-1", subscription.ArtistId);
            Assert.Equal("Scene Artist", subscription.ArtistName);
            Assert.Contains("muted-release-group", subscription.MutedReleaseGroupIds);
            Assert.Single(firstObservation.Notifications);
            Assert.Single(notifications);
            Assert.Empty(duplicateObservation.Notifications);
        }
        finally
        {
            File.Delete(storagePath);
            File.Delete($"{storagePath}.tmp");
        }
    }

    [Fact]
    public async Task RouteNotificationAsync_RoutesStoredNotificationToSelectedSafePeers()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"artist-release-radar-{Guid.NewGuid():N}.json");
        var router = new Mock<IPodMessageRouter>();
        router
            .Setup(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PodMessage message, IEnumerable<string> targets, CancellationToken _) =>
                new PodMessageRoutingResult(
                    Success: true,
                    MessageId: message.MessageId,
                    PodId: message.PodId,
                    TargetPeerCount: targets.Count(),
                    SuccessfullyRoutedCount: targets.Count(),
                    FailedRoutingCount: 0,
                    RoutingDuration: TimeSpan.FromMilliseconds(5)));
        try
        {
            var service = CreateService(storagePath, router.Object);
            await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1" });
            var observation = await service.RecordObservationAsync(CreateObservation());
            var notification = Assert.Single(observation.Notifications);

            var attempt = await service.RouteNotificationAsync(
                notification.Id,
                new ArtistRadarRouteRequest
                {
                    TargetPeerIds = new List<string> { "peer-b", "peer-a", "peer-a" },
                    PodId = "realm:crate",
                    SenderPeerId = "local-peer",
                });
            var attempts = await service.GetRouteAttemptsAsync(notification.Id);

            Assert.True(attempt.Success);
            Assert.Equal(new List<string> { "peer-a", "peer-b" }, attempt.TargetPeerIds);
            Assert.Equal(attempt.TargetPeerIds, attempt.RoutedPeerIds);
            Assert.Equal("realm:crate", attempt.PodId);
            Assert.Single(attempts);
            router.Verify(service => service.RouteMessageToPeersAsync(
                It.Is<PodMessage>(message =>
                    message.PodId == "realm:crate" &&
                    message.ChannelId == $"notification:{notification.Id}" &&
                    message.Signature == "local-artist-release-radar-route" &&
                    message.Body.Contains("slskdn.artist-release-radar.observation.v1")),
                It.Is<IEnumerable<string>>(targets => targets.SequenceEqual(new List<string> { "peer-a", "peer-b" })),
                It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            File.Delete(storagePath);
            File.Delete($"{storagePath}.tmp");
        }
    }

    [Fact]
    public async Task RouteNotificationAsync_RejectsUnsafeTargetWithoutRouting()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"artist-release-radar-{Guid.NewGuid():N}.json");
        var router = new Mock<IPodMessageRouter>();
        try
        {
            var service = CreateService(storagePath, router.Object);
            await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1" });
            var observation = await service.RecordObservationAsync(CreateObservation());
            var notification = Assert.Single(observation.Notifications);

            var attempt = await service.RouteNotificationAsync(
                notification.Id,
                new ArtistRadarRouteRequest
                {
                    TargetPeerIds = new List<string> { "../peer" },
                });

            Assert.False(attempt.Success);
            Assert.Equal("Route targets must be opaque and safe.", attempt.ErrorMessage);
            router.Verify(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            File.Delete(storagePath);
            File.Delete($"{storagePath}.tmp");
        }
    }

    [Fact]
    public async Task RouteNotificationAsync_PersistsRouteAttempts()
    {
        var storagePath = Path.Combine(Path.GetTempPath(), $"artist-release-radar-{Guid.NewGuid():N}.json");
        var router = new Mock<IPodMessageRouter>();
        router
            .Setup(service => service.RouteMessageToPeersAsync(
                It.IsAny<PodMessage>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((PodMessage message, IEnumerable<string> targets, CancellationToken _) =>
                new PodMessageRoutingResult(
                    Success: false,
                    MessageId: message.MessageId,
                    PodId: message.PodId,
                    TargetPeerCount: targets.Count(),
                    SuccessfullyRoutedCount: 1,
                    FailedRoutingCount: 1,
                    RoutingDuration: TimeSpan.FromMilliseconds(5),
                    ErrorMessage: "partial route failure",
                    FailedPeerIds: new List<string> { "peer-b" }));
        try
        {
            var service = CreateService(storagePath, router.Object);
            await service.SubscribeAsync(new ArtistRadarSubscription { ArtistId = "artist-1" });
            var observation = await service.RecordObservationAsync(CreateObservation());
            var notification = Assert.Single(observation.Notifications);

            await service.RouteNotificationAsync(
                notification.Id,
                new ArtistRadarRouteRequest { TargetPeerIds = new List<string> { "peer-a", "peer-b" } });

            var reloaded = CreateService(storagePath);
            var attempts = await reloaded.GetRouteAttemptsAsync(notification.Id);
            var attempt = Assert.Single(attempts);
            Assert.False(attempt.Success);
            Assert.Equal(new List<string> { "peer-b" }, attempt.FailedPeerIds);
            Assert.Equal(new List<string> { "peer-a" }, attempt.RoutedPeerIds);
        }
        finally
        {
            File.Delete(storagePath);
            File.Delete($"{storagePath}.tmp");
        }
    }

    private static ArtistReleaseRadarService CreateService()
    {
        return CreateService(Path.Combine(Path.GetTempPath(), $"artist-release-radar-{Guid.NewGuid():N}.json"));
    }

    private static ArtistReleaseRadarService CreateService(string storagePath)
    {
        return new ArtistReleaseRadarService(NullLogger<ArtistReleaseRadarService>.Instance, storagePath);
    }

    private static ArtistReleaseRadarService CreateService(string storagePath, IPodMessageRouter messageRouter)
    {
        return new ArtistReleaseRadarService(NullLogger<ArtistReleaseRadarService>.Instance, storagePath, messageRouter);
    }

    private static ArtistRadarObservation CreateObservation(
        string artistId = "artist-1",
        string recordingId = "recording-1",
        string sourceRealm = "realm-a",
        string sourceActor = "actor-a",
        bool songIdConfirmed = true)
    {
        return new ArtistRadarObservation
        {
            ArtistId = artistId,
            RecordingId = recordingId,
            ReleaseId = "release-1",
            ReleaseGroupId = "release-group-1",
            SourceRealm = sourceRealm,
            SourceActor = sourceActor,
            SongIdConfirmed = songIdConfirmed,
            Confidence = 0.95,
            WorkRef = new WorkRef
            {
                Id = "https://realm.example/works/rare-track",
                Domain = "music",
                Title = "Rare Track",
                Creator = "Scene Artist",
            },
            ObservedAt = new DateTimeOffset(2026, 4, 30, 20, 0, 0, TimeSpan.Zero),
        };
    }
}
