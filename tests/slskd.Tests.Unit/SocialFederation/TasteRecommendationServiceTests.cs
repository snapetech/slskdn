// <copyright file="TasteRecommendationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.SocialFederation;

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.SocialFederation;

public sealed class TasteRecommendationServiceTests
{
    private readonly Mock<IActivityPubInboxStore> _inboxStore = new();
    private readonly Mock<IActivityPubRelationshipStore> _relationshipStore = new();

    [Fact]
    public async Task GetRecommendationsAsync_HidesSingleSourceCandidates()
    {
        var service = CreateService(
            trustedActors: new[] { "https://remote-one.example/actors/music" },
            entries: new[]
            {
                CreateEntry("https://remote-one.example/actors/music", "Shared Song", "Shared Artist"),
            });

        var result = await service.GetRecommendationsAsync(new TasteRecommendationRequest
        {
            MinimumTrustedSources = 2,
        });

        Assert.Equal(1, result.CandidateCount);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task GetRecommendationsAsync_SurfacesWorkRefsAfterKAnonymityThreshold()
    {
        var service = CreateService(
            trustedActors: new[]
            {
                "https://remote-one.example/actors/music",
                "https://remote-two.example/actors/music",
            },
            entries: new[]
            {
                CreateEntry("https://remote-one.example/actors/music", "Shared Song", "Shared Artist"),
                CreateEntry("https://remote-two.example/actors/music", "Shared Song", "Shared Artist"),
            });

        var result = await service.GetRecommendationsAsync(new TasteRecommendationRequest
        {
            MinimumTrustedSources = 2,
        });

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Equal("Shared Song", recommendation.WorkRef.Title);
        Assert.Equal(2, recommendation.TrustedSourceCount);
        Assert.Empty(recommendation.SourceActors);
        Assert.Contains("appeared in 2 trusted federated music libraries", recommendation.Reasons);
    }

    [Fact]
    public async Task GetRecommendationsAsync_IgnoresUntrustedInboundActors()
    {
        var service = CreateService(
            trustedActors: new[] { "https://remote-one.example/actors/music" },
            entries: new[]
            {
                CreateEntry("https://remote-one.example/actors/music", "Shared Song", "Shared Artist"),
                CreateEntry("https://unknown.example/actors/music", "Shared Song", "Shared Artist"),
            });

        var result = await service.GetRecommendationsAsync(new TasteRecommendationRequest
        {
            MinimumTrustedSources = 2,
        });

        Assert.Equal(1, result.CandidateCount);
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task GetRecommendationsAsync_OnlyNamesSourcesWhenRequested()
    {
        var service = CreateService(
            trustedActors: new[]
            {
                "https://remote-one.example/actors/music",
                "https://remote-two.example/actors/music",
            },
            entries: new[]
            {
                CreateEntry("https://remote-one.example/actors/music", "Shared Song", "Shared Artist"),
                CreateEntry("https://remote-two.example/actors/music", "Shared Song", "Shared Artist"),
            });

        var result = await service.GetRecommendationsAsync(new TasteRecommendationRequest
        {
            IncludeSourceActors = true,
            MinimumTrustedSources = 2,
        });

        var recommendation = Assert.Single(result.Recommendations);
        Assert.Contains("https://remote-one.example/actors/music", recommendation.SourceActors);
        Assert.Contains("https://remote-two.example/actors/music", recommendation.SourceActors);
    }

    private TasteRecommendationService CreateService(
        IReadOnlyList<string> trustedActors,
        IReadOnlyList<ActivityPubInboxEntry> entries)
    {
        _relationshipStore
            .Setup(store => store.GetFollowingAsync("music", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(trustedActors);
        _inboxStore
            .Setup(store => store.GetActivitiesAsync("music", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        return new TasteRecommendationService(
            _inboxStore.Object,
            _relationshipStore.Object,
            NullLogger<TasteRecommendationService>.Instance);
    }

    private static ActivityPubInboxEntry CreateEntry(string remoteActor, string title, string creator)
    {
        var activity = new
        {
            id = $"https://{Guid.NewGuid():N}.example/activity",
            type = "Create",
            actor = remoteActor,
            @object = new
            {
                id = $"https://{Guid.NewGuid():N}.example/work",
                type = "WorkRef",
                domain = "music",
                title,
                creator,
                externalIds = new Dictionary<string, string>
                {
                    ["musicbrainz"] = "12345678-1234-1234-1234-1234567890ab",
                },
            },
        };

        return new ActivityPubInboxEntry
        {
            ActorName = "music",
            ActivityId = Guid.NewGuid().ToString(),
            ActivityType = "Create",
            RemoteActor = remoteActor,
            ReceivedAt = DateTimeOffset.UtcNow,
            RawJson = JsonSerializer.Serialize(activity),
            Processed = true,
        };
    }
}
