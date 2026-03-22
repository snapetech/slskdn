// <copyright file="SongIdServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#nullable enable

namespace slskd.Tests.Unit.SongID;

using System.Net.Http;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public void ParseProfiles_IgnoresNonPositiveClipLengthsAndSteps()
    {
        var method = typeof(SongIdService).GetMethod("ParseProfiles", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var profiles = Assert.IsType<List<(int ClipLength, int Step)>>(method!.Invoke(null, new object[] { "15:5,0:5,20:0,-10:3,30:10" }));

        Assert.Equal(2, profiles.Count);
        Assert.Contains((15, 5), profiles);
        Assert.Contains((30, 10), profiles);
        Assert.DoesNotContain((0, 5), profiles);
        Assert.DoesNotContain((20, 0), profiles);
        Assert.DoesNotContain((-10, 3), profiles);
    }

    [Fact]
    public void BuildClipStarts_WithNonPositiveProfileValues_ReturnsEmpty()
    {
        var method = typeof(SongIdService).GetMethod("BuildClipStarts", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var zeroClipLength = Assert.IsAssignableFrom<IEnumerable<int>>(method!.Invoke(null, new object[] { 120, 0, 5 })!);
        var zeroStep = Assert.IsAssignableFrom<IEnumerable<int>>(method.Invoke(null, new object[] { 120, 15, 0 })!);
        var negativeStep = Assert.IsAssignableFrom<IEnumerable<int>>(method.Invoke(null, new object[] { 120, 15, -2 })!);

        Assert.Empty(zeroClipLength);
        Assert.Empty(zeroStep);
        Assert.Empty(negativeStep);
    }

    [Fact]
    public void ParseSongRecFinding_InvalidJson_ReturnsNull()
    {
        var method = typeof(SongIdService).GetMethod("ParseSongRecFinding", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, new object[] { "not-json" });

        Assert.Null(result);
    }

    [Fact]
    public void ResolveCorpusFingerprintPath_RejectsPathTraversalOutsideMetadataDirectory()
    {
        var metadataDir = Path.Combine(_tempDir, "corpus");
        Directory.CreateDirectory(metadataDir);

        var metadataPath = Path.Combine(metadataDir, "entry.json");
        File.WriteAllText(metadataPath, "{}");

        var outsideFingerprint = Path.Combine(_tempDir, "outside.fpcalc");
        File.WriteAllText(outsideFingerprint, "FINGERPRINT=1,2,3");

        var method = typeof(SongIdService).GetMethod("ResolveCorpusFingerprintPath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var entryType = typeof(SongIdService).GetNestedType("SongIdCorpusEntry", BindingFlags.NonPublic);
        Assert.NotNull(entryType);

        var entry = Activator.CreateInstance(entryType!);
        Assert.NotNull(entry);

        entryType!.GetProperty("FingerprintPath")!.SetValue(entry, Path.Combine("..", "outside.fpcalc"));

        var result = method!.Invoke(null, new object[] { metadataPath, entry });

        Assert.Null(result);
    }

    [Fact]
    public void ParseSongRecFinding_ParsesMatchesArrayFirstTrack()
    {
        var method = typeof(SongIdService).GetMethod("ParseSongRecFinding", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var payload = JsonSerializer.Serialize(new
        {
            matches = new object[]
            {
                new
                {
                    track = new
                    {
                        title = "Track Title",
                        subtitle = "Track Artist",
                        key = "track-key",
                    },
                },
            },
        });

        var result = method!.Invoke(null, new object[] { payload });

        var finding = Assert.IsType<SongIdRecognizerFinding>(result);
        Assert.Equal("Track Title", finding.Title);
        Assert.Equal("Track Artist", finding.Artist);
        Assert.Equal("track-key", finding.ExternalId);
        Assert.Equal(1, finding.MatchCount);
    }

    [Fact]
    public async Task AnalyzeLocalFileAsync_WhenFingerprintExtractionThrows_SanitizesEvidence()
    {
        var store = new SongIdRunStore();
        var fingerprintService = new Mock<IFingerprintExtractionService>();
        fingerprintService
            .Setup(service => service.ExtractFingerprintAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive local fingerprint detail"));

        var metadataFacade = new Mock<IMetadataFacade>();
        metadataFacade
            .Setup(service => service.GetByFileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MetadataResult?)null);

        var service = CreateService(store, metadataFacade.Object, fingerprintService.Object);
        var method = typeof(SongIdService).GetMethod("AnalyzeLocalFileAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var audioPath = Path.Combine(_tempDir, "sample.flac");
        await File.WriteAllBytesAsync(audioPath, new byte[] { 0, 1, 2, 3 });

        var task = (Task)method!.Invoke(service, new object[] { audioPath, CancellationToken.None })!;
        await task;
        var analysis = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)!.GetValue(task)!;
        var evidence = (IEnumerable<string>)analysis.GetType().GetProperty("Evidence", BindingFlags.Public | BindingFlags.Instance)!.GetValue(analysis)!;
        var query = (string)analysis.GetType().GetProperty("Query", BindingFlags.Public | BindingFlags.Instance)!.GetValue(analysis)!;

        var sanitizedEvidence = Assert.Single(evidence.Where(item => item.Contains("Chromaprint extraction failed for local file", StringComparison.Ordinal)));
        Assert.DoesNotContain("sensitive", sanitizedEvidence, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFileNameWithoutExtension(audioPath), query);
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
        IMetadataFacade? metadataFacade = null,
        IFingerprintExtractionService? fingerprintExtractionService = null)
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
            metadataFacade ?? Mock.Of<IMetadataFacade>(),
            Mock.Of<IMusicBrainzClient>(),
            Mock.Of<IAcoustIdClient>(),
            Mock.Of<ICanonicalStatsService>(),
            Mock.Of<IArtistReleaseGraphService>(),
            fingerprintExtractionService ?? Mock.Of<IFingerprintExtractionService>(),
            Mock.Of<IHttpClientFactory>(),
            new TestOptionsMonitor<slskd.Options>(options),
            hubContext,
            Mock.Of<ILogger<SongIdService>>(),
            enableBackgroundWorkers: false);
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
