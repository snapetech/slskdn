// <copyright file="DiscoveryGraphServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SongID;

using Moq;
using slskd.DiscoveryGraph;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;
using slskd.SongID;
using Xunit;

public sealed class DiscoveryGraphServiceTests
{
    [Fact]
    public async Task BuildAsync_WithSongIdRunScope_ReturnsSeedTracksAndSegments()
    {
        var runId = Guid.NewGuid();
        var run = new SongIdRun
        {
            Id = runId,
            Query = "test seed",
            Metadata = new SongIdMetadata
            {
                Title = "Test Seed",
            },
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    CandidateId = "track-1",
                    RecordingId = "rec-1",
                    Artist = "Artist One",
                    Title = "Track One",
                    IdentityScore = 0.92,
                    ByzantineScore = 0.81,
                    ActionScore = 0.88,
                },
            },
            Segments = new List<SongIdSegmentResult>
            {
                new()
                {
                    SegmentId = "seg-1",
                    Label = "Opening",
                    Confidence = 0.71,
                    DecompositionLabel = "chapter",
                    Candidates = new List<SongIdTrackCandidate>
                    {
                        new()
                        {
                            CandidateId = "track-2",
                            RecordingId = "rec-2",
                            Artist = "Artist Two",
                            Title = "Track Two",
                            IdentityScore = 0.74,
                            ByzantineScore = 0.68,
                            ActionScore = 0.72,
                        },
                    },
                },
            },
        };

        var songIdService = new Mock<ISongIdService>();
        songIdService.Setup(service => service.Get(runId)).Returns(run);

        var releaseGraphService = new Mock<IArtistReleaseGraphService>(MockBehavior.Strict);

        var service = new DiscoveryGraphService(songIdService.Object, releaseGraphService.Object);

        var graph = await service.BuildAsync(new DiscoveryGraphRequest
        {
            Scope = "songid_run",
            SongIdRunId = runId,
        });

        Assert.Equal($"songid:{runId:D}", graph.SeedNodeId);
        Assert.Contains(graph.Nodes, node => node.NodeId == "track:rec-1");
        Assert.Contains(graph.Nodes, node => node.NodeId == "segment:seg-1");
        Assert.Contains(graph.Edges, edge => edge.EdgeType == "identity_candidate");
        Assert.Contains(graph.Edges, edge => edge.EdgeType == "segment_candidate");
    }

    [Fact]
    public async Task BuildAsync_RunGraphIncludesMixNodes()
    {
        var runId = Guid.NewGuid();
        var run = new SongIdRun
        {
            Id = runId,
            Query = "mix seed",
            Segments = new List<SongIdSegmentResult>
            {
                new()
                {
                    SegmentId = "seg-1",
                    Label = "Segment One",
                    Confidence = 0.71,
                    StartSeconds = 0,
                    Candidates = new List<SongIdTrackCandidate>
                    {
                        new()
                        {
                            CandidateId = "track-1",
                            RecordingId = "rec-1",
                            IdentityScore = 0.66,
                            ByzantineScore = 0.61,
                            ActionScore = 0.64,
                        },
                    },
                },
                new()
                {
                    SegmentId = "seg-2",
                    Label = "Segment Two",
                    Confidence = 0.68,
                    StartSeconds = 22,
                    Candidates = new List<SongIdTrackCandidate>
                    {
                        new()
                        {
                            CandidateId = "track-2",
                            RecordingId = "rec-2",
                            IdentityScore = 0.63,
                            ByzantineScore = 0.60,
                            ActionScore = 0.62,
                        },
                    },
                },
            },
        };
        run.MixGroups.Add(new SongIdMixGroup
        {
            MixId = "mix-seg-1",
            Label = "Mix cluster",
            SegmentIds = new List<string> { "seg-1", "seg-2" },
            Confidence = 0.69,
            IdentityScore = 0.64,
            ByzantineScore = 0.60,
            ActionScore = 0.63,
            SearchText = "mix seed query",
        });

        var songIdService = new Mock<ISongIdService>();
        songIdService.Setup(service => service.Get(runId)).Returns(run);

        var releaseGraphService = new Mock<IArtistReleaseGraphService>(MockBehavior.Strict);
        var service = new DiscoveryGraphService(songIdService.Object, releaseGraphService.Object);

        var graph = await service.BuildAsync(new DiscoveryGraphRequest
        {
            Scope = "songid_run",
            SongIdRunId = runId,
        });

        Assert.Contains(graph.Nodes, node => node.NodeId == "mix:mix-seg-1");
        Assert.Contains(graph.Edges, edge => edge.EdgeType == "mix_cluster");
        Assert.Contains(graph.Edges, edge => edge.EdgeType == "mix_segment");
    }

    [Fact]
    public async Task BuildAsync_WithArtistScope_AddsReleaseGroupsFromMusicBrainz()
    {
        const string artistId = "artist-1";
        var run = new SongIdRun
        {
            Artists = new List<SongIdArtistCandidate>
            {
                new()
                {
                    CandidateId = "artist-candidate-1",
                    ArtistId = artistId,
                    Name = "Artist One",
                    IdentityScore = 0.84,
                    ByzantineScore = 0.77,
                    ActionScore = 0.81,
                },
            },
        };

        var songIdService = new Mock<ISongIdService>();
        songIdService.Setup(service => service.Get(It.IsAny<Guid>())).Returns(run);

        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        releaseGraphService
            .Setup(service => service.GetArtistReleaseGraphAsync(artistId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArtistReleaseGraph
            {
                ArtistId = artistId,
                Name = "Artist One",
                ReleaseGroups = new List<ReleaseGroup>
                {
                    new()
                    {
                        ReleaseGroupId = "rg-1",
                        Title = "Release One",
                    },
                },
            });

        var service = new DiscoveryGraphService(songIdService.Object, releaseGraphService.Object);

        var graph = await service.BuildAsync(new DiscoveryGraphRequest
        {
            Scope = "artist",
            SongIdRunId = Guid.NewGuid(),
            ArtistId = artistId,
            Artist = "Artist One",
        });

        Assert.Contains(graph.Nodes, node => node.NodeId == "release-group:rg-1");
        Assert.Contains(graph.Edges, edge => edge.EdgeType == "release_group");
    }

    [Fact]
    public async Task BuildAsync_WithCompareNodeId_AddsComparisonEdge()
    {
        var runId = Guid.NewGuid();
        var run = new SongIdRun
        {
            Id = runId,
            Query = "compare seed",
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    CandidateId = "track-1",
                    RecordingId = "rec-1",
                    Artist = "Artist One",
                    Title = "Track One",
                    IdentityScore = 0.88,
                    ByzantineScore = 0.78,
                    ActionScore = 0.84,
                },
            },
        };

        var songIdService = new Mock<ISongIdService>();
        songIdService.Setup(service => service.Get(runId)).Returns(run);

        var releaseGraphService = new Mock<IArtistReleaseGraphService>(MockBehavior.Strict);
        var service = new DiscoveryGraphService(songIdService.Object, releaseGraphService.Object);

        var graph = await service.BuildAsync(new DiscoveryGraphRequest
        {
            Scope = "songid_run",
            SongIdRunId = runId,
            CompareNodeId = "artist:artist-compare",
            CompareLabel = "Pinned Artist",
        });

        Assert.Contains(graph.Nodes, node => node.NodeId == "artist:artist-compare");
        Assert.Contains(graph.Edges, edge => edge.EdgeType == "comparison");
    }
}
