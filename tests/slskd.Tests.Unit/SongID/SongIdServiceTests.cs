// <copyright file="SongIdServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#nullable enable

namespace slskd.Tests.Unit.SongID;

using System;
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
    public void AddFallbackOptions_IncludesRawTranscriptOcrAndCommentQueries()
    {
        var run = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Query = "seed query",
            Metadata = new SongIdMetadata
            {
                Artist = "Known Artist",
                Title = "Known Title",
            },
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "needs_manual_review",
            },
            Transcripts = new List<SongIdTranscriptFinding>
            {
                new()
                {
                    MusicBrainzQueries = new List<string> { "Loose Transcript Phrase" },
                },
            },
            Ocr = new List<SongIdOcrFinding>
            {
                new()
                {
                    Text = "OCR Candidate Title",
                },
            },
            Comments = new List<SongIdCommentFinding>
            {
                new()
                {
                    Text = "00:12 Comment Candidate Title",
                },
            },
        };

        var method = typeof(SongIdService).GetMethod("AddFallbackOptions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        method!.Invoke(null, new object[] { run });

        var option = Assert.Single(run.Options);
        Assert.NotNull(option.SearchTexts);
        Assert.Contains("Loose Transcript Phrase", option.SearchTexts!, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("OCR Candidate Title", option.SearchTexts!, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Comment Candidate Title", option.SearchTexts!, StringComparer.OrdinalIgnoreCase);
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
    public void BuildSegmentQueries_IncludesTranscriptAndOcrDerivedQueries()
    {
        var method = typeof(SongIdService).GetMethod("BuildSegmentQueries", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var run = new SongIdRun
        {
            Metadata = new SongIdMetadata
            {
                Artist = "Known Artist",
            },
            Transcripts = new List<SongIdTranscriptFinding>
            {
                new()
                {
                    TranscriptId = "tx-1",
                    Source = "whisper",
                    ExcerptStartSeconds = 45,
                    MusicBrainzQueries = new List<string> { "Known Title live mix" },
                },
            },
            Ocr = new List<SongIdOcrFinding>
            {
                new()
                {
                    OcrId = "ocr-1",
                    TimestampSeconds = 60,
                    Text = "Known Title [Live]",
                },
            },
        };

        var queries = Assert.IsAssignableFrom<IEnumerable<object>>(method!.Invoke(null, new object[] { run })!);
        var serialized = queries.Select(query => JsonSerializer.Serialize(query)).ToList();

        Assert.Contains(serialized, item => item.Contains("45-known artist known title", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(serialized, item => item.Contains("60-known artist known title", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(serialized, item => item.Contains("Known Artist Known Title live mix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AddFallbackOptions_UsesTranscriptOcrAndCommentEvidence()
    {
        var method = typeof(SongIdService).GetMethod("AddFallbackOptions", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var run = new SongIdRun
        {
            Query = "raw query",
            Metadata = new SongIdMetadata
            {
                Artist = "Known Artist",
                Title = "Known Title",
            },
            IdentityAssessment = new SongIdAssessment
            {
                Verdict = "needs_manual_review",
            },
            ForensicMatrix = new SongIdForensicMatrix
            {
                KnownFamilyScore = 35,
            },
            Transcripts = new List<SongIdTranscriptFinding>
            {
                new()
                {
                    TranscriptId = "tx-1",
                    MusicBrainzQueries = new List<string> { "Deep cut alt mix" },
                },
            },
            Ocr = new List<SongIdOcrFinding>
            {
                new()
                {
                    OcrId = "ocr-1",
                    TimestampSeconds = 10,
                    Text = "Title Card [Live]",
                },
            },
            Comments = new List<SongIdCommentFinding>
            {
                new()
                {
                    CommentId = "c-1",
                    Text = "what is this unreleased version",
                },
            },
        };

        method!.Invoke(null, new object[] { run });

        var option = Assert.Single(run.Options);
        Assert.Contains(option.SearchTexts, query => query.Contains("Deep cut alt mix", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(option.SearchTexts, query => query.Contains("Title Card", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(option.SearchTexts, query => query.Contains("unreleased version", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryGetString_ParsesStringifiedNumericAndBooleanValues()
    {
        var method = typeof(SongIdService).GetMethod("TryGetString", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        using var doc = JsonDocument.Parse("{\"duration\":123,\"verified\":true}");
        var duration = method!.Invoke(null, new object[] { doc.RootElement, "duration" });
        var verified = method.Invoke(null, new object[] { doc.RootElement, "verified" });

        Assert.Equal("123", duration);
        Assert.Equal(bool.TrueString, verified);
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

    [Fact]
    public async Task AddSearchCandidatesAsync_AddsSyntheticCandidateForMetadataHitWithoutRecordingId()
    {
        var store = new SongIdRunStore();
        var metadataFacade = new Mock<IMetadataFacade>();
        metadataFacade
            .Setup(facade => facade.SearchAsync("artist title", 8, default))
            .Returns(CreateAsyncEnumerable(new[]
            {
                new MetadataResult(
                    "Artist",
                    "Title",
                    Album: null,
                    MusicBrainzRecordingId: null,
                    MusicBrainzReleaseId: null,
                    MusicBrainzArtistId: "artist-1",
                    Isrc: null,
                    Year: null,
                    Genre: null,
                    MetadataResult.SourceMusicBrainz),
            }));
        var service = CreateService(store, metadataFacade.Object);
        var run = new SongIdRun();

        var method = typeof(SongIdService).GetMethod("AddSearchCandidatesAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        await (Task)method!.Invoke(service, new object[] { run, "artist title", CancellationToken.None })!;

        var candidate = Assert.Single(run.Tracks);
        Assert.StartsWith("metadata:artist title", candidate.RecordingId, StringComparison.Ordinal);
        Assert.Contains(run.Evidence, item => item.Contains("1 track candidate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeLocalFileAsync_UsesFilenameFallbackWhenMetadataFacadeReturnsNull()
    {
        var tempDir = Path.Combine(_tempDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "Known Artist - Known Title.mp3");
        await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());

        var metadataFacade = new Mock<IMetadataFacade>();
        metadataFacade
            .Setup(facade => facade.GetByFileAsync(filePath, default))
            .ReturnsAsync((MetadataResult?)null);
        metadataFacade
            .Setup(facade => facade.GetBySoulseekFilenameAsync(string.Empty, "Known Artist - Known Title.mp3", default))
            .ReturnsAsync(new MetadataResult(
                "Known Artist",
                "Known Title",
                Album: null,
                MusicBrainzRecordingId: null,
                MusicBrainzReleaseId: null,
                MusicBrainzArtistId: null,
                Isrc: null,
                Year: null,
                Genre: null,
                MetadataResult.SourceSoulseek));

        var service = CreateService(new SongIdRunStore(), metadataFacade.Object);
        var method = typeof(SongIdService).GetMethod("AnalyzeLocalFileAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = (Task)method!.Invoke(service, new object[] { filePath, CancellationToken.None })!;
        await task.ConfigureAwait(false);
        var analysis = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance)!.GetValue(task)!;

        Assert.Equal("Known Artist Known Title", analysis.GetType().GetProperty("Query")!.GetValue(analysis));
        var metadata = analysis.GetType().GetProperty("Metadata")!.GetValue(analysis)!;
        Assert.Equal("Known Artist", metadata.GetType().GetProperty("Artist")!.GetValue(metadata));
        Assert.Equal("Known Title", metadata.GetType().GetProperty("Title")!.GetValue(metadata));
    }

    [Fact]
    public void ResolveCorpusFingerprintPath_TrimsRelativeFingerprintPath()
    {
        var metadataDir = Path.Combine(_tempDir, "trimmed-corpus");
        Directory.CreateDirectory(metadataDir);

        var metadataPath = Path.Combine(metadataDir, "entry.json");
        File.WriteAllText(metadataPath, "{}");

        var fingerprintPath = Path.Combine(metadataDir, "fingerprint.fp");
        File.WriteAllText(fingerprintPath, "FINGERPRINT=1,2,3");

        var method = typeof(SongIdService).GetMethod("ResolveCorpusFingerprintPath", BindingFlags.NonPublic | BindingFlags.Static);
        var entryType = typeof(SongIdService).GetNestedType("SongIdCorpusEntry", BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.NotNull(entryType);

        var entry = Activator.CreateInstance(entryType!);
        Assert.NotNull(entry);
        entryType!.GetProperty("FingerprintPath")!.SetValue(entry, " fingerprint.fp ");

        var result = method!.Invoke(null, new object[] { metadataPath, entry });

        Assert.Equal(fingerprintPath, result);
    }

    [Fact]
    public void ChooseExcerptStart_UsesEarliestTimestampInsteadOfInputOrder()
    {
        var method = typeof(SongIdService).GetMethod("ChooseExcerptStart", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = Assert.IsType<int>(method!.Invoke(null, new object[] { new[] { 95, 20, 60 }, 300, 90 })!);

        Assert.Equal(0, result);
    }

    [Fact]
    public void LocatePanakoJar_UsesTrimmedEnvironmentValue()
    {
        var jarPath = Path.Combine(_tempDir, "panako.jar");
        File.WriteAllText(jarPath, "jar");
        Environment.SetEnvironmentVariable("PANAKO_JAR", $"  {jarPath}  ");

        try
        {
            var service = CreateService(new SongIdRunStore());
            var method = typeof(SongIdService).GetMethod("LocatePanakoJar", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var result = method!.Invoke(service, Array.Empty<object>());

            Assert.Equal(jarPath, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PANAKO_JAR", null);
        }
    }

    [Fact]
    public void LocateAudfprintScript_UsesTrimmedEnvironmentValue()
    {
        var scriptPath = Path.Combine(_tempDir, "audfprint.py");
        File.WriteAllText(scriptPath, "print('ok')");
        Environment.SetEnvironmentVariable("AUDFPRINT_SCRIPT", $"  {scriptPath}  ");

        try
        {
            var service = CreateService(new SongIdRunStore());
            var method = typeof(SongIdService).GetMethod("LocateAudfprintScript", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var result = method!.Invoke(service, Array.Empty<object>());

            Assert.Equal(scriptPath, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUDFPRINT_SCRIPT", null);
        }
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

    private static async IAsyncEnumerable<MetadataResult> CreateAsyncEnumerable(IEnumerable<MetadataResult> results)
    {
        foreach (var result in results)
        {
            yield return result;
            await Task.Yield();
        }
    }
}
