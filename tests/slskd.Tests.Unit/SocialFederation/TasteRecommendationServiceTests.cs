// <copyright file="TasteRecommendationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.SocialFederation;

using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.DiscoveryGraph;
using slskd.Integrations.MusicBrainz.Radar;
using slskd.SocialFederation;
using slskd.Wishlist;

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

    [Fact]
    public async Task GetRecommendationsAsync_AddsGraphEvidenceToScoreAndReasons()
    {
        var discoveryGraph = new Mock<IDiscoveryGraphService>();
        discoveryGraph
            .Setup(service => service.BuildAsync(It.IsAny<DiscoveryGraphRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryGraphResult
            {
                SeedNodeId = "artist:artist-1",
                Nodes = new List<DiscoveryGraphNode>
                {
                    new() { NodeId = "artist:artist-1" },
                    new() { NodeId = "track:track-1" },
                },
                Edges = new List<DiscoveryGraphEdge>
                {
                    new() { SourceNodeId = "artist:artist-1", TargetNodeId = "track:track-1" },
                },
            });
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
            },
            discoveryGraph: discoveryGraph.Object);

        var result = await service.GetRecommendationsAsync(new TasteRecommendationRequest
        {
            MinimumTrustedSources = 2,
            IncludeGraphEvidence = true,
        });

        var recommendation = Assert.Single(result.Recommendations);
        Assert.NotNull(recommendation.GraphEvidence);
        Assert.Equal("artist:artist-1", recommendation.GraphEvidence.SeedNodeId);
        Assert.Contains("near a Discovery Graph neighborhood with 2 nodes", recommendation.Reasons);
        Assert.True(recommendation.Score > 20);
    }

    [Fact]
    public async Task PromoteToWishlistAsync_CreatesReviewOnlySeedAndDedupesExistingSearch()
    {
        var wishlist = new Mock<IWishlistService>();
        wishlist.SetupSequence(service => service.ListAsync())
            .ReturnsAsync(new List<WishlistItem>())
            .ReturnsAsync(new List<WishlistItem>
            {
                new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), SearchText = "Shared Artist Shared Song" },
            });
        wishlist
            .Setup(service => service.CreateAsync(It.IsAny<WishlistItem>()))
            .ReturnsAsync((WishlistItem item) =>
            {
                item.Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
                return item;
            });
        var service = CreateService(
            trustedActors: Array.Empty<string>(),
            entries: Array.Empty<ActivityPubInboxEntry>(),
            wishlist: wishlist.Object);
        var request = new TasteRecommendationWishlistPromotionRequest
        {
            WorkRef = CreateWorkRef("Shared Song", "Shared Artist"),
            Note = "trusted libraries",
        };

        var created = await service.PromoteToWishlistAsync(request);
        var duplicate = await service.PromoteToWishlistAsync(request);

        Assert.True(created.Created);
        Assert.Equal("22222222-2222-2222-2222-222222222222", created.WishlistItemId);
        Assert.False(duplicate.Created);
        Assert.Equal("11111111-1111-1111-1111-111111111111", duplicate.WishlistItemId);
        wishlist.Verify(service => service.CreateAsync(It.Is<WishlistItem>(item =>
            item.SearchText == "Shared Artist Shared Song" &&
            item.Enabled == false &&
            item.AutoDownload == false &&
            item.Filter.Contains("source:taste-recommendation") &&
            item.Filter.Contains("review-only"))), Times.Once);
    }

    [Fact]
    public async Task SubscribeArtistRadarAsync_RequiresArtistMbidAndCreatesSubscription()
    {
        var radar = new Mock<IArtistReleaseRadarService>();
        radar
            .Setup(service => service.SubscribeAsync(It.IsAny<ArtistRadarSubscription>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ArtistRadarSubscription subscription, CancellationToken _) =>
            {
                subscription.Id = $"artist-radar:{subscription.ArtistId}";
                return subscription;
            });
        var service = CreateService(
            trustedActors: Array.Empty<string>(),
            entries: Array.Empty<ActivityPubInboxEntry>(),
            radar: radar.Object);

        var missing = await service.SubscribeArtistRadarAsync(new TasteRecommendationRadarSubscriptionRequest
        {
            WorkRef = CreateWorkRef("Shared Song", "Shared Artist"),
        });
        var created = await service.SubscribeArtistRadarAsync(new TasteRecommendationRadarSubscriptionRequest
        {
            WorkRef = CreateWorkRef("Shared Song", "Shared Artist", artistId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Scope = "realm:trusted-crate",
        });

        Assert.False(missing.Created);
        Assert.True(created.Created);
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", created.ArtistId);
        radar.Verify(service => service.SubscribeAsync(It.Is<ArtistRadarSubscription>(subscription =>
            subscription.ArtistId == "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" &&
            subscription.ArtistName == "Shared Artist" &&
            subscription.Scope == "realm:trusted-crate"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewDiscoveryGraphAsync_ReturnsGraphSummary()
    {
        var discoveryGraph = new Mock<IDiscoveryGraphService>();
        discoveryGraph
            .Setup(service => service.BuildAsync(It.Is<DiscoveryGraphRequest>(request =>
                request.Scope == "artist" &&
                request.Artist == "Shared Artist"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryGraphResult
            {
                SeedNodeId = "artist:shared",
                Nodes = new List<DiscoveryGraphNode> { new() { NodeId = "artist:shared" } },
                Edges = new List<DiscoveryGraphEdge>(),
            });
        var service = CreateService(
            trustedActors: Array.Empty<string>(),
            entries: Array.Empty<ActivityPubInboxEntry>(),
            discoveryGraph: discoveryGraph.Object);

        var result = await service.PreviewDiscoveryGraphAsync(new TasteRecommendationGraphPreviewRequest
        {
            WorkRef = CreateWorkRef("Shared Song", "Shared Artist"),
        });

        Assert.True(result.Available);
        Assert.Equal("artist:shared", result.SeedNodeId);
        Assert.Equal(1, result.NodeCount);
    }

    private TasteRecommendationService CreateService(
        IReadOnlyList<string> trustedActors,
        IReadOnlyList<ActivityPubInboxEntry> entries,
        IDiscoveryGraphService? discoveryGraph = null,
        IWishlistService? wishlist = null,
        IArtistReleaseRadarService? radar = null)
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
            discoveryGraph,
            wishlist,
            radar,
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
                externalIds = CreateWorkRef(title, creator, artistId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ExternalIds,
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

    private static WorkRef CreateWorkRef(string title, string creator, string? artistId = null)
    {
        var workRef = new WorkRef
        {
            Id = $"https://{Guid.NewGuid():N}.example/work",
            Domain = "music",
            Title = title,
            Creator = creator,
            ExternalIds = new Dictionary<string, string>
            {
                ["musicbrainz"] = "12345678-1234-1234-1234-1234567890ab",
            },
        };

        if (!string.IsNullOrWhiteSpace(artistId))
        {
            workRef.ExternalIds["musicbrainz_artist"] = artistId;
        }

        return workRef;
    }
}
