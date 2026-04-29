// <copyright file="SongIdServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
#nullable enable

namespace slskd.Tests.Unit.SongID;

using System.Net.Http;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Audio;
using slskd.Integrations.AcoustId;
using slskd.Integrations.Chromaprint;
using slskd.Integrations.MetadataFacade;
using slskd.Integrations.MusicBrainz;
using slskd.SongID;
using slskd.SongID.API;
using Xunit;

[Collection("SongIdAppDirectory")]
public sealed class SongIdServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalAppDirectory;

    public SongIdServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "slskdn-songid-service-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var property = typeof(Program).GetProperty(nameof(Program.AppDirectory), BindingFlags.Public | BindingFlags.Static);
        _originalAppDirectory = property?.GetValue(null) as string;
        property?.SetValue(null, _tempDir);
    }

    [Fact]
    public async Task RecoverQueuedRunsAsync_RequeuesRunningRunsAndRefreshesQueuePositions()
    {
        var store = new SongIdRunStore();
        var olderQueued = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "older",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
            Summary = "older",
        };
        var recoveredRunning = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "running",
            Status = "running",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            Summary = "running",
            WorkerSlot = 1,
            PercentComplete = 0.67,
        };
        var newerQueued = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "newer",
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            Summary = "newer",
        };

        store.Upsert(olderQueued);
        store.Upsert(recoveredRunning);
        store.Upsert(newerQueued);

        var service = CreateService(store);

        await service.RecoverQueuedRunsAsync();

        var active = store.ListByStatuses(new[] { "queued", "running" }, 10);

        Assert.Equal(3, active.Count);
        Assert.All(active, run => Assert.Equal("queued", run.Status));
        Assert.Equal(olderQueued.Id, active[0].Id);
        Assert.Equal(recoveredRunning.Id, active[1].Id);
        Assert.Equal(newerQueued.Id, active[2].Id);
        Assert.Equal(1, active[0].QueuePosition);
        Assert.Equal(2, active[1].QueuePosition);
        Assert.Equal(3, active[2].QueuePosition);
        Assert.Null(active[1].WorkerSlot);
        Assert.Contains(active[1].Evidence, item => item.Contains("Recovered after restart", StringComparison.Ordinal));
        Assert.True(active[1].PercentComplete <= 0.05);
    }

    [Fact]
    public async Task EnqueueRunAsync_AssignsQueuePositionsInCreatedOrder()
    {
        var store = new SongIdRunStore();
        var service = CreateService(store);
        var first = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "first",
            Status = "created",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        };
        var second = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "second",
            Status = "created",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

        await service.EnqueueRunAsync(second);
        await service.EnqueueRunAsync(first);

        var queued = store.ListByStatuses(new[] { "queued" }, 10);

        Assert.Equal(2, queued.Count);
        Assert.Equal(first.Id, queued[0].Id);
        Assert.Equal(second.Id, queued[1].Id);
        Assert.Equal(1, queued[0].QueuePosition);
        Assert.Equal(2, queued[1].QueuePosition);
        Assert.Equal("Queued for SongID analysis. Next to run.", queued[0].Summary);
        Assert.Equal("Queued for SongID analysis. 1 run(s) ahead.", queued[1].Summary);
    }

    [Fact]
    public void BuildSegmentOptions_PrefersHigherIdentityCandidate()
    {
        var segment = new SongIdSegmentResult
        {
            SegmentId = "seg-1",
            Label = "Segment @ 00:30",
            Query = "segment search",
            SourceLabel = "comments",
            StartSeconds = 30,
            Confidence = 0.55,
            Candidates = new List<SongIdTrackCandidate>
            {
                new()
                {
                    RecordingId = "rec-low-identity",
                    Artist = "Low Identity",
                    Title = "High Canonical",
                    SearchText = "Low Identity High Canonical",
                    IdentityScore = 0.48,
                    CanonicalScore = 1.0,
                    HasLosslessCanonical = true,
                    ByzantineScore = 0.68,
                    ActionScore = 0.74,
                },
                new()
                {
                    RecordingId = "rec-high-identity",
                    Artist = "High Identity",
                    Title = "Lower Canonical",
                    SearchText = "High Identity Lower Canonical",
                    IdentityScore = 0.91,
                    CanonicalScore = 0.10,
                    ByzantineScore = 0.64,
                    ActionScore = 0.71,
                },
            },
        };

        var method = typeof(SongIdService).GetMethod("BuildSegmentOptions", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(SongIdSegmentResult) }, null);
        Assert.NotNull(method);

        var options = Assert.IsType<List<SongIdAcquisitionOption>>(method!.Invoke(null, new object[] { segment }));
        var candidateOptions = options.Where(option => option.Mode == "segment_candidate").ToList();

        Assert.Equal(2, candidateOptions.Count);
        Assert.Equal("rec-high-identity", candidateOptions[0].TargetId);
        Assert.True(candidateOptions[0].OverallScore > candidateOptions[1].OverallScore);
    }

    [Fact]
    public void BuildPlans_AddsMixPlanForAdjacentSegments()
    {
        var run = new SongIdRun
        {
            Segments = new List<SongIdSegmentResult>
            {
                new()
                {
                    SegmentId = "seg-1",
                    Label = "Segment One",
                    Query = "segment one",
                    StartSeconds = 0,
                    Confidence = 0.62,
                    Candidates = new List<SongIdTrackCandidate>
                    {
                        new()
                        {
                            RecordingId = "rec-one",
                            IdentityScore = 0.58,
                            ByzantineScore = 0.54,
                            ActionScore = 0.56,
                        },
                    },
                },
                new()
                {
                    SegmentId = "seg-2",
                    Label = "Segment Two",
                    Query = "segment two",
                    StartSeconds = 20,
                    Confidence = 0.55,
                    Candidates = new List<SongIdTrackCandidate>
                    {
                        new()
                        {
                            RecordingId = "rec-two",
                            IdentityScore = 0.61,
                            ByzantineScore = 0.59,
                            ActionScore = 0.63,
                        },
                    },
                },
            },
        };

        var method = typeof(SongIdService).GetMethod("BuildPlans", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, new object[] { run });

        Assert.Contains(run.Plans, plan => plan.Kind == "mix" && plan.Title.StartsWith("Mix cluster"));
        Assert.Contains(run.Evidence, entry => entry.Contains("mix cluster"));
    }

    [Fact]
    public void BuildSegmentQueries_UsesArtistDashTrackFormat()
    {
        var run = new SongIdRun
        {
            Metadata = new SongIdMetadata
            {
                Artist = "Joachim Pastor",
            },
            Chapters = new List<SongIdChapterFinding>
            {
                new()
                {
                    Title = "Joda",
                    StartSeconds = 15,
                },
            },
        };

        var method = typeof(SongIdService).GetMethod("BuildSegmentQueries", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var segments = Assert.IsAssignableFrom<System.Collections.IEnumerable>(method!.Invoke(null, new object[] { run }));
        var segment = Assert.Single(segments.Cast<object>());
        var query = segment.GetType().GetProperty("Query", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(segment) as string;

        Assert.Equal("Joachim Pastor - Joda", query);
    }

    [Fact]
    public void AddFallbackOptions_UsesArtistDashTrackQueries()
    {
        var run = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Query = "Uploader Name Very Long Video Title Official Audio",
            Metadata = new SongIdMetadata
            {
                Artist = "Lucio101",
                Title = "Taylor Swift",
            },
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "needs_manual_review",
            },
        };

        var method = typeof(SongIdService).GetMethod("AddFallbackOptions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, new object[] { run });

        var option = Assert.Single(run.Options);
        Assert.Equal("Lucio101 - Taylor Swift", option.SearchText);
        Assert.Contains("Lucio101 - Taylor Swift", option.SearchTexts);
        Assert.Contains("Uploader Name Very Long Video Title Official Audio", option.SearchTexts);
        Assert.DoesNotContain(option.SearchTexts, query => query.Contains("Lucio101 Taylor Swift", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddPipelineEvidenceAsync_WithMissingYtDlp_DoesNotFailYouTubeRuns()
    {
        var run = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "https://youtu.be/K3wtamktLGs",
            SourceType = "youtube_url",
            Status = "running",
            CreatedAt = DateTimeOffset.UtcNow,
            ArtifactDirectory = Path.Combine(_tempDir, "songid", Guid.NewGuid().ToString("D")),
        };
        var service = CreateService(new SongIdRunStore(), commandExistsOverride: (fileName, _) =>
            Task.FromResult(!string.Equals(fileName, "yt-dlp", StringComparison.OrdinalIgnoreCase)));
        var method = typeof(SongIdService).GetMethod("AddPipelineEvidenceAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(service, new object[] { run, run.Source, CancellationToken.None }));
        await task;

        Assert.Equal("youtube_metadata", run.Scorecard.AnalysisAudioSource);
        Assert.Contains(run.Evidence, entry => entry.Contains("yt-dlp unavailable; skipping YouTube audio, video, and comment extraction", StringComparison.Ordinal));
        Assert.Empty(run.Clips);
        Assert.Empty(run.Transcripts);
        Assert.Empty(run.Ocr);
    }

    [Fact]
    public async Task AddArtistCandidatesAsync_WithTimedOutReleaseGraph_UsesLightweightFallback()
    {
        var run = new SongIdRun
        {
            Tracks = new List<SongIdTrackCandidate>
            {
                new()
                {
                    CandidateId = "track-1",
                    RecordingId = "recording-1",
                    Artist = "Slow Artist",
                    MusicBrainzArtistId = "artist-1",
                    Title = "Slow Song",
                },
            },
        };
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        releaseGraphService
            .Setup(service => service.GetArtistReleaseGraphAsync("artist-1", false, It.IsAny<CancellationToken>()))
            .Returns(async (string _, bool _, CancellationToken ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return (slskd.Integrations.MusicBrainz.Models.ArtistReleaseGraph?)null;
            });

        var service = CreateService(new SongIdRunStore(), releaseGraphService: releaseGraphService.Object);
        var method = typeof(SongIdService).GetMethod("AddArtistCandidatesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(service, new object[] { run, CancellationToken.None }));
        await task;

        Assert.Single(run.Artists);
        Assert.Equal("Slow Artist", run.Artists[0].Name);
        Assert.Equal(0, run.Artists[0].ReleaseGroupCount);
        Assert.Contains(run.Evidence, entry => entry.Contains("Artist graph fetch timed out for Slow Artist", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        var property = typeof(Program).GetProperty(nameof(Program.AppDirectory), BindingFlags.Public | BindingFlags.Static);
        property?.SetValue(null, _originalAppDirectory);

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static SongIdService CreateService(
        ISongIdRunStore store,
        Func<string, CancellationToken, Task<bool>>? commandExistsOverride = null,
        IArtistReleaseGraphService? releaseGraphService = null)
    {
        var hubContext = CreateHubContext();
        var options = new slskd.Options
        {
            SongId = new slskd.Options.SongIdOptions
            {
                MaxConcurrentRuns = 2,
            },
        };

        return new SongIdService(
            store,
            Mock.Of<IMetadataFacade>(),
            Mock.Of<IMusicBrainzClient>(),
            Mock.Of<IAcoustIdClient>(),
            Mock.Of<ICanonicalStatsService>(),
            releaseGraphService ?? Mock.Of<IArtistReleaseGraphService>(),
            Mock.Of<IFingerprintExtractionService>(),
            Mock.Of<IHttpClientFactory>(),
            new TestOptionsMonitor<slskd.Options>(options),
            hubContext,
            Mock.Of<ILogger<SongIdService>>(),
            enableBackgroundWorkers: false,
            commandExistsOverride: commandExistsOverride);
    }

    private static IHubContext<SongIdHub> CreateHubContext()
    {
        var clientProxy = new Mock<IClientProxy>();
        clientProxy
            .Setup(proxy => proxy.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), default))
            .Returns(Task.CompletedTask);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(clients => clients.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<SongIdHub>>();
        hubContext.Setup(context => context.Clients).Returns(hubClients.Object);
        return hubContext.Object;
    }
}
