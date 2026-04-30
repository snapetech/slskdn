// <copyright file="ArtistReleaseRadarServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Microsoft.Extensions.Logging.Abstractions;
using slskd.Integrations.MusicBrainz.Radar;
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

    private static ArtistReleaseRadarService CreateService()
    {
        return new ArtistReleaseRadarService(NullLogger<ArtistReleaseRadarService>.Instance);
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
