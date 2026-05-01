// <copyright file="BrainzClientTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Moq;
using slskd.Integrations.AcoustId;
using slskd.Integrations.AcoustId.Models;
using slskd.Integrations.Brainz;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;
using Xunit;

public sealed class BrainzClientTests
{
    [Fact]
    public async Task GetRecordingAsync_UsesMusicBrainzAndCachesByTrimmedId()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        var client = CreateClient(musicBrainz);
        var track = new TrackTarget
        {
            MusicBrainzRecordingId = "rec-1",
            Title = "Song",
            Artist = "Artist",
        };

        musicBrainz
            .Setup(x => x.GetRecordingAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(track);

        var first = await client.GetRecordingAsync(" rec-1 ");
        var second = await client.GetRecordingAsync("REC-1");

        Assert.Same(track, first);
        Assert.Same(track, second);
        musicBrainz.Verify(x => x.GetRecordingAsync("rec-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchRecordingsAsync_TrimsDeduplicatesAndCapsLimit()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        var client = CreateClient(musicBrainz);

        musicBrainz
            .Setup(x => x.SearchRecordingsAsync("artist title", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordingSearchHit(" rec-1 ", " Song ", " Artist ", " artist-1 "),
                new RecordingSearchHit("REC-1", "Duplicate", "Artist", "artist-1"),
                new RecordingSearchHit("rec-2", "Other", "Other Artist", null),
            });

        var results = await client.SearchRecordingsAsync("  artist title  ", 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("rec-1", results[0].RecordingId);
        Assert.Equal("Song", results[0].Title);
        Assert.Equal("Artist", results[0].Artist);
        Assert.Equal("artist-1", results[0].MusicBrainzArtistId);
        Assert.Equal("rec-2", results[1].RecordingId);
    }

    [Fact]
    public async Task LookupFingerprintAsync_ResolvesAcoustIdRecordingThroughMusicBrainz()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        var acoustId = new Mock<IAcoustIdClient>();
        var client = CreateClient(musicBrainz, acoustId);

        acoustId
            .Setup(x => x.LookupAsync("fingerprint", 44100, 180, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcoustIdResult
            {
                Id = " acoust-1 ",
                Score = 0.98,
                Recordings =
                [
                    new Recording
                    {
                        Id = " rec-1 ",
                        Title = "AcoustID Title",
                        Artists =
                        [
                            new Artist { Name = "AcoustID Artist" },
                        ],
                    },
                ],
            });

        musicBrainz
            .Setup(x => x.GetRecordingAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackTarget
            {
                MusicBrainzRecordingId = "rec-1",
                Title = "MusicBrainz Title",
                Artist = "MusicBrainz Artist",
            });

        var result = await client.LookupFingerprintAsync(" fingerprint ", 44100, 180);

        Assert.Equal("acoust-1", result?.AcoustId);
        Assert.Equal(0.98, result?.Score);
        Assert.Equal("rec-1", result?.RecordingId);
        Assert.Equal("MusicBrainz Title", result?.Title);
        Assert.Equal("MusicBrainz Artist", result?.Artist);
        Assert.NotNull(result?.MusicBrainzRecording);
    }

    [Fact]
    public async Task LookupFingerprintAsync_FallsBackToAcoustIdMetadataWhenMusicBrainzMisses()
    {
        var musicBrainz = new Mock<IMusicBrainzClient>();
        var acoustId = new Mock<IAcoustIdClient>();
        var client = CreateClient(musicBrainz, acoustId);

        acoustId
            .Setup(x => x.LookupAsync("fingerprint", 44100, 180, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AcoustIdResult
            {
                Id = "acoust-1",
                Score = 0.82,
                Recordings =
                [
                    new Recording
                    {
                        Id = "rec-1",
                        Title = "AcoustID Title",
                        Artists =
                        [
                            new Artist { Name = "AcoustID Artist" },
                        ],
                    },
                ],
            });

        musicBrainz
            .Setup(x => x.GetRecordingAsync("rec-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackTarget)null);

        var result = await client.LookupFingerprintAsync("fingerprint", 44100, 180);

        Assert.Equal("AcoustID Title", result?.Title);
        Assert.Equal("AcoustID Artist", result?.Artist);
        Assert.Null(result?.MusicBrainzRecording);
    }

    private static BrainzClient CreateClient()
    {
        return CreateClient(new Mock<IMusicBrainzClient>(), new Mock<IAcoustIdClient>());
    }

    private static BrainzClient CreateClient(Mock<IMusicBrainzClient> musicBrainz)
    {
        return CreateClient(musicBrainz, new Mock<IAcoustIdClient>());
    }

    private static BrainzClient CreateClient(Mock<IMusicBrainzClient> musicBrainz, Mock<IAcoustIdClient> acoustId)
    {
        return new BrainzClient(
            musicBrainz.Object,
            acoustId.Object);
    }
}
