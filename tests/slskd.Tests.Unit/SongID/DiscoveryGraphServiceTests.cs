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
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "recognized_cataloged_track",
                Confidence = 0.82,
            },
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
                            Artist = "Artist One",
                            Title = "Track One",
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
                            Artist = "Artist Two",
                            Title = "Track Two",
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
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "recognized_cataloged_track",
                Confidence = 0.82,
            },
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
    public async Task BuildAsync_WithWeakSongIdRun_DoesNotPromoteSecondaryEvidenceIntoNeighborhood()
    {
        var runId = Guid.NewGuid();
        var run = new SongIdRun
        {
            Id = runId,
            Query = "Worakls - red herring",
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "needs_manual_review",
                Confidence = 0.58,
            },
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    CandidateId = "weak-track",
                    RecordingId = "weak-rec",
                    Artist = "Bloum",
                    Title = "Unverified Candidate",
                    MusicBrainzArtistId = "artist-bloum",
                    IdentityScore = 0.72,
                    ByzantineScore = 0.58,
                    ActionScore = 0.67,
                },
            },
            Albums = new List<SongIdAlbumCandidate>
            {
                new()
                {
                    CandidateId = "album-tv-show",
                    ReleaseId = "album-tv-show",
                    Artist = "Bloum",
                    Title = "TV Show",
                    IdentityScore = 0.72,
                    ByzantineScore = 0.58,
                    ActionScore = 0.67,
                },
            },
            Artists = new List<SongIdArtistCandidate>
            {
                new()
                {
                    CandidateId = "artist-bloum",
                    ArtistId = "artist-bloum",
                    Name = "Bloum",
                    IdentityScore = 0.68,
                    ByzantineScore = 0.60,
                    ActionScore = 0.74,
                },
            },
            Segments = new List<SongIdSegmentResult>
            {
                new()
                {
                    SegmentId = "segment-tv-show",
                    Label = "TV Show",
                    Confidence = 0.91,
                    Candidates = new List<SongIdTrackCandidate>
                    {
                        new()
                        {
                            CandidateId = "segment-track",
                            RecordingId = "segment-rec",
                            Artist = "Faith",
                            Title = "TV Show",
                            IdentityScore = 0.70,
                            ByzantineScore = 0.60,
                            ActionScore = 0.65,
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
        Assert.DoesNotContain(graph.Nodes, node => node.NodeType == "artist");
        Assert.DoesNotContain(graph.Nodes, node => node.NodeType == "album");
        Assert.DoesNotContain(graph.Nodes, node => node.NodeType == "segment");
        Assert.DoesNotContain(graph.Nodes, node => node.Label == "TV Show");
    }

    [Fact]
    public async Task BuildAsync_WithWeakArtistScope_DoesNotFetchReleaseGraph()
    {
        const string artistId = "artist-bloum";
        var runId = Guid.NewGuid();
        var run = new SongIdRun
        {
            Id = runId,
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "needs_manual_review",
                Confidence = 0.58,
            },
            Artists = new List<SongIdArtistCandidate>
            {
                new()
                {
                    CandidateId = artistId,
                    ArtistId = artistId,
                    Name = "Bloum",
                    IdentityScore = 0.68,
                    ByzantineScore = 0.60,
                    ActionScore = 0.74,
                },
            },
        };

        var songIdService = new Mock<ISongIdService>();
        songIdService.Setup(service => service.Get(runId)).Returns(run);

        var releaseGraphService = new Mock<IArtistReleaseGraphService>(MockBehavior.Strict);
        var service = new DiscoveryGraphService(songIdService.Object, releaseGraphService.Object);

        var graph = await service.BuildAsync(new DiscoveryGraphRequest
        {
            Scope = "artist",
            SongIdRunId = runId,
            ArtistId = artistId,
        });

        Assert.Single(graph.Nodes);
        Assert.Equal($"artist:{artistId}", graph.SeedNodeId);
        releaseGraphService.Verify(
            service => service.GetArtistReleaseGraphAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
