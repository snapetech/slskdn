// <copyright file="SongIdService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SongID;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Audio;
using slskd.Integrations.AcoustId;
using slskd.Integrations.AcoustId.Models;
using slskd.Integrations.Chromaprint;
using slskd.Integrations.MetadataFacade;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.Models;
using slskd.SongID.API;

public interface ISongIdService
{
    Task<SongIdRun> QueueAnalyzeAsync(string source, CancellationToken cancellationToken = default);

    SongIdRun? Get(Guid id);

    IReadOnlyList<SongIdRun> List(int limit = 25);
}

public sealed class SongIdService : ISongIdService
{
    private const string ClipProfiles = "90:45,60:30,45:15";
    private const int RecoveryBatchLimit = 10000;
    private const int MaxBaseClipsPerProfile = 4;
    private const int MaxFocusedClipsPerProfile = 6;
    private const int MaxComments = 40;
    private const int MaxSegmentGroups = 6;
    private const int MaxSegmentCandidatesPerGroup = 4;
    private static readonly TimeSpan ArtistGraphFetchTimeout = TimeSpan.FromSeconds(15);
    private const int WhisperExcerptSeconds = 180;
    private const int DemucsExcerptSeconds = 180;
    private const int PerturbationExcerptSeconds = 75;
    private const int MixGapThresholdSeconds = 45;
    private const int MinSegmentsPerMix = 2;
    private const string DefaultPanakoStrategy = "panako";
    private static readonly Regex SpotifyTrackRegex = new(
        @"https?://open\.spotify\.com/track/[A-Za-z0-9]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex YouTubeRegex = new(
        @"https?://(www\.)?(youtube\.com|youtu\.be)/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex OgMetaRegex = new(
        "<meta\\s+(?:property|name)=[\"'](?<key>[^\"']+)[\"']\\s+content=[\"'](?<value>[^\"']*)[\"']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TimestampRegex = new(
        @"(?<!\d)(?:(?<hours>\d{1,2}):)?(?<minutes>\d{1,2}):(?<seconds>\d{2})(?!\d)",
        RegexOptions.Compiled);

    private readonly ISongIdRunStore _store;
    private readonly IMetadataFacade _metadataFacade;
    private readonly IMusicBrainzClient _musicBrainzClient;
    private readonly IAcoustIdClient _acoustIdClient;
    private readonly ICanonicalStatsService _canonicalStatsService;
    private readonly IArtistReleaseGraphService _releaseGraphService;
    private readonly IFingerprintExtractionService _fingerprintExtractionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHubContext<SongIdHub> _songIdHub;
    private readonly ILogger<SongIdService> _logger;
    private readonly IOptionsMonitor<slskd.Options> _optionsMonitor;
    private readonly Func<string, CancellationToken, Task<bool>>? _commandExistsOverride;
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = false,
        SingleWriter = false,
    });
    private readonly ConcurrentDictionary<Guid, byte> _queuedRunIds = new();

    public SongIdService(
        ISongIdRunStore store,
        IMetadataFacade metadataFacade,
        IMusicBrainzClient musicBrainzClient,
        IAcoustIdClient acoustIdClient,
        ICanonicalStatsService canonicalStatsService,
        IArtistReleaseGraphService releaseGraphService,
        IFingerprintExtractionService fingerprintExtractionService,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        IHubContext<SongIdHub> songIdHub,
        ILogger<SongIdService> logger)
        : this(
            store,
            metadataFacade,
            musicBrainzClient,
            acoustIdClient,
            canonicalStatsService,
            releaseGraphService,
            fingerprintExtractionService,
            httpClientFactory,
            optionsMonitor,
            songIdHub,
            logger,
            enableBackgroundWorkers: true,
            commandExistsOverride: null)
    {
    }

    internal SongIdService(
        ISongIdRunStore store,
        IMetadataFacade metadataFacade,
        IMusicBrainzClient musicBrainzClient,
        IAcoustIdClient acoustIdClient,
        ICanonicalStatsService canonicalStatsService,
        IArtistReleaseGraphService releaseGraphService,
        IFingerprintExtractionService fingerprintExtractionService,
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        IHubContext<SongIdHub> songIdHub,
        ILogger<SongIdService> logger,
        bool enableBackgroundWorkers,
        Func<string, CancellationToken, Task<bool>>? commandExistsOverride = null)
    {
        _store = store;
        _metadataFacade = metadataFacade;
        _musicBrainzClient = musicBrainzClient;
        _acoustIdClient = acoustIdClient;
        _canonicalStatsService = canonicalStatsService;
        _releaseGraphService = releaseGraphService;
        _fingerprintExtractionService = fingerprintExtractionService;
        _httpClientFactory = httpClientFactory;
        _optionsMonitor = optionsMonitor;
        _songIdHub = songIdHub;
        _logger = logger;
        _commandExistsOverride = commandExistsOverride;

        if (enableBackgroundWorkers)
        {
            StartWorkers();
            _ = RecoverQueuedRunsAsync();
        }
    }

    public async Task<SongIdRun> QueueAnalyzeAsync(string source, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("SongID source is required", nameof(source));
        }

        var normalizedSource = source.Trim();
        var run = new SongIdRun
        {
            Source = normalizedSource,
            SourceType = DetectSourceType(normalizedSource),
            Status = "queued",
            CreatedAt = DateTimeOffset.UtcNow,
            ArtifactDirectory = GetWorkspaceDirectory(Guid.NewGuid()),
            Summary = "Queued for SongID analysis.",
            CurrentStage = "queued",
            PercentComplete = 0.05,
        };
        run.Id = Guid.Parse(Path.GetFileName(run.ArtifactDirectory));

        _store.Upsert(run);
        await _songIdHub.BroadcastCreateAsync(run).ConfigureAwait(false);
        await EnqueueRunAsync(run).ConfigureAwait(false);
        return run;
    }

    public SongIdRun? Get(Guid id)
    {
        return _store.Get(id);
    }

    public IReadOnlyList<SongIdRun> List(int limit = 25)
    {
        return _store.List(limit);
    }

    private async Task StartWorkerAsync(int workerSlot)
    {
        await foreach (var runId in _queue.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            _queuedRunIds.TryRemove(runId, out _);
            await RefreshQueuePositionsAsync().ConfigureAwait(false);
            var run = _store.Get(runId);
            if (run == null)
            {
                continue;
            }

            if (!string.Equals(run.Status, "queued", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            run.WorkerSlot = workerSlot;
            run.QueuePosition = null;
            await RunAnalysisInBackgroundAsync(run).ConfigureAwait(false);
            run.WorkerSlot = null;
            await PublishRunAsync(run).ConfigureAwait(false);
            await RefreshQueuePositionsAsync().ConfigureAwait(false);
        }
    }

    private async Task RunAnalysisInBackgroundAsync(SongIdRun run)
    {
        try
        {
            await UpdateRunAsync(run, "running", "source_analysis", 0.12, "Starting SongID source analysis.").ConfigureAwait(false);
            var analysis = await AnalyzeSourceAsync(run.Source, run.SourceType, CancellationToken.None).ConfigureAwait(false);
            run.SourceType = analysis.SourceType;
            run.Query = analysis.Query;
            run.Evidence = analysis.Evidence;
            run.Summary = analysis.Summary;
            run.Metadata = analysis.Metadata;
            run.Chapters = analysis.Chapters;
            await PublishRunAsync(run).ConfigureAwait(false);

            if (analysis.ExactTrack != null)
            {
                AddExactTrack(run, analysis.ExactTrack);
            }

            if (analysis.ExactAlbum != null)
            {
                AddExactAlbum(run, analysis.ExactAlbum);
            }

            if (!string.IsNullOrWhiteSpace(analysis.Query))
            {
                await UpdateRunAsync(run, "running", "catalog_candidates", 0.28, "Resolving MusicBrainz and metadata candidates.").ConfigureAwait(false);
                await AddSearchCandidatesAsync(run, analysis.Query, CancellationToken.None).ConfigureAwait(false);
            }

            await UpdateRunAsync(run, "running", "segment_decomposition", 0.34, "Decomposing chapters and timestamped clues into segment-level candidate groups.").ConfigureAwait(false);
            await AddSegmentCandidateGroupsAsync(run, CancellationToken.None).ConfigureAwait(false);
            await UpdateRunAsync(run, "running", "artist_graph", 0.38, "Expanding artist and discography context.").ConfigureAwait(false);
            await AddArtistCandidatesAsync(run, CancellationToken.None).ConfigureAwait(false);
            await UpdateRunAsync(run, "running", "evidence_pipeline", 0.52, "Running SongID evidence pipeline.").ConfigureAwait(false);
            await AddPipelineEvidenceAsync(run, run.Source, CancellationToken.None).ConfigureAwait(false);
            await UpdateRunAsync(run, "running", "native_quality", 0.72, "Applying canonical quality signals from local slskdn evidence.").ConfigureAwait(false);
            await ApplyNativeQualitySignalsAsync(run, CancellationToken.None).ConfigureAwait(false);
            await UpdateRunAsync(run, "running", "reranking", 0.82, "Reranking SongID results using corpus and evidence consensus.").ConfigureAwait(false);
            ApplyCorpusReranking(run);
            BuildPlans(run);
            BuildAcquisitionOptions(run);
            await UpdateRunAsync(run, "running", "corpus_registration", 0.93, "Registering SongID run into the local corpus.").ConfigureAwait(false);
            await RegisterCorpusEntryAsync(run, CancellationToken.None).ConfigureAwait(false);

            run.Status = "completed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.Summary = "SongID analysis complete.";
            run.CurrentStage = "completed";
            run.PercentComplete = 1;
            run.QueuePosition = null;
            await PublishRunAsync(run).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SongID analysis failed for source {Source}", run.Source);
            run.Status = "failed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.Summary = ex.Message;
            run.CurrentStage = "failed";
            run.QueuePosition = null;
            run.Evidence.Add($"Analysis failed: {ex.Message}");
            await PublishRunAsync(run).ConfigureAwait(false);
        }
    }

    private async Task UpdateRunAsync(SongIdRun run, string status, string stage, double percentComplete, string summary)
    {
        run.Status = status;
        run.CurrentStage = stage;
        run.PercentComplete = ClampScore(percentComplete);
        run.Summary = summary;
        await PublishRunAsync(run).ConfigureAwait(false);
    }

    private async Task PublishRunAsync(SongIdRun run)
    {
        _store.Upsert(run);
        await _songIdHub.BroadcastUpdateAsync(run).ConfigureAwait(false);
    }

    private void StartWorkers()
    {
        foreach (var workerSlot in Enumerable.Range(1, GetMaxConcurrentRuns()))
        {
            _ = Task.Run(() => StartWorkerAsync(workerSlot));
        }
    }

    private int GetMaxConcurrentRuns()
    {
        return Math.Max(1, _optionsMonitor.CurrentValue.SongId.MaxConcurrentRuns);
    }

    internal async Task RecoverQueuedRunsAsync()
    {
        try
        {
            var pendingRuns = _store.ListByStatuses(new[] { "queued", "running" }, RecoveryBatchLimit);
            foreach (var run in pendingRuns)
            {
                if (string.Equals(run.Status, "running", StringComparison.OrdinalIgnoreCase))
                {
                    run.Status = "queued";
                    run.CompletedAt = null;
                    run.CurrentStage = "queued";
                    run.PercentComplete = Math.Min(run.PercentComplete, 0.05);
                    run.Summary = "Queued for SongID analysis.";
                    run.WorkerSlot = null;
                    run.Evidence.Add("Recovered after restart and re-queued for SongID analysis.");
                    _store.Upsert(run);
                }

                await EnqueueRunAsync(run, broadcastCreate: false).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to recover queued SongID runs from the run store");
        }
    }

    internal async Task EnqueueRunAsync(SongIdRun run, bool broadcastCreate = false)
    {
        run.Status = "queued";
        run.CurrentStage = "queued";
        run.WorkerSlot = null;
        _store.Upsert(run);

        if (_queuedRunIds.TryAdd(run.Id, 0))
        {
            await _queue.Writer.WriteAsync(run.Id).ConfigureAwait(false);
        }

        await RefreshQueuePositionsAsync().ConfigureAwait(false);
        if (broadcastCreate)
        {
            await _songIdHub.BroadcastCreateAsync(run).ConfigureAwait(false);
        }
    }

    internal async Task RefreshQueuePositionsAsync()
    {
        var queuedRuns = _store.ListByStatuses(new[] { "queued" }, RecoveryBatchLimit)
            .OrderBy(run => run.CreatedAt)
            .ToList();
        var position = 1;
        foreach (var queuedRun in queuedRuns)
        {
            var summary = position == 1
                ? "Queued for SongID analysis. Next to run."
                : $"Queued for SongID analysis. {position - 1} run(s) ahead.";
            var changed = queuedRun.QueuePosition != position ||
                !string.Equals(queuedRun.Summary, summary, StringComparison.Ordinal) ||
                !string.Equals(queuedRun.CurrentStage, "queued", StringComparison.Ordinal);
            if (!changed)
            {
                position++;
                continue;
            }

            queuedRun.QueuePosition = position;
            queuedRun.CurrentStage = "queued";
            queuedRun.Summary = summary;
            _store.Upsert(queuedRun);
            await _songIdHub.BroadcastUpdateAsync(queuedRun).ConfigureAwait(false);
            position++;
        }
    }

    private async Task<SongIdAnalysis> AnalyzeSourceAsync(string source, string sourceType, CancellationToken cancellationToken)
    {
        return sourceType switch
        {
            "local_file" => await AnalyzeLocalFileAsync(source, cancellationToken).ConfigureAwait(false),
            "youtube_url" => await AnalyzeYouTubeAsync(source, cancellationToken).ConfigureAwait(false),
            "spotify_url" => await AnalyzeSpotifyAsync(source, cancellationToken).ConfigureAwait(false),
            _ => AnalyzeFreeText(source),
        };
    }

    private async Task<SongIdAnalysis> AnalyzeLocalFileAsync(string source, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(source);
        var analysis = new SongIdAnalysis
        {
            SourceType = "local_file",
            Summary = $"Analyzed local file `{fileName}`.",
            Metadata = new SongIdMetadata
            {
                Title = Path.GetFileNameWithoutExtension(source),
                AnalysisAudioSource = "local_file",
            },
        };

        analysis.Evidence.Add($"Local file path detected: {source}");

        try
        {
            var fingerprint = await _fingerprintExtractionService.ExtractFingerprintAsync(source, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                analysis.Evidence.Add($"Chromaprint extracted for local file ({fingerprint.Length} chars).");
            }
        }
        catch (Exception ex)
        {
            analysis.Evidence.Add($"Chromaprint extraction failed for local file: {ex.Message}");
        }

        var metadata = await _metadataFacade.GetByFileAsync(source, cancellationToken).ConfigureAwait(false);
        if (metadata == null)
        {
            analysis.Query = Path.GetFileNameWithoutExtension(source);
            analysis.Evidence.Add("No tags or fingerprint match were resolved. Falling back to filename-based SongID query.");
            return analysis;
        }

        if (!string.IsNullOrWhiteSpace(metadata.Artist) || !string.IsNullOrWhiteSpace(metadata.Title))
        {
            analysis.Query = string.Join(" ", new[] { metadata.Artist, metadata.Title }.Where(value => !string.IsNullOrWhiteSpace(value)));
            analysis.Evidence.Add($"MetadataFacade resolved artist/title: {metadata.Artist} - {metadata.Title}");
            analysis.Metadata.Artist = metadata.Artist ?? string.Empty;
            analysis.Metadata.Title = metadata.Title ?? analysis.Metadata.Title;
            analysis.Metadata.Album = metadata.Album ?? string.Empty;
        }
        else
        {
            analysis.Query = Path.GetFileNameWithoutExtension(source);
        }

        if (!string.IsNullOrWhiteSpace(metadata.MusicBrainzRecordingId))
        {
            analysis.ExactTrack = await _musicBrainzClient.GetRecordingAsync(metadata.MusicBrainzRecordingId, cancellationToken).ConfigureAwait(false);
            analysis.Evidence.Add($"Exact MusicBrainz recording match from file analysis: {metadata.MusicBrainzRecordingId}");
        }

        if (!string.IsNullOrWhiteSpace(metadata.MusicBrainzReleaseId))
        {
            analysis.ExactAlbum = await _musicBrainzClient.GetReleaseAsync(metadata.MusicBrainzReleaseId, cancellationToken).ConfigureAwait(false);
            analysis.Evidence.Add($"Exact MusicBrainz release match from file tags: {metadata.MusicBrainzReleaseId}");
        }

        return analysis;
    }

    private async Task<SongIdAnalysis> AnalyzeYouTubeAsync(string source, CancellationToken cancellationToken)
    {
        var analysis = new SongIdAnalysis
        {
            SourceType = "youtube_url",
            Summary = "Analyzed YouTube metadata for SongID query generation.",
            Metadata = new SongIdMetadata
            {
                AnalysisAudioSource = "youtube_audio",
            },
        };

        analysis.Evidence.Add("YouTube URL detected.");

        try
        {
            var result = await RunToolAsync("yt-dlp", new[] { "--dump-single-json", "--skip-download", source }, cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(result.StandardOutput);
            var root = doc.RootElement;
            var track = TryGetString(root, "track");
            var artist = TryGetString(root, "artist");
            var title = TryGetString(root, "title");
            var uploader = TryGetString(root, "uploader");

            analysis.Query = BuildBestQuery(track, artist, title, uploader);
            analysis.Evidence.Add($"yt-dlp metadata extracted query: {analysis.Query}");
            analysis.Chapters = ParseChapterFindings(root);

            if (!string.IsNullOrWhiteSpace(title))
            {
                analysis.Summary = $"Analyzed YouTube source titled `{title}`.";
                analysis.Metadata.Title = title;
            }

            analysis.Metadata.Artist = artist ?? uploader ?? string.Empty;
            analysis.Metadata.Extra["uploader"] = uploader ?? string.Empty;
            if (analysis.Chapters.Count > 0)
            {
                analysis.Metadata.Extra["chapter_count"] = analysis.Chapters.Count.ToString(CultureInfo.InvariantCulture);
                analysis.Metadata.Extra["chapter_titles"] = string.Join(" | ", analysis.Chapters.Select(chapter => chapter.Title).Where(value => !string.IsNullOrWhiteSpace(value)));
                analysis.Evidence.Add($"yt-dlp metadata contained {analysis.Chapters.Count} chapter clue(s).");
            }

            return analysis;
        }
        catch (Exception ex)
        {
            analysis.Query = source;
            analysis.Evidence.Add($"yt-dlp unavailable or failed; falling back to raw URL query. {ex.Message}");
            return analysis;
        }
    }

    private async Task<SongIdAnalysis> AnalyzeSpotifyAsync(string source, CancellationToken cancellationToken)
    {
        var analysis = new SongIdAnalysis
        {
            SourceType = "spotify_url",
            Summary = "Analyzed Spotify page metadata for SongID query generation.",
            Metadata = new SongIdMetadata
            {
                AnalysisAudioSource = "spotify_page",
            },
        };

        analysis.Evidence.Add("Spotify track URL detected.");

        try
        {
            using var httpClient = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, source);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var metadata = ExtractOgMeta(html);
            var title = metadata.TryGetValue("og:title", out var ogTitle) ? ogTitle : null;
            var description = metadata.TryGetValue("og:description", out var ogDescription) ? ogDescription : null;
            var parts = (description ?? string.Empty)
                .Split('·', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var artist = parts.Length > 0 ? parts[0] : null;
            var album = parts.Length > 1 ? parts[1] : null;
            analysis.Query = BuildBestQuery(title, artist, title, album);
            analysis.Evidence.Add($"Spotify page metadata extracted query: {analysis.Query}");
            if (!string.IsNullOrWhiteSpace(album))
            {
                analysis.Evidence.Add($"Spotify album context: {album}");
            }

            analysis.Metadata.Title = title ?? string.Empty;
            analysis.Metadata.Artist = artist ?? string.Empty;
            analysis.Metadata.Album = album ?? string.Empty;
            analysis.Metadata.SpotifyTrackId = ExtractSpotifyTrackId(source);
            analysis.Metadata.PreviewUrl = metadata.TryGetValue("og:audio", out var previewUrl) ? previewUrl : null;

            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
            {
                try
                {
                    var search = await RunToolAsync("yt-dlp", new[] { "--dump-single-json", $"ytsearch5:{title} {artist}" }, cancellationToken).ConfigureAwait(false);
                    using var searchDoc = JsonDocument.Parse(search.StandardOutput);
                    if (searchDoc.RootElement.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
                    {
                        analysis.Metadata.Extra["youtube_candidate_count"] = entries.GetArrayLength().ToString(CultureInfo.InvariantCulture);
                        var firstCandidate = entries.EnumerateArray().FirstOrDefault();
                        var candidateUrl = TryGetString(firstCandidate, "webpage_url");
                        if (!string.IsNullOrWhiteSpace(candidateUrl))
                        {
                            analysis.Metadata.Extra["matched_youtube_url"] = candidateUrl!;
                            analysis.Evidence.Add($"Spotify bridge matched YouTube candidate: {candidateUrl}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    analysis.Evidence.Add($"Spotify YouTube candidate search skipped: {ex.Message}");
                }
            }

            return analysis;
        }
        catch (Exception ex)
        {
            analysis.Query = source;
            analysis.Evidence.Add($"Spotify metadata fetch failed; falling back to raw source query. {ex.Message}");
            return analysis;
        }
    }

    private static SongIdAnalysis AnalyzeFreeText(string source)
    {
        return new SongIdAnalysis
        {
            SourceType = "text_query",
            Query = source,
            Summary = "Using free-text SongID query.",
            Evidence = new List<string> { "Treating input as a direct SongID text query." },
            Metadata = new SongIdMetadata
            {
                Title = source,
                AnalysisAudioSource = "text_query",
            },
        };
    }

    private async Task AddSearchCandidatesAsync(SongIdRun run, string query, CancellationToken cancellationToken)
    {
        await foreach (var hit in _metadataFacade.SearchAsync(query, 8, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(hit.MusicBrainzRecordingId) ||
                run.Tracks.Any(candidate => string.Equals(candidate.RecordingId, hit.MusicBrainzRecordingId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            run.Tracks.Add(new SongIdTrackCandidate
            {
                CandidateId = hit.MusicBrainzRecordingId,
                RecordingId = hit.MusicBrainzRecordingId,
                Title = hit.Title ?? string.Empty,
                Artist = hit.Artist ?? string.Empty,
                MusicBrainzArtistId = hit.MusicBrainzArtistId,
                SearchText = string.Join(" ", new[] { hit.Artist, hit.Title }.Where(value => !string.IsNullOrWhiteSpace(value))),
                IdentityScore = 0.72,
                ByzantineScore = 0.58,
                ActionScore = 0.67,
            });
        }

        if (run.Tracks.Count > 0)
        {
            run.Evidence.Add($"MusicBrainz search produced {run.Tracks.Count} track candidate(s) for query `{query}`.");
        }
    }

    private async Task AddArtistCandidatesAsync(SongIdRun run, CancellationToken cancellationToken)
    {
        foreach (var group in run.Tracks
                     .Where(track => !string.IsNullOrWhiteSpace(track.MusicBrainzArtistId))
                     .GroupBy(track => new { track.MusicBrainzArtistId, track.Artist })
                     .Take(4))
        {
            var artistId = group.Key.MusicBrainzArtistId!;
            ArtistReleaseGraph? releaseGraph = null;

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(ArtistGraphFetchTimeout);
                releaseGraph = await _releaseGraphService.GetArtistReleaseGraphAsync(artistId, false, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                run.Evidence.Add($"Artist graph fetch timed out for {group.Key.Artist} ({artistId}); continuing with lightweight discography planning.");
            }
            catch (Exception ex)
            {
                run.Evidence.Add($"Artist graph fetch failed for {group.Key.Artist} ({artistId}); continuing with lightweight discography planning. {ex.Message}");
            }

            run.Artists.Add(new SongIdArtistCandidate
            {
                CandidateId = artistId,
                ArtistId = artistId,
                Name = string.IsNullOrWhiteSpace(releaseGraph?.Name) ? group.Key.Artist : releaseGraph.Name,
                ReleaseGroupCount = releaseGraph?.ReleaseGroups?.Count ?? 0,
                IdentityScore = 0.68,
                ByzantineScore = Math.Min(0.92, 0.45 + ((releaseGraph?.ReleaseGroups?.Count ?? 0) / 25.0)),
                ActionScore = Math.Min(0.95, 0.55 + ((releaseGraph?.ReleaseGroups?.Count ?? 0) / 30.0)),
            });
        }

        if (run.Artists.Count > 0)
        {
            run.Evidence.Add($"Prepared {run.Artists.Count} artist candidate(s) with discography actions.");
        }
    }

    private async Task AddSegmentCandidateGroupsAsync(SongIdRun run, CancellationToken cancellationToken)
    {
        run.Segments.Clear();

        foreach (var segmentQuery in BuildSegmentQueries(run).Take(MaxSegmentGroups))
        {
            var group = new SongIdSegmentResult
            {
                SegmentId = segmentQuery.Id,
                Label = segmentQuery.Label,
                SourceLabel = segmentQuery.SourceLabel,
                Query = segmentQuery.Query,
                DecompositionLabel = BuildDecompositionLabel(segmentQuery),
                StartSeconds = segmentQuery.StartSeconds,
                Confidence = segmentQuery.Confidence,
            };

            var candidates = new List<SongIdTrackCandidate>();
            var seenRecordingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await foreach (var hit in _metadataFacade.SearchAsync(segmentQuery.Query, MaxSegmentCandidatesPerGroup, cancellationToken).ConfigureAwait(false))
            {
                var candidate = CreateSegmentCandidateFromMetadata(hit, segmentQuery, candidates.Count);
                if (candidate == null || !seenRecordingIds.Add(candidate.RecordingId))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            var recordingHits = await _musicBrainzClient
                .SearchRecordingsAsync(segmentQuery.Query, MaxSegmentCandidatesPerGroup, cancellationToken)
                .ConfigureAwait(false);
            foreach (var hit in recordingHits)
            {
                if (string.IsNullOrWhiteSpace(hit.RecordingId) || !seenRecordingIds.Add(hit.RecordingId))
                {
                    continue;
                }

                candidates.Add(CreateSegmentCandidateFromRecordingSearch(hit, segmentQuery, candidates.Count));
                if (candidates.Count >= MaxSegmentCandidatesPerGroup)
                {
                    break;
                }
            }

            group.Candidates = candidates
                .OrderByDescending(candidate => candidate.ActionScore)
                .ThenByDescending(candidate => candidate.IdentityScore)
                .Take(MaxSegmentCandidatesPerGroup)
                .ToList();
            group.Plans = BuildSegmentPlans(group);
            group.Options = BuildSegmentOptions(group);
            run.Segments.Add(group);
        }

        if (run.Segments.Count > 0)
        {
            var candidateCount = run.Segments.Sum(segment => segment.Candidates.Count);
            run.Evidence.Add($"Segment decomposition built {run.Segments.Count} segment group(s) with {candidateCount} explicit candidate(s).");
        }
    }

    private static void AddExactTrack(SongIdRun run, TrackTarget track)
    {
        if (string.IsNullOrWhiteSpace(track.MusicBrainzRecordingId))
        {
            return;
        }

        if (run.Tracks.Any(candidate => string.Equals(candidate.RecordingId, track.MusicBrainzRecordingId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        run.Tracks.Insert(0, new SongIdTrackCandidate
        {
            CandidateId = track.MusicBrainzRecordingId,
            RecordingId = track.MusicBrainzRecordingId,
            Title = track.Title,
            Artist = track.Artist,
            IsExact = true,
            SearchText = string.Join(" ", new[] { track.Artist, track.Title }.Where(value => !string.IsNullOrWhiteSpace(value))),
            IdentityScore = 0.96,
            ByzantineScore = 0.89,
            ActionScore = 0.93,
        });
    }

    private static void AddExactAlbum(SongIdRun run, AlbumTarget album)
    {
        if (string.IsNullOrWhiteSpace(album.MusicBrainzReleaseId))
        {
            return;
        }

        if (run.Albums.Any(candidate => string.Equals(candidate.ReleaseId, album.MusicBrainzReleaseId, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        run.Albums.Add(new SongIdAlbumCandidate
        {
            CandidateId = album.MusicBrainzReleaseId,
            ReleaseId = album.MusicBrainzReleaseId,
            Title = album.Title,
            Artist = album.Artist,
            MusicBrainzArtistId = album.MusicBrainzArtistId,
            TrackCount = album.Tracks?.Count ?? 0,
            IsExact = true,
            IdentityScore = 0.93,
            ByzantineScore = Math.Min(0.94, 0.70 + ((album.Tracks?.Count ?? 0) / 100.0)),
            ActionScore = Math.Min(0.96, 0.75 + ((album.Tracks?.Count ?? 0) / 120.0)),
        });
    }

    private static void BuildPlans(SongIdRun run)
    {
        run.Plans.Clear();
        run.MixGroups = BuildMixGroups(run.Segments);
        if (run.MixGroups.Count > 0)
        {
            run.Evidence.Add($"Detected {run.MixGroups.Count} mix cluster(s) covering {run.MixGroups.Sum(group => group.SegmentCount)} segment clues.");
        }

        foreach (var track in run.Tracks.Take(3))
        {
            run.Plans.Add(new SongIdPlan
            {
                PlanId = $"track:{track.RecordingId}",
                Kind = "track",
                Title = $"{track.Artist} - {track.Title}",
                Subtitle = track.IsExact ? "Exact track match with immediate search handoff." : "Strong track candidate from SongID evidence.",
                ActionLabel = "Search Song",
                TargetId = track.RecordingId,
                SearchText = track.SearchText,
                IdentityScore = track.IdentityScore,
                ByzantineScore = track.ByzantineScore,
                ActionScore = track.ActionScore,
            });
        }

        foreach (var segmentPlan in BuildSegmentTrackPlans(run))
        {
            run.Plans.Add(segmentPlan);
        }

        foreach (var mix in run.MixGroups)
        {
            run.Plans.Add(CreateMixPlan(mix));
        }

        foreach (var album in run.Albums.Take(2))
        {
            run.Plans.Add(new SongIdPlan
            {
                PlanId = $"album:{album.ReleaseId}",
                Kind = "album",
                Title = $"{album.Artist} - {album.Title}",
                Subtitle = $"{album.TrackCount} track(s) available for completion planning.",
                ActionLabel = "Prepare Album",
                TargetId = album.ReleaseId,
                IdentityScore = album.IdentityScore,
                ByzantineScore = album.ByzantineScore,
                ActionScore = album.ActionScore,
            });
        }

        foreach (var artist in run.Artists.Take(2))
        {
            run.Plans.Add(new SongIdPlan
            {
                PlanId = $"artist:{artist.ArtistId}",
                Kind = "artist",
                Title = artist.Name,
                Subtitle = $"{artist.ReleaseGroupCount} release group(s) available for discography planning.",
                ActionLabel = "Plan Discography",
                TargetId = artist.ArtistId,
                Profile = artist.RecommendedProfile,
                IdentityScore = artist.IdentityScore,
                ByzantineScore = artist.ByzantineScore,
                ActionScore = artist.ActionScore,
            });
        }

        run.Plans = run.Plans
            .OrderByDescending(plan => plan.ActionScore)
            .ThenByDescending(plan => plan.IdentityScore)
            .ThenByDescending(plan => plan.ByzantineScore)
            .ToList();
    }

    private async Task ApplyNativeQualitySignalsAsync(SongIdRun run, CancellationToken cancellationToken)
    {
        foreach (var track in run.Tracks.Where(track => !string.IsNullOrWhiteSpace(track.RecordingId)))
        {
            var variants = await _canonicalStatsService
                .GetCanonicalVariantCandidatesAsync(track.RecordingId, cancellationToken)
                .ConfigureAwait(false);
            if (variants.Count == 0)
            {
                continue;
            }

            SongIdScoring.ApplyCanonicalTrackSignals(track, variants);
            run.Evidence.Add($"Native canonical stats found {variants.Count} variant(s) for {track.Artist} - {track.Title}.");
        }

        SongIdScoring.ApplyRunQualityConsensus(run);
    }

    private static void ApplyCorpusReranking(SongIdRun run)
    {
        if (run.CorpusMatches.Count == 0)
        {
            return;
        }

        SongIdScoring.ApplyCorpusReranking(run);
        SongIdScoring.ApplyCorpusFamilyHints(run);
        var topCorpusMatch = run.CorpusMatches[0];
        run.Evidence.Add($"Corpus reranking boosted nearby catalog candidates using {topCorpusMatch.Label ?? topCorpusMatch.Source ?? "the best local match"}.");
        if (run.ForensicMatrix != null &&
            !string.IsNullOrWhiteSpace(run.ForensicMatrix.FamilyLabel) &&
            !string.Equals(run.ForensicMatrix.FamilyLabel, "none", StringComparison.OrdinalIgnoreCase))
        {
            run.Evidence.Add($"Corpus family reuse reinforced the forensic family hint: {run.ForensicMatrix.FamilyLabel}.");
        }
    }

    private static void BuildAcquisitionOptions(SongIdRun run)
    {
        run.Options.Clear();

        foreach (var track in run.Tracks.Take(2))
        {
            AddTrackOption(
                run,
                track,
                "balanced",
                "Search Song",
                "Route this SongID hit into a standard slskdn search and let smart source ranking take over.",
                track.SearchText,
                baseQualityScore: 0.74,
                readinessBonus: 0.06);

            AddTrackOption(
                run,
                track,
                "lossless",
                "Search Lossless",
                "Bias toward canonical FLAC-class matches when quality matters more than breadth.",
                AppendSearchFilter(track.SearchText, "islossless minbitrate:700"),
                baseQualityScore: 0.92,
                readinessBonus: 0.02);

            AddTrackOption(
                run,
                track,
                "wide",
                "Search Wide",
                "Open recall to more candidate sources when the exact rip is harder to find.",
                AppendSearchFilter(track.SearchText, "minbitrate:192"),
                baseQualityScore: 0.62,
                readinessBonus: 0.10);
        }

        var fanoutQueries = run.Tracks
            .Take(3)
            .Where(track => !track.IsExact && !string.IsNullOrWhiteSpace(track.SearchText))
            .Select(track => track.SearchText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (fanoutQueries.Count >= 2)
        {
            var topActionScore = run.Tracks.FirstOrDefault()?.ActionScore ?? 0;
            run.Options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"track-fanout:{run.Id:D}",
                Scope = "track",
                Mode = "fanout",
                Title = run.Metadata.Title is { Length: > 0 } ? run.Metadata.Title : run.Query,
                Description = "Run multiple top candidate searches when SongID has several plausible matches and no single exact identity.",
                ActionKind = "track_search_batch",
                ActionLabel = "Search Top Candidates",
                TargetId = run.Id.ToString("D"),
                SearchTexts = fanoutQueries,
                QualityScore = Math.Min(0.88, topActionScore),
                ByzantineScore = Math.Min(0.92, run.Tracks.Take(3).Average(track => track.ByzantineScore) + 0.04),
                ReadinessScore = Math.Min(0.96, run.Tracks.Take(3).Average(track => track.ActionScore) + 0.06),
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(
                    run.Tracks.Take(3).Average(track => track.IdentityScore),
                    Math.Min(0.88, topActionScore),
                    Math.Min(0.92, run.Tracks.Take(3).Average(track => track.ByzantineScore) + 0.04),
                    Math.Min(0.96, run.Tracks.Take(3).Average(track => track.ActionScore) + 0.06)),
            });
        }

        AddFallbackOptions(run);

        foreach (var segmentOption in BuildSegmentOptions(run))
        {
            run.Options.Add(segmentOption);
        }

        foreach (var album in run.Albums.Take(2))
        {
            run.Options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"album-prepare:{album.ReleaseId}",
                Scope = "album",
                Mode = "prepare",
                Title = $"{album.Artist} - {album.Title}",
                Description = "Resolve and cache the MusicBrainz release so album completion, canonical matching, and downstream jobs can use it.",
                ActionKind = "album_prepare",
                ActionLabel = "Prepare Album",
                TargetId = album.ReleaseId,
                QualityScore = Math.Min(0.95, album.IdentityScore + 0.02),
                ByzantineScore = album.ByzantineScore,
                ReadinessScore = album.ActionScore,
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(album.IdentityScore, Math.Min(0.95, album.IdentityScore + 0.02), album.ByzantineScore, album.ActionScore),
            });

            run.Options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"album-job:{album.ReleaseId}",
                Scope = "album",
                Mode = "job",
                Title = $"{album.Artist} - {album.Title}",
                Description = "Create a single-release download job for this album so SongID can hand off directly into slskdn job planning.",
                ActionKind = "mb_release_job",
                ActionLabel = "Download Album",
                TargetId = album.ReleaseId,
                QualityScore = Math.Min(0.97, album.IdentityScore + 0.03),
                ByzantineScore = Math.Min(0.97, album.ByzantineScore + 0.02),
                ReadinessScore = Math.Min(0.98, album.ActionScore + 0.04),
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(album.IdentityScore, Math.Min(0.97, album.IdentityScore + 0.03), Math.Min(0.97, album.ByzantineScore + 0.02), Math.Min(0.98, album.ActionScore + 0.04)),
            });
        }

        foreach (var artist in run.Artists.Take(2))
        {
            run.Options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"artist-core:{artist.ArtistId}",
                Scope = "artist",
                Mode = "core",
                Title = artist.Name,
                Description = "Focus on core albums first for a high-signal, lower-noise discography pull.",
                ActionKind = "discography_job",
                ActionLabel = "Plan Discography",
                TargetId = artist.ArtistId,
                Profile = artist.RecommendedProfile,
                QualityScore = Math.Min(0.93, artist.IdentityScore + 0.03),
                ByzantineScore = artist.ByzantineScore,
                ReadinessScore = artist.ActionScore,
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(artist.IdentityScore, Math.Min(0.93, artist.IdentityScore + 0.03), artist.ByzantineScore, artist.ActionScore),
            });

            run.Options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"artist-extended:{artist.ArtistId}",
                Scope = "artist",
                Mode = "extended",
                Title = artist.Name,
                Description = "Include EPs and selected live material when you want broader coverage without going fully exhaustive.",
                ActionKind = "discography_job",
                ActionLabel = "Plan Extended",
                TargetId = artist.ArtistId,
                Profile = "ExtendedDiscography",
                QualityScore = Math.Min(0.9, artist.IdentityScore),
                ByzantineScore = Math.Min(0.96, artist.ByzantineScore + 0.03),
                ReadinessScore = Math.Max(0.45, artist.ActionScore - 0.04),
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(artist.IdentityScore, Math.Min(0.9, artist.IdentityScore), Math.Min(0.96, artist.ByzantineScore + 0.03), Math.Max(0.45, artist.ActionScore - 0.04)),
            });

            run.Options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"artist-all:{artist.ArtistId}",
                Scope = "artist",
                Mode = "all",
                Title = artist.Name,
                Description = "Go exhaustive across albums, singles, compilations, and edge releases when recall beats curation.",
                ActionKind = "discography_job",
                ActionLabel = "Plan Full Catalog",
                TargetId = artist.ArtistId,
                Profile = "AllReleases",
                QualityScore = Math.Max(0.55, artist.IdentityScore - 0.06),
                ByzantineScore = Math.Min(0.98, artist.ByzantineScore + 0.05),
                ReadinessScore = Math.Max(0.40, artist.ActionScore - 0.08),
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(artist.IdentityScore, Math.Max(0.55, artist.IdentityScore - 0.06), Math.Min(0.98, artist.ByzantineScore + 0.05), Math.Max(0.40, artist.ActionScore - 0.08)),
            });
        }

        run.Options = run.Options
            .OrderByDescending(option => option.OverallScore)
            .ThenByDescending(option => option.QualityScore)
            .ThenByDescending(option => option.ByzantineScore)
            .Take(12)
            .ToList();
    }

    private static void AddFallbackOptions(SongIdRun run)
    {
        var identityVerdict = run.IdentityAssessment?.Verdict ?? run.Assessment?.Verdict ?? string.Empty;
        if (!string.Equals(identityVerdict, "likely_uncataloged_or_original", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(identityVerdict, "likely_ai_or_channel_original", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(identityVerdict, "needs_manual_review", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fallbackQueries = new List<string>();
        AddFallbackQuery(fallbackQueries, BuildBestQuery(run.Metadata.Artist, run.Metadata.Title));
        AddFallbackQuery(fallbackQueries, run.Query);
        AddFallbackQuery(fallbackQueries, BuildBestQuery(TryGetMetadataValue(run.Metadata.Extra, "uploader"), run.Metadata.Title));
        AddFallbackQuery(fallbackQueries, BuildBestQuery(TryGetMetadataValue(run.Metadata.Extra, "uploader"), run.Metadata.Album, run.Metadata.Title));

        if (fallbackQueries.Count == 0)
        {
            return;
        }

        var familyLabel = run.ForensicMatrix?.FamilyLabel;
        var description = string.Equals(identityVerdict, "likely_ai_or_channel_original", StringComparison.OrdinalIgnoreCase)
            ? "Search broad text variants for a likely channel-original or AI-mediated source without pretending SongID has a clean catalog identity."
            : "Search broad text variants for a likely uncataloged or source-original item when SongID has context but not a strong catalog match.";
        if (!string.IsNullOrWhiteSpace(familyLabel) && !string.Equals(familyLabel, "none", StringComparison.OrdinalIgnoreCase))
        {
            description = $"{description} Synthetic family hint: {familyLabel}.";
        }

        run.Options.Add(new SongIdAcquisitionOption
        {
            OptionId = $"fallback-search:{run.Id:D}",
            Scope = "fallback",
            Mode = "source_original",
            Title = run.Metadata.Title is { Length: > 0 } ? run.Metadata.Title : run.Query,
            Description = description,
            ActionKind = fallbackQueries.Count > 1 ? "track_search_batch" : "track_search",
            ActionLabel = fallbackQueries.Count > 1 ? "Search Source Variants" : "Search Source Text",
            TargetId = run.Id.ToString("D"),
            SearchText = fallbackQueries[0],
            SearchTexts = fallbackQueries,
            QualityScore = 0.38,
            ByzantineScore = Math.Max(0.42, (run.ForensicMatrix?.KnownFamilyScore ?? 0) / 100.0),
            ReadinessScore = 0.58,
            OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(0.32, 0.38, Math.Max(0.42, (run.ForensicMatrix?.KnownFamilyScore ?? 0) / 100.0), 0.58),
        });
    }

    private static void AddTrackOption(
        SongIdRun run,
        SongIdTrackCandidate track,
        string mode,
        string actionLabel,
        string description,
        string searchText,
        double baseQualityScore,
        double readinessBonus)
    {
        var readiness = Math.Min(0.98, track.ActionScore + readinessBonus);
        var qualityScore = SongIdScoring.ComputeTrackSearchQualityScore(track, baseQualityScore);
        run.Options.Add(new SongIdAcquisitionOption
        {
            OptionId = $"track-{mode}:{track.RecordingId}",
            Scope = "track",
            Mode = mode,
            Title = $"{track.Artist} - {track.Title}",
            Description = description,
            ActionKind = "track_search",
            ActionLabel = actionLabel,
            TargetId = track.RecordingId,
            SearchText = searchText,
            QualityScore = qualityScore,
            ByzantineScore = track.ByzantineScore,
            ReadinessScore = readiness,
            OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(track.IdentityScore, qualityScore, track.ByzantineScore, readiness),
        });
    }

    private static double ClampScore(double value)
    {
        return Math.Max(0, Math.Min(1, value));
    }

    private static IEnumerable<SongIdPlan> BuildSegmentTrackPlans(SongIdRun run)
    {
        if (run.Segments.Count > 0)
        {
            foreach (var segment in run.Segments.Take(4))
            {
                foreach (var plan in segment.Plans.Take(2))
                {
                    yield return plan;
                }
            }

            yield break;
        }

        foreach (var segment in BuildSegmentQueries(run).Take(4))
        {
            yield return CreateSegmentSearchPlan(segment);
        }
    }

    private static IEnumerable<SongIdAcquisitionOption> BuildSegmentOptions(SongIdRun run)
    {
        if (run.Segments.Count > 0)
        {
            return run.Segments
                .SelectMany(segment => segment.Options.Take(2))
                .Take(6)
                .ToList();
        }

        return BuildSegmentQueries(run)
            .Take(4)
            .Select(CreateSegmentSearchOption)
            .ToList();
    }

    private static List<SongIdSegmentQuery> BuildSegmentQueries(SongIdRun run)
    {
        var segments = new List<SongIdSegmentQuery>();
        var seenQueries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var artistContext = !string.IsNullOrWhiteSpace(run.Metadata.Artist)
            ? run.Metadata.Artist
            : run.Tracks.FirstOrDefault()?.Artist ?? string.Empty;

        foreach (var chapter in run.Chapters)
        {
            var cleaned = CleanSegmentTitle(chapter.Title);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var query = BuildBestQuery(artistContext, cleaned);
            if (!TryAddSegmentQuery(segments, seenQueries, query, $"Chapter {FormatTimestamp(chapter.StartSeconds)}", $"chapter \"{chapter.Title}\"", chapter.StartSeconds, 0.54))
            {
                continue;
            }
        }

        foreach (var comment in run.Comments.Where(comment => comment.TimestampSeconds.HasValue))
        {
            var cleaned = CleanSegmentTitle(RemoveTimestampText(comment.Text));
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                continue;
            }

            var query = BuildBestQuery(artistContext, cleaned);
            TryAddSegmentQuery(
                segments,
                seenQueries,
                query,
                $"Comment {FormatTimestamp(comment.TimestampSeconds ?? 0)}",
                comment.Author is { Length: > 0 } ? $"comment by {comment.Author}" : "timestamped comment",
                comment.TimestampSeconds ?? 0,
                0.48);
        }

        return segments
            .OrderBy(segment => segment.StartSeconds)
            .ThenByDescending(segment => segment.Confidence)
            .ToList();
    }

    private static bool TryAddSegmentQuery(
        List<SongIdSegmentQuery> segments,
        HashSet<string> seenQueries,
        string query,
        string label,
        string sourceLabel,
        int startSeconds,
        double confidence)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        var normalized = NormalizeSegmentQuery(query);
        if (string.IsNullOrWhiteSpace(normalized) || !seenQueries.Add(normalized))
        {
            return false;
        }

        segments.Add(new SongIdSegmentQuery
        {
            Id = $"{startSeconds}-{normalized}",
            Label = label,
            Query = query,
            SourceLabel = sourceLabel,
            StartSeconds = startSeconds,
            Confidence = confidence,
        });
        return true;
    }

    private static SongIdTrackCandidate? CreateSegmentCandidateFromMetadata(MetadataResult hit, SongIdSegmentQuery segmentQuery, int rank)
    {
        if (string.IsNullOrWhiteSpace(hit.MusicBrainzRecordingId))
        {
            return null;
        }

        return new SongIdTrackCandidate
        {
            CandidateId = $"{segmentQuery.Id}:{hit.MusicBrainzRecordingId}",
            RecordingId = hit.MusicBrainzRecordingId,
            Title = hit.Title ?? string.Empty,
            Artist = hit.Artist ?? string.Empty,
            MusicBrainzArtistId = hit.MusicBrainzArtistId,
            SearchText = string.Join(" ", new[] { hit.Artist, hit.Title }.Where(value => !string.IsNullOrWhiteSpace(value))),
            IdentityScore = Math.Max(0.46, segmentQuery.Confidence + 0.16 - (rank * 0.05)),
            ByzantineScore = Math.Max(0.42, segmentQuery.Confidence + 0.08 - (rank * 0.04)),
            ActionScore = Math.Max(0.5, segmentQuery.Confidence + 0.18 - (rank * 0.04)),
        };
    }

    private static SongIdTrackCandidate CreateSegmentCandidateFromRecordingSearch(RecordingSearchHit hit, SongIdSegmentQuery segmentQuery, int rank)
    {
        return new SongIdTrackCandidate
        {
            CandidateId = $"{segmentQuery.Id}:{hit.RecordingId}",
            RecordingId = hit.RecordingId,
            Title = hit.Title,
            Artist = hit.Artist,
            MusicBrainzArtistId = hit.MusicBrainzArtistId,
            SearchText = string.Join(" ", new[] { hit.Artist, hit.Title }.Where(value => !string.IsNullOrWhiteSpace(value))),
            IdentityScore = Math.Max(0.42, segmentQuery.Confidence + 0.10 - (rank * 0.05)),
            ByzantineScore = Math.Max(0.39, segmentQuery.Confidence + 0.04 - (rank * 0.04)),
            ActionScore = Math.Max(0.46, segmentQuery.Confidence + 0.12 - (rank * 0.04)),
        };
    }

    private static List<SongIdPlan> BuildSegmentPlans(SongIdSegmentResult segment)
    {
        var plans = new List<SongIdPlan>();

        foreach (var candidate in segment.Candidates.Take(2))
        {
            plans.Add(new SongIdPlan
            {
                PlanId = $"segment-track:{segment.SegmentId}:{candidate.RecordingId}",
                Kind = "track",
                Title = $"{segment.Label}: {candidate.Artist} - {candidate.Title}",
                Subtitle = $"Segment-derived track candidate from {segment.SourceLabel}.",
                ActionLabel = "Search Segment Song",
                TargetId = candidate.RecordingId,
                SearchText = candidate.SearchText,
                IdentityScore = candidate.IdentityScore,
                ByzantineScore = candidate.ByzantineScore,
                ActionScore = candidate.ActionScore,
            });
        }

        plans.Add(CreateSegmentSearchPlan(new SongIdSegmentQuery
        {
            Id = segment.SegmentId,
            Label = segment.Label,
            Query = segment.Query,
            SourceLabel = segment.SourceLabel,
            StartSeconds = segment.StartSeconds,
            Confidence = segment.Confidence,
        }));

        return plans
            .OrderByDescending(plan => plan.ActionScore)
            .ThenByDescending(plan => plan.IdentityScore)
            .ToList();
    }

    private static List<SongIdMixGroup> BuildMixGroups(IReadOnlyList<SongIdSegmentResult> segments)
    {
        if (segments == null || segments.Count < MinSegmentsPerMix)
        {
            return new List<SongIdMixGroup>();
        }

        var groups = new List<SongIdMixGroup>();
        var ordered = segments.OrderBy(segment => segment.StartSeconds).ToList();
        var currentGroup = new List<SongIdSegmentResult> { ordered[0] };
        for (var i = 1; i < ordered.Count; i++)
        {
            var candidate = ordered[i];
            var gap = candidate.StartSeconds - currentGroup.Last().StartSeconds;
            if (gap <= MixGapThresholdSeconds)
            {
                currentGroup.Add(candidate);
                continue;
            }

            if (currentGroup.Count >= MinSegmentsPerMix)
            {
                groups.Add(CreateMixGroup(currentGroup));
            }

            currentGroup = new List<SongIdSegmentResult> { candidate };
        }

        if (currentGroup.Count >= MinSegmentsPerMix)
        {
            groups.Add(CreateMixGroup(currentGroup));
        }

        return groups;
    }

    private static SongIdMixGroup CreateMixGroup(IReadOnlyCollection<SongIdSegmentResult> segments)
    {
        var first = segments.First();
        var searchText = string.Join(
            " + ",
            segments
                .Select(segment => segment.Query)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase));

        var identityScores = segments
            .Select(segment => segment.Candidates.FirstOrDefault()?.IdentityScore ?? segment.Confidence)
            .ToList();
        var byzantineScores = segments
            .Select(segment => segment.Candidates.FirstOrDefault()?.ByzantineScore ?? segment.Confidence)
            .ToList();
        var actionScores = segments
            .Select(segment => segment.Candidates.FirstOrDefault()?.ActionScore ?? segment.Confidence)
            .ToList();

        return new SongIdMixGroup
        {
            MixId = $"mix-{first.SegmentId}",
            Label = $"Mix cluster starting at {first.Label}",
            SegmentIds = segments.Select(segment => segment.SegmentId).ToList(),
            Confidence = segments.Average(segment => segment.Confidence),
            IdentityScore = identityScores.Any() ? identityScores.Average() : 0,
            ByzantineScore = byzantineScores.Any() ? byzantineScores.Average() : 0,
            ActionScore = actionScores.Any() ? actionScores.Average() : 0,
            SearchText = string.IsNullOrWhiteSpace(searchText) ? first.Query : searchText,
        };
    }

    private static SongIdPlan CreateMixPlan(SongIdMixGroup mix)
    {
        return new SongIdPlan
        {
            PlanId = mix.MixId,
            Kind = "mix",
            Title = mix.Label,
            Subtitle = $"Mix of {mix.SegmentCount} segment clues",
            ActionLabel = "Search Mix",
            TargetId = mix.MixId,
            SearchText = mix.SearchText,
            IdentityScore = mix.IdentityScore,
            ByzantineScore = mix.ByzantineScore,
            ActionScore = mix.ActionScore,
        };
    }

    private static List<SongIdAcquisitionOption> BuildSegmentOptions(SongIdSegmentResult segment)
    {
        var options = new List<SongIdAcquisitionOption>();
        var candidateSearches = segment.Candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.SearchText))
            .Select(candidate => candidate.SearchText!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (candidateSearches.Count >= 2)
        {
            var topCandidate = segment.Candidates[0];
            var qualityScore = SongIdScoring.ComputeTrackSearchQualityScore(topCandidate, 0.61);
            var byzantineScore = Math.Min(0.9, segment.Candidates.Take(3).Average(candidate => candidate.ByzantineScore) + 0.03);
            var readinessScore = Math.Min(0.94, segment.Candidates.Take(3).Average(candidate => candidate.ActionScore) + 0.05);
            var identityScore = Math.Max(segment.Confidence, topCandidate.IdentityScore);
            options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"segment-fanout:{segment.SegmentId}",
                Scope = "track",
                Mode = "segment_fanout",
                Title = $"{segment.Label}: top candidates",
                Description = $"Search the strongest decomposed candidates together for this segment from {segment.SourceLabel}.",
                ActionKind = "track_search_batch",
                ActionLabel = "Search Segment Candidates",
                TargetId = segment.SegmentId,
                SearchText = candidateSearches[0],
                SearchTexts = candidateSearches,
                QualityScore = qualityScore,
                ByzantineScore = byzantineScore,
                ReadinessScore = readinessScore,
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(identityScore, qualityScore, byzantineScore, readinessScore),
            });
        }

        foreach (var candidate in segment.Candidates.Take(2))
        {
            var qualityScore = SongIdScoring.ComputeTrackSearchQualityScore(candidate, 0.63);
            var readinessScore = Math.Min(0.93, candidate.ActionScore + 0.04);
            options.Add(new SongIdAcquisitionOption
            {
                OptionId = $"segment-candidate:{segment.SegmentId}:{candidate.RecordingId}",
                Scope = "track",
                Mode = "segment_candidate",
                Title = $"{segment.Label}: {candidate.Artist} - {candidate.Title}",
                Description = $"Use the decomposed segment candidate inferred from {segment.SourceLabel}.",
                ActionKind = "track_search",
                ActionLabel = "Search Segment Song",
                TargetId = candidate.RecordingId,
                SearchText = candidate.SearchText,
                QualityScore = qualityScore,
                ByzantineScore = candidate.ByzantineScore,
                ReadinessScore = readinessScore,
                OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(candidate.IdentityScore, qualityScore, candidate.ByzantineScore, readinessScore),
            });
        }

        options.Add(CreateSegmentSearchOption(new SongIdSegmentQuery
        {
            Id = segment.SegmentId,
            Label = segment.Label,
            Query = segment.Query,
            SourceLabel = segment.SourceLabel,
            StartSeconds = segment.StartSeconds,
            Confidence = segment.Confidence,
        }));

        return options
            .OrderByDescending(option => option.OverallScore)
            .ThenByDescending(option => option.ReadinessScore)
            .ToList();
    }

    private static SongIdPlan CreateSegmentSearchPlan(SongIdSegmentQuery segment)
    {
        return new SongIdPlan
        {
            PlanId = $"segment:{segment.Id}",
            Kind = "track",
            Title = segment.Label,
            Subtitle = $"Segment-derived track search from {segment.SourceLabel}.",
            ActionLabel = "Search Segment",
            TargetId = segment.Id,
            SearchText = segment.Query,
            IdentityScore = segment.Confidence,
            ByzantineScore = Math.Min(0.84, segment.Confidence + 0.10),
            ActionScore = Math.Min(0.9, segment.Confidence + 0.14),
        };
    }

    private static SongIdAcquisitionOption CreateSegmentSearchOption(SongIdSegmentQuery segment)
    {
        const double qualityScore = 0.58;
        const double byzantineScore = 0.61;
        const double readinessScore = 0.68;

        return new SongIdAcquisitionOption
        {
            OptionId = $"segment-search:{segment.Id}",
            Scope = "track",
            Mode = "segment",
            Title = segment.Label,
            Description = $"Search a segment-derived candidate from {segment.SourceLabel}.",
            ActionKind = "track_search",
            ActionLabel = "Search Segment",
            TargetId = segment.Id,
            SearchText = segment.Query,
            QualityScore = qualityScore,
            ByzantineScore = byzantineScore,
            ReadinessScore = readinessScore,
            OverallScore = SongIdScoring.ComputeIdentityFirstOverallScore(segment.Confidence, qualityScore, byzantineScore, readinessScore),
        };
    }

    private static string BuildDecompositionLabel(SongIdSegmentQuery segmentQuery)
    {
        return $"Segment @ {FormatTimestamp(segmentQuery.StartSeconds)} from {segmentQuery.SourceLabel}";
    }

    private static string AppendSearchFilter(string searchText, string filter)
    {
        return string.Join(" ", new[] { searchText?.Trim(), filter?.Trim() }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AddFallbackQuery(List<string> fallbackQueries, string? query)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) ||
            fallbackQueries.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        fallbackQueries.Add(trimmed);
    }

    private async Task AddPipelineEvidenceAsync(SongIdRun run, string source, CancellationToken cancellationToken)
    {
        var assets = await PrepareAnalysisAssetsAsync(run, source, cancellationToken).ConfigureAwait(false);
        run.ArtifactDirectory = assets.WorkspacePath;
        run.Scorecard.SourceType = run.SourceType;
        run.Scorecard.AnalysisAudioSource = assets.AnalysisAudioSource;
        run.Scorecard.SpotifyTrackIdPresent = !string.IsNullOrWhiteSpace(run.Metadata.SpotifyTrackId);
        run.Scorecard.SpotifyPreviewUrlPresent = !string.IsNullOrWhiteSpace(run.Metadata.PreviewUrl);
        run.Scorecard.MatchedYoutubeCandidatePresent = string.Equals(assets.AnalysisAudioSource, "youtube_candidate", StringComparison.OrdinalIgnoreCase);
        run.Scorecard.YoutubeCandidateCount = ParseInt(run.Metadata.Extra, "youtube_candidate_count");
        run.Scorecard.EmbeddedMetadataKeys = BuildEmbeddedMetadataKeys(run.Metadata);
        run.Scorecard.EmbeddedMetadataPresent = run.Scorecard.EmbeddedMetadataKeys.Count > 0;

        if (!string.IsNullOrWhiteSpace(run.Metadata.Title))
        {
            run.Evidence.Add($"SongID pipeline title context: {run.Metadata.Title}");
        }

        var commentTimestamps = new List<int>();
        if (assets.CommentsPath != null)
        {
            commentTimestamps = AddCommentFindings(run, assets.CommentsPath);
        }

        var focusTimestamps = commentTimestamps
            .Concat(run.Chapters.Select(chapter => chapter.StartSeconds))
            .Distinct()
            .OrderBy(value => value)
            .ToList();

        if (assets.AudioPath != null)
        {
            run.FullSourceFingerprint = await CreateFullSourceFingerprintAsync(run, assets.AudioPath, cancellationToken).ConfigureAwait(false);
            run.CorpusMatches = await FindCorpusMatchesAsync(run, cancellationToken).ConfigureAwait(false);
            run.Provenance = await ScanProvenanceSignalsAsync(run.Metadata, assets.AudioPath, cancellationToken).ConfigureAwait(false);
            run.Perturbations = await AnalyzePerturbationsAsync(run, assets.AudioPath, assets.DurationSeconds, focusTimestamps, cancellationToken).ConfigureAwait(false);

            var panakoJar = LocatePanakoJar();
            var panakoStore = await StorePanakoSourceAsync(run, panakoJar, assets.AudioPath, cancellationToken).ConfigureAwait(false);
            var audfprintScript = LocateAudfprintScript();
            var audfprintDatabasePath = await EnsureAudfprintDatabaseAsync(run, audfprintScript, assets.AudioPath, cancellationToken).ConfigureAwait(false);
            var stems = await SeparateStemsDemucsAsync(run, assets.AudioPath, assets.DurationSeconds, focusTimestamps, cancellationToken).ConfigureAwait(false);
            if (stems.Count > 0)
            {
                run.Stems = stems;
            }

            var transcriptAudio = run.Stems.FirstOrDefault(stem => string.Equals(stem.ArtifactId, "vocals", StringComparison.OrdinalIgnoreCase))?.Path ?? assets.AudioPath;
            await AddTranscriptFindingsAsync(run, transcriptAudio, assets.DurationSeconds, focusTimestamps, cancellationToken).ConfigureAwait(false);
            await AddClipFindingsAsync(
                run,
                assets.AudioPath,
                assets.DurationSeconds,
                focusTimestamps,
                panakoStore.JarPath,
                audfprintScript,
                audfprintDatabasePath,
                cancellationToken).ConfigureAwait(false);
        }

        if (assets.VideoPath != null)
        {
            await AddOcrFindingsAsync(run, assets.VideoPath, assets.DurationSeconds, focusTimestamps, cancellationToken).ConfigureAwait(false);
        }

        run.AiHeuristics = BuildAggregateAiHeuristics(run.Clips);
        run.Scorecard.ClipCount = run.Clips.Count;
        run.Scorecard.AcoustIdHitCount = run.Clips.Count(clip => !string.IsNullOrWhiteSpace(clip.AcoustId?.RecordingId));
        run.Scorecard.RawAcoustIdHitCount = run.Clips.Count(clip => clip.AcoustId != null);
        run.Scorecard.SongRecHitCount = run.Clips.Count(clip => clip.SongRec != null);
        run.Scorecard.SongRecDistinctMatchCount = run.Clips
            .Select(clip => clip.SongRec)
            .Where(finding => finding != null)
            .Cast<SongIdRecognizerFinding>()
            .Select(finding => string.Join(" | ", new[] { finding.ExternalId, finding.Artist, finding.Title }.Where(value => !string.IsNullOrWhiteSpace(value))))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        run.Scorecard.PanakoHitCount = run.Clips.Count(clip => clip.Panako != null);
        run.Scorecard.AudfprintHitCount = run.Clips.Count(clip => clip.Audfprint != null);
        run.Scorecard.CorpusMatchCount = run.CorpusMatches.Count;
        run.Scorecard.TranscriptCount = run.Transcripts.Count;
        run.Scorecard.OcrCount = run.Ocr.Count;
        run.Scorecard.CommentFindingCount = run.Comments.Count;
        run.Scorecard.TimestampHintCount = run.Comments.Count(comment => comment.TimestampSeconds.HasValue);
        run.Scorecard.ChapterHintCount = run.Chapters.Count;
        run.Scorecard.PlaylistRequestCount = run.Comments.Count(comment => LooksLikePlaylistRequest(comment.Text));
        run.Scorecard.AiCommentMentionCount = run.Comments.Count(comment => LooksSyntheticComment(comment.Text));
        run.Scorecard.ProvenanceSignalCount = run.Provenance.SignalCount;
        run.Scorecard.ProvenanceSignals = run.Provenance.Signals;
        run.Scorecard.AiArtifactClipCount = run.Clips.Count(clip => clip.AiHeuristics != null);
        run.Scorecard.HighAiArtifactClipCount = run.Clips.Count(clip => string.Equals(clip.AiHeuristics?.ArtifactLabel, "high", StringComparison.OrdinalIgnoreCase));
        run.Scorecard.MeanAiArtifactScore = run.AiHeuristics?.ArtifactScore ?? 0;
        run.Scorecard.MaxAiArtifactScore = run.Clips.Count == 0
            ? 0
            : run.Clips.Max(clip => clip.AiHeuristics?.ArtifactScore ?? 0);

        run.IdentityAssessment = SongIdScoring.BuildIdentityAssessment(run);
        run.Assessment = run.IdentityAssessment;
        run.ForensicMatrix = SongIdScoring.BuildForensicMatrix(run);
        run.SyntheticAssessment = SongIdScoring.BuildSyntheticAssessment(run, run.ForensicMatrix);

        if (!string.IsNullOrWhiteSpace(run.IdentityAssessment.Summary))
        {
            run.Evidence.Add(run.IdentityAssessment.Summary);
        }

        if (!string.IsNullOrWhiteSpace(run.SyntheticAssessment.Summary))
        {
            run.Evidence.Add($"Synthetic likelihood: {run.SyntheticAssessment.Summary}");
        }

        if (run.CorpusMatches.Count > 0)
        {
            run.Evidence.Add($"SongID corpus reranking found {run.CorpusMatches.Count} local similarity match(es); top score {run.CorpusMatches[0].SimilarityScore:F2}.");
        }
    }

    private async Task<PreparedAnalysisAssets> PrepareAnalysisAssetsAsync(SongIdRun run, string source, CancellationToken cancellationToken)
    {
        var workspace = run.ArtifactDirectory;
        Directory.CreateDirectory(workspace);

        return run.SourceType switch
        {
            "local_file" => await PrepareLocalAssetsAsync(workspace, source, cancellationToken).ConfigureAwait(false),
            "youtube_url" => await PrepareYouTubeAssetsAsync(workspace, run, source, cancellationToken).ConfigureAwait(false),
            "spotify_url" => await PrepareSpotifyAssetsAsync(workspace, run, cancellationToken).ConfigureAwait(false),
            _ => new PreparedAnalysisAssets { WorkspacePath = workspace, AnalysisAudioSource = "text_query" },
        };
    }

    private async Task<PreparedAnalysisAssets> PrepareLocalAssetsAsync(string workspace, string source, CancellationToken cancellationToken)
    {
        if (!File.Exists(source))
        {
            return new PreparedAnalysisAssets { WorkspacePath = workspace };
        }

        var duration = await GetDurationSecondsAsync(source, cancellationToken).ConfigureAwait(false);
        return new PreparedAnalysisAssets
        {
            WorkspacePath = workspace,
            AudioPath = source,
            VideoPath = LooksLikeVideo(source) ? source : null,
            DurationSeconds = duration,
            AnalysisAudioSource = "local_file",
        };
    }

    private async Task<PreparedAnalysisAssets> PrepareYouTubeAssetsAsync(string workspace, SongIdRun run, string source, CancellationToken cancellationToken)
    {
        if (!await CommandExistsAsync("yt-dlp", cancellationToken).ConfigureAwait(false))
        {
            run.Evidence.Add("yt-dlp unavailable; skipping YouTube audio, video, and comment extraction. Continuing with metadata-only SongID analysis.");
            return new PreparedAnalysisAssets
            {
                WorkspacePath = workspace,
                AnalysisAudioSource = "youtube_metadata",
            };
        }

        var downloadDir = Path.Combine(workspace, "download");
        Directory.CreateDirectory(downloadDir);
        var audioOutput = Path.Combine(downloadDir, "source-audio.%(ext)s");
        await RunToolAsync("yt-dlp", new[] { "-f", "bestaudio", "-o", audioOutput, source }, cancellationToken).ConfigureAwait(false);

        var audioPath = ResolveDownloadedFile(downloadDir, "source-audio");
        string? videoPath = null;

        try
        {
            var videoDir = Path.Combine(workspace, "video");
            Directory.CreateDirectory(videoDir);
            var videoOutput = Path.Combine(videoDir, "source-video.%(ext)s");
            await RunToolAsync(
                "yt-dlp",
                new[] { "-f", "bestvideo[height<=360]+bestaudio/best[height<=360]/best", "-o", videoOutput, source },
                cancellationToken).ConfigureAwait(false);
            videoPath = ResolveDownloadedFile(videoDir, "source-video");
        }
        catch (Exception ex)
        {
            run.Evidence.Add($"Video capture skipped for OCR: {ex.Message}");
        }

        string? commentsPath = null;
        try
        {
            var commentsDir = Path.Combine(workspace, "comments");
            Directory.CreateDirectory(commentsDir);
            var commentsOutput = Path.Combine(commentsDir, "%(id)s.%(ext)s");
            await RunToolAsync(
                "yt-dlp",
                new[]
                {
                    "--skip-download",
                    "--write-comments",
                    "--write-info-json",
                    "--extractor-args",
                    $"youtube:max_comments={MaxComments}",
                    "-o",
                    commentsOutput,
                    source,
                },
                cancellationToken).ConfigureAwait(false);
            commentsPath = Directory.EnumerateFiles(commentsDir, "*.info.json").FirstOrDefault();
        }
        catch (Exception ex)
        {
            run.Evidence.Add($"Comment harvesting skipped: {ex.Message}");
        }

        return new PreparedAnalysisAssets
        {
            WorkspacePath = workspace,
            AudioPath = audioPath,
            VideoPath = videoPath,
            CommentsPath = commentsPath,
            DurationSeconds = await GetDurationSecondsAsync(audioPath, cancellationToken).ConfigureAwait(false),
            AnalysisAudioSource = "youtube_audio",
        };
    }

    private async Task<PreparedAnalysisAssets> PrepareSpotifyAssetsAsync(string workspace, SongIdRun run, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(workspace);

        if (!string.IsNullOrWhiteSpace(run.Metadata.PreviewUrl))
        {
            var audioPath = Path.Combine(workspace, "spotify-preview.mp3");
            using var client = _httpClientFactory.CreateClient();
            var bytes = await client.GetByteArrayAsync(run.Metadata.PreviewUrl!, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(audioPath, bytes, cancellationToken).ConfigureAwait(false);
            return new PreparedAnalysisAssets
            {
                WorkspacePath = workspace,
                AudioPath = audioPath,
                DurationSeconds = await GetDurationSecondsAsync(audioPath, cancellationToken).ConfigureAwait(false),
                AnalysisAudioSource = "spotify_preview",
            };
        }

        var youtubeUrl = TryGetMetadataValue(run.Metadata.Extra, "matched_youtube_url");
        if (!string.IsNullOrWhiteSpace(youtubeUrl))
        {
            var assets = await PrepareYouTubeAssetsAsync(workspace, run, youtubeUrl, cancellationToken).ConfigureAwait(false);
            assets.AnalysisAudioSource = "youtube_candidate";
            return assets;
        }

        return new PreparedAnalysisAssets
        {
            WorkspacePath = workspace,
            AnalysisAudioSource = "spotify_page",
        };
    }

    private async Task AddClipFindingsAsync(
        SongIdRun run,
        string audioPath,
        int durationSeconds,
        IReadOnlyCollection<int> commentTimestamps,
        string? panakoJarPath,
        string? audfprintScriptPath,
        string? audfprintDatabasePath,
        CancellationToken cancellationToken)
    {
        var profiles = ParseProfiles(ClipProfiles);
        var focusedStarts = BuildFocusStarts(durationSeconds, commentTimestamps, profiles);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (clipLength, step) in profiles)
        {
            var starts = new List<int>();
            starts.AddRange(BuildClipStarts(durationSeconds, clipLength, step).Take(MaxBaseClipsPerProfile));
            starts.AddRange(focusedStarts.Where(item => item.ClipLength == clipLength && item.Step == step).Select(item => item.StartSeconds).Take(MaxFocusedClipsPerProfile));

            foreach (var start in starts.Distinct().OrderBy(value => value))
            {
                var actualDuration = Math.Min(clipLength, Math.Max(0, durationSeconds - start));
                if (actualDuration < 15)
                {
                    continue;
                }

                var clipId = $"clip-{clipLength}-{step}-{start}";
                if (!seen.Add(clipId))
                {
                    continue;
                }

                var clipPath = Path.Combine(run.ArtifactDirectory, "clips", $"{clipId}.flac");
                Directory.CreateDirectory(Path.GetDirectoryName(clipPath)!);
                await ExtractClipAsync(audioPath, clipPath, start, actualDuration, cancellationToken).ConfigureAwait(false);

                var finding = new SongIdClipFinding
                {
                    ClipId = clipId,
                    Profile = $"{clipLength}:{step}",
                    StartSeconds = start,
                    DurationSeconds = actualDuration,
                };

                try
                {
                    var fingerprint = await _fingerprintExtractionService.ExtractFingerprintAsync(clipPath, cancellationToken).ConfigureAwait(false);
                    finding.Fingerprint = fingerprint ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(fingerprint))
                    {
                        var acoustId = await _acoustIdClient.LookupAsync(fingerprint, 11025, actualDuration, cancellationToken).ConfigureAwait(false);
                        if (acoustId != null)
                        {
                            finding.AcoustId = MapAcoustIdFinding(acoustId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    run.Evidence.Add($"Clip fingerprint lookup failed for {finding.ClipId}: {ex.Message}");
                }

                try
                {
                    if (await CommandExistsAsync("songrec", cancellationToken).ConfigureAwait(false))
                    {
                        var songrec = await RunToolAsync("songrec", new[] { "audio-file-to-recognized-song", clipPath }, cancellationToken).ConfigureAwait(false);
                        finding.SongRec = ParseSongRecFinding(songrec.StandardOutput);
                    }
                }
                catch (Exception ex)
                {
                    run.Evidence.Add($"SongRec lookup failed for {finding.ClipId}: {ex.Message}");
                }

                try
                {
                    finding.AiHeuristics = await ScoreAiAudioArtifactsAsync(clipPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    run.Evidence.Add($"AI artifact heuristic skipped for {finding.ClipId}: {ex.Message}");
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(panakoJarPath))
                    {
                        finding.Panako = await QueryPanakoClipAsync(run, panakoJarPath, clipPath, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    run.Evidence.Add($"Panako lookup failed for {finding.ClipId}: {ex.Message}");
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(audfprintScriptPath) && !string.IsNullOrWhiteSpace(audfprintDatabasePath))
                    {
                        finding.Audfprint = await QueryAudfprintAsync(audfprintScriptPath, audfprintDatabasePath, clipPath, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    run.Evidence.Add($"Audfprint lookup failed for {finding.ClipId}: {ex.Message}");
                }

                run.Clips.Add(finding);
            }
        }
    }

    private async Task AddTranscriptFindingsAsync(
        SongIdRun run,
        string audioPath,
        int durationSeconds,
        IReadOnlyCollection<int> commentTimestamps,
        CancellationToken cancellationToken)
    {
        if (!await CommandExistsAsync("whisper", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var (transcriptAudioPath, excerptStart) = await PrepareAnalysisExcerptAsync(run, audioPath, "whisper_excerpt", durationSeconds, commentTimestamps, WhisperExcerptSeconds, cancellationToken).ConfigureAwait(false);
        var transcriptOutput = Path.Combine(run.ArtifactDirectory, "reports", "transcripts");
        Directory.CreateDirectory(transcriptOutput);

        try
        {
            await RunToolAsync(
                "whisper",
                new[] { transcriptAudioPath, "--model", "base", "--output_dir", transcriptOutput, "--output_format", "json", "--fp16", "False" },
                cancellationToken).ConfigureAwait(false);

            var transcriptPath = Directory.EnumerateFiles(transcriptOutput, $"{Path.GetFileNameWithoutExtension(transcriptAudioPath)}.json").FirstOrDefault();
            if (string.IsNullOrWhiteSpace(transcriptPath) || !File.Exists(transcriptPath))
            {
                return;
            }

            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(transcriptPath, cancellationToken).ConfigureAwait(false));
            var root = doc.RootElement;
            var text = TryGetString(root, "text")?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var segments = root.TryGetProperty("segments", out var segmentArray) && segmentArray.ValueKind == JsonValueKind.Array
                ? segmentArray.GetArrayLength()
                : 0;
            var phrases = ExtractTranscriptQueries(text);
            run.Transcripts.Add(new SongIdTranscriptFinding
            {
                TranscriptId = $"transcript-{Path.GetFileNameWithoutExtension(transcriptPath)}",
                Source = "whisper",
                Text = text,
                SegmentCount = segments,
                Language = TryGetString(root, "language"),
                ExcerptStartSeconds = excerptStart,
                ExcerptDurationSeconds = Math.Min(WhisperExcerptSeconds, durationSeconds),
                MusicBrainzQueries = phrases,
            });

            foreach (var phrase in phrases.Take(2))
            {
                await AddSearchCandidatesAsync(run, phrase, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            run.Evidence.Add($"Transcript extraction skipped: {ex.Message}");
        }
    }

    private async Task AddOcrFindingsAsync(SongIdRun run, string videoPath, int durationSeconds, IReadOnlyCollection<int> commentTimestamps, CancellationToken cancellationToken)
    {
        if (!await CommandExistsAsync("tesseract", cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var timestamps = commentTimestamps
            .Concat(CollectEvenlySpacedTimestamps(durationSeconds, 3))
            .Distinct()
            .OrderBy(value => value)
            .Take(12)
            .ToList();

        foreach (var timestamp in timestamps)
        {
            var framePath = Path.Combine(run.ArtifactDirectory, "reports", "frames", $"frame-{timestamp}.png");
            Directory.CreateDirectory(Path.GetDirectoryName(framePath)!);

            try
            {
                await RunToolAsync(
                    "ffmpeg",
                    new[] { "-hide_banner", "-loglevel", "error", "-y", "-ss", timestamp.ToString(CultureInfo.InvariantCulture), "-i", videoPath, "-frames:v", "1", framePath },
                    cancellationToken).ConfigureAwait(false);

                var ocr = await RunToolAsync("tesseract", new[] { framePath, "stdout", "-l", "eng" }, cancellationToken).ConfigureAwait(false);
                var text = ocr.StandardOutput.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    run.Ocr.Add(new SongIdOcrFinding
                    {
                        OcrId = $"ocr-{timestamp}",
                        TimestampSeconds = timestamp,
                        Text = text,
                    });
                }
            }
            catch (Exception ex)
            {
                run.Evidence.Add($"OCR skipped at {timestamp}s: {ex.Message}");
            }
        }
    }

    private List<int> AddCommentFindings(SongIdRun run, string commentsPath)
    {
        var timestamps = new List<int>();
        using var doc = JsonDocument.Parse(File.ReadAllText(commentsPath));
        if (!doc.RootElement.TryGetProperty("comments", out var comments) || comments.ValueKind != JsonValueKind.Array)
        {
            return timestamps;
        }

        foreach (var comment in comments.EnumerateArray().Take(MaxComments))
        {
            var text = TryGetString(comment, "text");
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var timestamp = ParseTimestamp(text);
            if (timestamp.HasValue)
            {
                timestamps.Add(timestamp.Value);
            }

            if (timestamp == null && !LooksInterestingComment(text))
            {
                continue;
            }

            run.Comments.Add(new SongIdCommentFinding
            {
                CommentId = TryGetString(comment, "id") ?? Guid.NewGuid().ToString("N"),
                Author = TryGetString(comment, "author") ?? string.Empty,
                Text = text,
                TimestampSeconds = timestamp,
            });
        }

        return timestamps.Distinct().OrderBy(value => value).ToList();
    }

    private static SongIdRecognizerFinding? MapAcoustIdFinding(AcoustIdResult result)
    {
        var recording = result.Recordings?.FirstOrDefault();
        return new SongIdRecognizerFinding
        {
            RecordingId = recording?.Id,
            ExternalId = result.Id,
            Title = recording?.Title ?? string.Empty,
            Artist = recording?.Artists?.FirstOrDefault()?.Name ?? string.Empty,
            Score = result.Score,
            Summary = $"AcoustID score {result.Score:F2}",
        };
    }

    private static SongIdRecognizerFinding? ParseSongRecFinding(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(stdout);
        var root = doc.RootElement;
        if (!root.TryGetProperty("track", out var track) || track.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var title = TryGetString(track, "title");
        var artist = TryGetString(track, "subtitle");
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
        {
            return null;
        }

        var matchCount = root.TryGetProperty("matches", out var matches) && matches.ValueKind == JsonValueKind.Array ? matches.GetArrayLength() : 0;
        return new SongIdRecognizerFinding
        {
            Title = title ?? string.Empty,
            Artist = artist ?? string.Empty,
            ExternalId = TryGetString(track, "key"),
            MatchCount = matchCount,
            Score = matchCount > 0 ? Math.Min(0.98, 0.78 + (matchCount * 0.03)) : 0.85,
            Summary = "SongRec recognition hit",
        };
    }

    private static SongIdRecognizerFinding? ParsePanakoFinding(string stdout)
    {
        foreach (var raw in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var line = raw.Trim();
            if (!Regex.IsMatch(line, @"^\d+\s*;\s*\d+\s*;"))
            {
                continue;
            }

            var parts = line.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length < 13)
            {
                continue;
            }

            if (!double.TryParse(parts[9], NumberStyles.Any, CultureInfo.InvariantCulture, out var matchScore))
            {
                continue;
            }

            return new SongIdRecognizerFinding
            {
                Title = Path.GetFileNameWithoutExtension(parts[5]),
                SourcePath = parts[5],
                ExternalId = parts[6],
                Score = Math.Min(0.99, matchScore / 100.0),
                Summary = $"Panako score {matchScore.ToString("F2", CultureInfo.InvariantCulture)}",
            };
        }

        return null;
    }

    private static SongIdRecognizerFinding? ParseAudfprintFinding(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bestLine = lines.FirstOrDefault(line => line.Contains("Matched", StringComparison.OrdinalIgnoreCase) || line.Contains("match", StringComparison.OrdinalIgnoreCase)) ?? lines.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(bestLine))
        {
            return null;
        }

        var scoreMatch = Regex.Match(bestLine, @"(?<score>\d+(\.\d+)?)");
        var score = scoreMatch.Success && double.TryParse(scoreMatch.Groups["score"].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedScore)
            ? Math.Min(0.99, parsedScore / 100.0)
            : 0.55;
        return new SongIdRecognizerFinding
        {
            Title = bestLine,
            Score = score,
            Summary = "Audfprint fingerprint match",
        };
    }

    private static List<(int ClipLength, int Step)> ParseProfiles(string spec)
    {
        return spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
            .Select(parts => (int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture)))
            .ToList();
    }

    private static IEnumerable<int> BuildClipStarts(int durationSeconds, int clipLength, int step)
    {
        if (durationSeconds <= 0)
        {
            yield break;
        }

        if (durationSeconds <= clipLength)
        {
            yield return 0;
            yield break;
        }

        var lastStart = Math.Max(0, durationSeconds - clipLength);
        for (var start = 0; start <= lastStart; start += Math.Max(1, step))
        {
            yield return start;
        }
    }

    private static IEnumerable<int> CollectEvenlySpacedTimestamps(int durationSeconds, int count)
    {
        if (durationSeconds <= 0 || count <= 0)
        {
            return Array.Empty<int>();
        }

        var step = Math.Max(1, durationSeconds / (count + 1));
        return Enumerable.Range(1, count).Select(index => step * index);
    }

    private static HashSet<(int ClipLength, int Step, int StartSeconds)> BuildFocusStarts(int durationSeconds, IReadOnlyCollection<int> commentTimestamps, IReadOnlyCollection<(int ClipLength, int Step)> profiles)
    {
        var focused = new HashSet<(int ClipLength, int Step, int StartSeconds)>();
        foreach (var (clipLength, step) in profiles)
        {
            foreach (var timestamp in commentTimestamps)
            {
                foreach (var offset in new[] { -20, 0, 20 })
                {
                    var start = Math.Max(0, Math.Min(Math.Max(0, durationSeconds - clipLength), timestamp + offset));
                    focused.Add((clipLength, step, start));
                }
            }
        }

        return focused;
    }

    private async Task ExtractClipAsync(string sourcePath, string clipPath, int startSeconds, int durationSeconds, CancellationToken cancellationToken)
    {
        await RunToolAsync(
            "ffmpeg",
            new[]
            {
                "-hide_banner",
                "-loglevel",
                "error",
                "-y",
                "-ss",
                startSeconds.ToString(CultureInfo.InvariantCulture),
                "-t",
                durationSeconds.ToString(CultureInfo.InvariantCulture),
                "-i",
                sourcePath,
                "-vn",
                "-map_metadata",
                "-1",
                "-c:a",
                "flac",
                clipPath,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SongIdFingerprintFinding?> CreateFullSourceFingerprintAsync(SongIdRun run, string audioPath, CancellationToken cancellationToken)
    {
        if (!await CommandExistsAsync("fpcalc", cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var outputPath = Path.Combine(run.ArtifactDirectory, "reports", "source.full.fp.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var result = await RunToolAsync("fpcalc", new[] { "-length", "0", "-algorithm", "2", audioPath }, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(outputPath, result.StandardOutput + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        var duration = ParseFpcalcDuration(result.StandardOutput);
        var fingerprint = ParseFpcalcFingerprint(result.StandardOutput);
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        return new SongIdFingerprintFinding
        {
            Path = outputPath,
            DurationSeconds = duration,
            FingerprintLength = fingerprint.Length,
        };
    }

    private async Task<List<SongIdCorpusMatch>> FindCorpusMatchesAsync(SongIdRun run, CancellationToken cancellationToken)
    {
        if (run.FullSourceFingerprint == null ||
            string.IsNullOrWhiteSpace(run.FullSourceFingerprint.Path) ||
            !File.Exists(run.FullSourceFingerprint.Path))
        {
            return new List<SongIdCorpusMatch>();
        }

        var currentFingerprint = ParseFpcalcFingerprint(await File.ReadAllTextAsync(run.FullSourceFingerprint.Path, cancellationToken).ConfigureAwait(false));
        if (string.IsNullOrWhiteSpace(currentFingerprint))
        {
            return new List<SongIdCorpusMatch>();
        }

        var corpusDir = GetCorpusDirectory();
        if (!Directory.Exists(corpusDir))
        {
            return new List<SongIdCorpusMatch>();
        }

        var matches = new List<SongIdCorpusMatch>();
        foreach (var metadataPath in Directory.EnumerateFiles(corpusDir, "*.json"))
        {
            SongIdCorpusEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<SongIdCorpusEntry>(await File.ReadAllTextAsync(metadataPath, cancellationToken).ConfigureAwait(false));
            }
            catch
            {
                continue;
            }

            if (entry == null ||
                string.Equals(entry.RunId, run.Id.ToString("D"), StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(entry.FingerprintPath) ||
                !File.Exists(entry.FingerprintPath))
            {
                continue;
            }

            var otherFingerprint = ParseFpcalcFingerprint(await File.ReadAllTextAsync(entry.FingerprintPath, cancellationToken).ConfigureAwait(false));
            var similarity = CompareFingerprints(currentFingerprint, otherFingerprint);
            if (similarity < 0.18)
            {
                continue;
            }

            matches.Add(new SongIdCorpusMatch
            {
                MatchId = entry.RunId ?? Path.GetFileNameWithoutExtension(metadataPath),
                Label = entry.Label ?? entry.Source ?? "songid-corpus",
                Source = entry.Source ?? string.Empty,
                SimilarityScore = Math.Round(similarity, 4),
                FingerprintPath = entry.FingerprintPath,
                RecordingId = entry.RecordingId,
                Artist = entry.Artist,
                Title = entry.Title,
                FamilyLabel = entry.FamilyLabel,
                KnownFamilyScore = entry.KnownFamilyScore,
            });
        }

        return matches
            .OrderByDescending(match => match.SimilarityScore)
            .Take(5)
            .ToList();
    }

    private async Task RegisterCorpusEntryAsync(SongIdRun run, CancellationToken cancellationToken)
    {
        if (run.FullSourceFingerprint == null ||
            string.IsNullOrWhiteSpace(run.FullSourceFingerprint.Path) ||
            !File.Exists(run.FullSourceFingerprint.Path))
        {
            return;
        }

        var corpusDir = GetCorpusDirectory();
        Directory.CreateDirectory(corpusDir);
        var topTrack = run.Tracks.FirstOrDefault();
        var entry = new SongIdCorpusEntry
        {
            RunId = run.Id.ToString("D"),
            Source = run.Source,
            Label = BuildBestQuery(run.Metadata.Artist, run.Metadata.Title),
            FingerprintPath = run.FullSourceFingerprint.Path,
            RecordingId = topTrack?.RecordingId,
            Artist = topTrack?.Artist ?? run.Metadata.Artist,
            Title = topTrack?.Title ?? run.Metadata.Title,
            FamilyLabel = run.ForensicMatrix?.FamilyLabel,
            KnownFamilyScore = run.ForensicMatrix?.KnownFamilyScore ?? 0,
        };

        var path = Path.Combine(corpusDir, $"{run.Id:D}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(entry), cancellationToken).ConfigureAwait(false);
    }

    private async Task<(string AudioPath, int StartSeconds)> PrepareAnalysisExcerptAsync(
        SongIdRun run,
        string sourcePath,
        string prefix,
        int durationSeconds,
        IReadOnlyCollection<int> commentTimestamps,
        int excerptSeconds,
        CancellationToken cancellationToken)
    {
        if (excerptSeconds <= 0 || durationSeconds <= excerptSeconds)
        {
            return (sourcePath, 0);
        }

        var start = ChooseExcerptStart(commentTimestamps, durationSeconds, excerptSeconds);
        var excerptPath = Path.Combine(run.ArtifactDirectory, "reports", "excerpts", $"{prefix}_{start}_{excerptSeconds}.flac");
        Directory.CreateDirectory(Path.GetDirectoryName(excerptPath)!);
        if (!File.Exists(excerptPath))
        {
            await ExtractClipAsync(sourcePath, excerptPath, start, excerptSeconds, cancellationToken).ConfigureAwait(false);
        }

        return (excerptPath, start);
    }

    private static int ChooseExcerptStart(IReadOnlyCollection<int> commentTimestamps, int durationSeconds, int excerptSeconds)
    {
        if (commentTimestamps.Count > 0)
        {
            var target = Math.Max(0, commentTimestamps.First() - Math.Min(20, excerptSeconds / 4));
            return Math.Min(target, Math.Max(0, durationSeconds - excerptSeconds));
        }

        return 0;
    }

    private async Task<List<SongIdArtifactFinding>> SeparateStemsDemucsAsync(SongIdRun run, string sourcePath, int durationSeconds, IReadOnlyCollection<int> commentTimestamps, CancellationToken cancellationToken)
    {
        if (!await CommandExistsAsync("demucs", cancellationToken).ConfigureAwait(false))
        {
            return new List<SongIdArtifactFinding>();
        }

        var stemsRoot = Path.Combine(run.ArtifactDirectory, "reports", "stems");
        Directory.CreateDirectory(stemsRoot);
        var (excerptPath, _) = await PrepareAnalysisExcerptAsync(run, sourcePath, "demucs_excerpt", durationSeconds, commentTimestamps, DemucsExcerptSeconds, cancellationToken).ConfigureAwait(false);

        await RunToolAsync("demucs", new[] { "--two-stems=vocals", "-o", stemsRoot, excerptPath }, cancellationToken).ConfigureAwait(false);
        var stemDir = Path.Combine(stemsRoot, "htdemucs", Path.GetFileNameWithoutExtension(excerptPath));
        if (!Directory.Exists(stemDir))
        {
            return new List<SongIdArtifactFinding>();
        }

        var artifacts = new List<SongIdArtifactFinding>();
        foreach (var fileName in new[] { "vocals.wav", "no_vocals.wav", "drums.wav", "bass.wav", "other.wav" })
        {
            var path = Path.Combine(stemDir, fileName);
            if (File.Exists(path))
            {
                artifacts.Add(new SongIdArtifactFinding
                {
                    ArtifactId = Path.GetFileNameWithoutExtension(fileName),
                    Label = Path.GetFileNameWithoutExtension(fileName),
                    Path = path,
                });
            }
        }

        if (artifacts.Count > 0)
        {
            run.Evidence.Add($"Demucs generated {artifacts.Count} stem artifact(s).");
        }

        return artifacts;
    }

    private async Task<SongIdProvenanceFinding> ScanProvenanceSignalsAsync(SongIdMetadata metadata, string audioPath, CancellationToken cancellationToken)
    {
        try
        {
            var toolAvailable = await CommandExistsAsync("c2patool", cancellationToken).ConfigureAwait(false);
            var ffprobe = await RunToolAsync(
                "ffprobe",
                new[] { "-v", "error", "-show_format", "-show_streams", "-print_format", "json", audioPath },
                cancellationToken).ConfigureAwait(false);
            using var ffprobeDoc = JsonDocument.Parse(ffprobe.StandardOutput);
            var texts = new List<string>();
            CollectMetadataStrings(metadata, texts);
            CollectJsonStrings(ffprobeDoc.RootElement, texts);

            var patterns = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase)
            {
                ["c2pa"] = new(@"\bc2pa\b", RegexOptions.IgnoreCase),
                ["content credentials"] = new(@"\bcontent credentials\b", RegexOptions.IgnoreCase),
                ["contentcredentials"] = new(@"\bcontentcredentials\b", RegexOptions.IgnoreCase),
                ["synthid"] = new(@"\bsynthid\b", RegexOptions.IgnoreCase),
                ["generated with ai"] = new(@"\bgenerated with ai\b", RegexOptions.IgnoreCase),
                ["ai-generated"] = new(@"\bai-generated\b", RegexOptions.IgnoreCase),
                ["ai generated"] = new(@"\bai generated\b", RegexOptions.IgnoreCase),
                ["suno"] = new(@"\bsuno\b", RegexOptions.IgnoreCase),
                ["udio"] = new(@"\budio\b", RegexOptions.IgnoreCase),
            };

            var matches = patterns
                .Where(pattern => texts.Any(text => pattern.Value.IsMatch(text)))
                .Select(pattern => pattern.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var manifestHint = matches.Any(match =>
                string.Equals(match, "c2pa", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(match, "content credentials", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(match, "contentcredentials", StringComparison.OrdinalIgnoreCase));
            var verified = false;
            string? validationState = null;

            if (toolAvailable)
            {
                try
                {
                    var c2pa = await RunToolAsync("c2patool", new[] { audioPath }, cancellationToken).ConfigureAwait(false);
                    var lowered = $"{c2pa.StandardOutput}\n{c2pa.StandardError}".ToLowerInvariant();
                    if (lowered.Contains("active manifest", StringComparison.Ordinal) || lowered.Contains("manifest", StringComparison.Ordinal))
                    {
                        manifestHint = true;
                    }

                    if (lowered.Contains("valid", StringComparison.Ordinal))
                    {
                        verified = true;
                        validationState = "valid";
                    }
                    else if (lowered.Contains("invalid", StringComparison.Ordinal))
                    {
                        validationState = "invalid";
                    }
                    else if (lowered.Contains("manifest", StringComparison.Ordinal))
                    {
                        validationState = "present";
                    }
                }
                catch
                {
                    validationState ??= "tool_failed";
                }
            }

            return new SongIdProvenanceFinding
            {
                SignalCount = matches.Count,
                Signals = matches,
                ToolAvailable = toolAvailable,
                ManifestHint = manifestHint,
                Verified = verified,
                ValidationState = validationState,
            };
        }
        catch
        {
            return new SongIdProvenanceFinding();
        }
    }

    private async Task<PanakoStoreResult> StorePanakoSourceAsync(SongIdRun run, string? jarPath, string audioPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jarPath))
        {
            return new PanakoStoreResult();
        }

        var reportsDir = Path.Combine(run.ArtifactDirectory, "reports");
        Directory.CreateDirectory(reportsDir);
        var arguments = BuildPanakoArguments(jarPath, reportsDir).Concat(new[] { "store", audioPath }).ToArray();
        var result = await RunToolAsync("java", arguments, cancellationToken).ConfigureAwait(false);
        run.Evidence.Add("Panako stored the source audio into a run-local fingerprint database.");
        return new PanakoStoreResult
        {
            JarPath = jarPath,
            Output = result.StandardOutput,
        };
    }

    private async Task<SongIdRecognizerFinding?> QueryPanakoClipAsync(SongIdRun run, string jarPath, string clipPath, CancellationToken cancellationToken)
    {
        var reportsDir = Path.Combine(run.ArtifactDirectory, "reports");
        var arguments = BuildPanakoArguments(jarPath, reportsDir).Concat(new[] { "query", clipPath }).ToArray();
        var result = await RunToolAsync("java", arguments, cancellationToken).ConfigureAwait(false);
        return ParsePanakoFinding(result.StandardOutput);
    }

    private string? LocatePanakoJar()
    {
        foreach (var root in GetSiblingSearchRoots())
        {
            var libsDir = Path.Combine(root, "external", "Panako", "build", "libs");
            if (!Directory.Exists(libsDir))
            {
                continue;
            }

            var jar = Directory.EnumerateFiles(libsDir, "*.jar").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).LastOrDefault();
            if (!string.IsNullOrWhiteSpace(jar))
            {
                return jar;
            }
        }

        return null;
    }

    private async Task<string?> EnsureAudfprintDatabaseAsync(SongIdRun run, string? scriptPath, string audioPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return null;
        }

        var dbPath = Path.Combine(run.ArtifactDirectory, "reports", "audfprint-db.pklz");
        if (File.Exists(dbPath))
        {
            return dbPath;
        }

        var command = $"python3 {ShellEscape(scriptPath)} new -d {ShellEscape(dbPath)} {ShellEscape(audioPath)}";
        await RunToolAsync("bash", new[] { "-lc", command }, cancellationToken).ConfigureAwait(false);
        run.Evidence.Add("Audfprint created a run-local source fingerprint database.");
        return dbPath;
    }

    private async Task<SongIdRecognizerFinding?> QueryAudfprintAsync(string scriptPath, string databasePath, string clipPath, CancellationToken cancellationToken)
    {
        var command = $"python3 {ShellEscape(scriptPath)} match -d {ShellEscape(databasePath)} {ShellEscape(clipPath)}";
        var result = await RunToolAsync("bash", new[] { "-lc", command }, cancellationToken).ConfigureAwait(false);
        return ParseAudfprintFinding(result.StandardOutput);
    }

    private string? LocateAudfprintScript()
    {
        foreach (var root in GetSiblingSearchRoots())
        {
            var script = Path.Combine(root, "external", "audfprint", "audfprint.py");
            if (File.Exists(script))
            {
                return script;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetSiblingSearchRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory, Program.AppDirectory })
        {
            if (string.IsNullOrWhiteSpace(start) || !Directory.Exists(start))
            {
                continue;
            }

            var current = new DirectoryInfo(start);
            while (current != null)
            {
                var direct = current.FullName;
                roots.Add(direct);
                var sibling = Path.Combine(current.FullName, "ytdlpchop");
                if (Directory.Exists(sibling))
                {
                    roots.Add(sibling);
                }

                current = current.Parent;
            }
        }

        return roots;
    }

    private static IEnumerable<string> BuildPanakoArguments(string jarPath, string reportsDir)
    {
        var panakoDir = Path.Combine(reportsDir, "panako");
        Directory.CreateDirectory(panakoDir);
        return new[]
        {
            "--add-opens",
            "java.base/java.nio=ALL-UNNAMED",
            $"-Djava.util.prefs.userRoot={Path.Combine(panakoDir, "prefs")}",
            "-jar",
            jarPath,
            $"STRATEGY={DefaultPanakoStrategy}",
            $"PANAKO_LMDB_FOLDER={Path.Combine(panakoDir, "panako_db")}",
            $"PANAKO_CACHE_FOLDER={Path.Combine(panakoDir, "panako_cache")}",
            $"OLAF_LMDB_FOLDER={Path.Combine(panakoDir, "olaf_db")}",
            $"OLAF_CACHE_FOLDER={Path.Combine(panakoDir, "olaf_cache")}",
        };
    }

    private async Task<SongIdAiHeuristicFinding?> ScoreAiAudioArtifactsAsync(string audioPath, CancellationToken cancellationToken)
    {
        var samples = await ExtractMonoSamplesAsync(audioPath, 16000, cancellationToken).ConfigureAwait(false);
        if (samples.Length < 16000 * 8)
        {
            return null;
        }

        var usableLength = Math.Min(samples.Length, 16000 * 60);
        var usable = new float[usableLength];
        Array.Copy(samples, usable, usableLength);

        const int frameSize = 4096;
        const int hop = 1024;
        if (usable.Length < frameSize)
        {
            return null;
        }

        var window = Enumerable.Range(0, frameSize)
            .Select(index => 0.5f - (0.5f * (float)Math.Cos((2.0 * Math.PI * index) / (frameSize - 1))))
            .ToArray();
        var summedSpectrum = new double[(frameSize / 2) + 1];
        var frameCount = 0;

        for (var start = 0; start <= usable.Length - frameSize; start += hop)
        {
            var frame = new Complex[frameSize];
            for (var index = 0; index < frameSize; index++)
            {
                frame[index] = new Complex(usable[start + index] * window[index], 0);
            }

            FastFourierTransform(frame);
            for (var index = 0; index < summedSpectrum.Length; index++)
            {
                summedSpectrum[index] += frame[index].Magnitude;
            }

            frameCount++;
        }

        if (frameCount == 0)
        {
            return null;
        }

        var averageSpectrum = summedSpectrum.Select(value => value / frameCount).ToArray();
        var frequencies = Enumerable.Range(0, averageSpectrum.Length)
            .Select(index => index * (16000.0 / frameSize))
            .ToArray();
        var band = averageSpectrum
            .Zip(frequencies, (magnitude, frequency) => new { magnitude, frequency })
            .Where(item => item.frequency >= 80.0 && item.frequency <= 7800.0)
            .ToArray();
        if (band.Length < 32)
        {
            return null;
        }

        var bandSpectrum = band.Select(item => item.magnitude).ToArray();
        var bandFrequencies = band.Select(item => item.frequency).ToArray();
        var baseline = MovingAverage(bandSpectrum, 31);
        var residual = bandSpectrum.Select((value, index) => Math.Max(0.0, value - baseline[index])).ToArray();
        var residualMean = residual.Average();
        var residualStd = StandardDeviation(residual, residualMean);
        var threshold = residualMean + (2.5 * residualStd);
        var peakIndices = new List<int>();
        for (var index = 1; index < residual.Length - 1; index++)
        {
            if (residual[index] > threshold && residual[index] > residual[index - 1] && residual[index] > residual[index + 1])
            {
                peakIndices.Add(index);
            }
        }

        var peakFrequencies = peakIndices.Select(index => bandFrequencies[index]).ToArray();
        var spacings = new List<double>();
        for (var index = 1; index < peakFrequencies.Length; index++)
        {
            spacings.Add(peakFrequencies[index] - peakFrequencies[index - 1]);
        }

        var roundedSpacingCounts = new Dictionary<double, int>();
        foreach (var spacing in spacings)
        {
            var rounded = Math.Round(spacing / 5.0) * 5.0;
            roundedSpacingCounts[rounded] = roundedSpacingCounts.TryGetValue(rounded, out var count) ? count + 1 : 1;
        }

        var dominantSpacing = roundedSpacingCounts.OrderByDescending(item => item.Value).Select(item => item.Key).FirstOrDefault();
        var periodicityStrength = spacings.Count > 0 && roundedSpacingCounts.Count > 0
            ? roundedSpacingCounts.Max(item => item.Value) / (double)spacings.Count
            : 0.0;
        var peakDensity = peakIndices.Count / (double)bandSpectrum.Length;
        var baselineMean = baseline.Average();
        var residualRatio = residualMean / (baselineMean + 1e-9);
        var weightedMagnitude = bandSpectrum.Sum();
        var spectralCentroid = weightedMagnitude > 0
            ? bandSpectrum.Select((value, index) => value * bandFrequencies[index]).Sum() / weightedMagnitude
            : 0;
        var totalFlux = 0.0;
        var fluxFrames = 0;
        for (var start = hop; start <= usable.Length - frameSize; start += hop)
        {
            var delta = 0.0;
            for (var index = 0; index < frameSize; index++)
            {
                delta += Math.Abs(usable[start + index] - usable[start + index - hop]);
            }

            totalFlux += delta / frameSize;
            fluxFrames++;
        }

        var spectralFlux = fluxFrames > 0 ? totalFlux / fluxFrames : 0;
        var pitchBins = bandSpectrum.Where((_, index) => bandFrequencies[index] >= 80 && bandFrequencies[index] <= 1200).ToArray();
        var pitchMean = pitchBins.Length > 0 ? pitchBins.Average() : 0;
        var pitchPeak = pitchBins.Length > 0 ? pitchBins.Max() : 0;
        var pitchSalience = pitchMean > 0 ? Math.Min(1, pitchPeak / pitchMean / 6.0) : 0;
        var analysisSeconds = usable.Length / 16000.0;
        var durationSuspicion = analysisSeconds < 18
            ? 0.62
            : analysisSeconds < 45
                ? 0.34
                : 0.08;
        var score = Math.Min(
            1.0,
            periodicityStrength * 0.55 + Math.Min(0.25, peakDensity * 18.0) + Math.Min(0.20, residualRatio * 0.8));

        return new SongIdAiHeuristicFinding
        {
            ArtifactScore = Math.Round(score, 4),
            ArtifactLabel = score >= 0.65 ? "high" : score >= 0.4 ? "medium" : "low",
            PeakCount = peakIndices.Count,
            PeakDensity = Math.Round(peakDensity, 6),
            PeriodicityStrength = Math.Round(periodicityStrength, 4),
            DominantSpacingHz = Math.Round(dominantSpacing, 2),
            ResidualRatio = Math.Round(residualRatio, 4),
            SpectralCentroid = Math.Round(spectralCentroid, 2),
            SpectralFlux = Math.Round(spectralFlux, 6),
            PitchSalience = Math.Round(pitchSalience, 4),
            DurationSuspicion = Math.Round(durationSuspicion, 4),
            SampleRate = 16000,
            AnalysisSeconds = Math.Round(analysisSeconds, 2),
        };
    }

    private static SongIdAiHeuristicFinding? BuildAggregateAiHeuristics(IEnumerable<SongIdClipFinding> clips)
    {
        var findings = clips.Select(clip => clip.AiHeuristics).Where(finding => finding != null).Cast<SongIdAiHeuristicFinding>().ToList();
        if (findings.Count == 0)
        {
            return null;
        }

        return new SongIdAiHeuristicFinding
        {
            ArtifactScore = Math.Round(findings.Average(finding => finding.ArtifactScore), 4),
            ArtifactLabel = findings.Any(finding => string.Equals(finding.ArtifactLabel, "high", StringComparison.OrdinalIgnoreCase))
                ? "high"
                : findings.Any(finding => string.Equals(finding.ArtifactLabel, "medium", StringComparison.OrdinalIgnoreCase))
                    ? "medium"
                    : "low",
            PeakCount = findings.Sum(finding => finding.PeakCount),
            PeakDensity = Math.Round(findings.Average(finding => finding.PeakDensity), 6),
            PeriodicityStrength = Math.Round(findings.Average(finding => finding.PeriodicityStrength), 4),
            DominantSpacingHz = Math.Round(findings.Average(finding => finding.DominantSpacingHz), 2),
            ResidualRatio = Math.Round(findings.Average(finding => finding.ResidualRatio), 4),
            SpectralCentroid = Math.Round(findings.Average(finding => finding.SpectralCentroid), 2),
            SpectralFlux = Math.Round(findings.Average(finding => finding.SpectralFlux), 6),
            PitchSalience = Math.Round(findings.Average(finding => finding.PitchSalience), 4),
            DurationSuspicion = Math.Round(findings.Average(finding => finding.DurationSuspicion), 4),
            SampleRate = findings[0].SampleRate,
            AnalysisSeconds = Math.Round(findings.Sum(finding => finding.AnalysisSeconds), 2),
        };
    }

    private async Task<List<SongIdPerturbationFinding>> AnalyzePerturbationsAsync(
        SongIdRun run,
        string audioPath,
        int durationSeconds,
        IReadOnlyCollection<int> commentTimestamps,
        CancellationToken cancellationToken)
    {
        var (excerptPath, _) = await PrepareAnalysisExcerptAsync(
            run,
            audioPath,
            "perturbation_excerpt",
            durationSeconds,
            commentTimestamps,
            PerturbationExcerptSeconds,
            cancellationToken).ConfigureAwait(false);

        var baseline = await ScoreAiAudioArtifactsAsync(excerptPath, cancellationToken).ConfigureAwait(false);
        if (baseline == null)
        {
            return new List<SongIdPerturbationFinding>();
        }

        var perturbationsDir = Path.Combine(run.ArtifactDirectory, "reports", "perturbations");
        Directory.CreateDirectory(perturbationsDir);

        var probes = new List<(string Id, string Label, string[] Args)>
        {
            ("lowpass", "Low-pass 8 kHz", new[] { "-hide_banner", "-loglevel", "error", "-y", "-i", excerptPath, "-af", "lowpass=f=8000", Path.Combine(perturbationsDir, "lowpass.flac") }),
            ("resample", "Resample 12 kHz", new[] { "-hide_banner", "-loglevel", "error", "-y", "-i", excerptPath, "-ar", "12000", Path.Combine(perturbationsDir, "resample.flac") }),
            ("pitch_shift", "Pitch shift +0.35", new[] { "-hide_banner", "-loglevel", "error", "-y", "-i", excerptPath, "-af", "asetrate=44100*1.02,aresample=44100,atempo=0.98", Path.Combine(perturbationsDir, "pitch_shift.flac") }),
        };

        var findings = new List<SongIdPerturbationFinding>();
        foreach (var probe in probes)
        {
            try
            {
                var outputPath = probe.Args[^1];
                await RunToolAsync("ffmpeg", probe.Args, cancellationToken).ConfigureAwait(false);
                var heuristics = await ScoreAiAudioArtifactsAsync(outputPath, cancellationToken).ConfigureAwait(false);
                if (heuristics == null)
                {
                    continue;
                }

                findings.Add(new SongIdPerturbationFinding
                {
                    PerturbationId = probe.Id,
                    Label = probe.Label,
                    Path = outputPath,
                    BaselineDelta = Math.Round(Math.Abs(baseline.ArtifactScore - heuristics.ArtifactScore), 4),
                    Heuristics = heuristics,
                });
            }
            catch (Exception ex)
            {
                run.Evidence.Add($"Perturbation probe {probe.Id} skipped: {ex.Message}");
            }
        }

        if (findings.Count > 0)
        {
            run.Evidence.Add($"Perturbation stability tested across {findings.Count} probe(s).");
        }

        return findings;
    }

    private async Task<float[]> ExtractMonoSamplesAsync(string audioPath, int sampleRate, CancellationToken cancellationToken)
    {
        var result = await RunToolAsync(
            "ffmpeg",
            new[]
            {
                "-hide_banner",
                "-loglevel",
                "error",
                "-i",
                audioPath,
                "-vn",
                "-ac",
                "1",
                "-ar",
                sampleRate.ToString(CultureInfo.InvariantCulture),
                "-f",
                "s16le",
                "-",
            },
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(result.StandardOutput) && string.IsNullOrEmpty(result.StandardOutputBytesBase64))
        {
            return Array.Empty<float>();
        }

        var bytes = Convert.FromBase64String(result.StandardOutputBytesBase64 ?? string.Empty);
        var samples = new float[bytes.Length / 2];
        for (var index = 0; index < samples.Length; index++)
        {
            var value = BitConverter.ToInt16(bytes, index * 2);
            samples[index] = value / 32768f;
        }

        return samples;
    }

    private async Task<int> GetDurationSecondsAsync(string path, CancellationToken cancellationToken)
    {
        var result = await RunToolAsync(
            "ffprobe",
            new[]
            {
                "-v",
                "error",
                "-show_entries",
                "format=duration",
                "-of",
                "default=nk=1:nw=1",
                path,
            },
            cancellationToken).ConfigureAwait(false);

        return double.TryParse(result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? (int)value
            : 0;
    }

    private async Task<CommandResult> RunToolAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        if (process.StartInfo.RedirectStandardOutput &&
            arguments.Contains("-") &&
            string.Equals(fileName, "ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            using var stdoutStream = new MemoryStream();
            using var stderrStream = new MemoryStream();
            var copyStdout = process.StandardOutput.BaseStream.CopyToAsync(stdoutStream, cancellationToken);
            var copyStderr = process.StandardError.BaseStream.CopyToAsync(stderrStream, cancellationToken);
            await Task.WhenAll(copyStdout, copyStderr, process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);

            var stdoutBytes = stdoutStream.ToArray();
            var stderr = System.Text.Encoding.UTF8.GetString(stderrStream.ToArray()).Trim();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {stderr}");
            }

            return new CommandResult(string.Empty, stderr, Convert.ToBase64String(stdoutBytes));
        }

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var stderrText = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {stderrText}");
        }

        return new CommandResult(stdout.Trim(), stderrText.Trim(), null);
    }

    private async Task<bool> CommandExistsAsync(string fileName, CancellationToken cancellationToken)
    {
        if (_commandExistsOverride != null)
        {
            return await _commandExistsOverride(fileName, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await RunToolAsync("bash", new[] { "-lc", $"command -v {ShellEscape(fileName)} >/dev/null 2>&1" }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string GetWorkspaceDirectory(Guid runId)
    {
        var root = Path.Combine(Program.AppDirectory, "songid", "runs");
        Directory.CreateDirectory(root);
        return Path.Combine(root, runId.ToString("D"));
    }

    private static string ResolveDownloadedFile(string directory, string prefix)
    {
        return Directory.EnumerateFiles(directory, $"{prefix}*")
            .First(file => !file.EndsWith(".part", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeVideo(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi";
    }

    private static int? ParseTimestamp(string text)
    {
        var match = TimestampRegex.Match(text ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        var hours = int.TryParse(match.Groups["hours"].Value, out var parsedHours) ? parsedHours : 0;
        var minutes = int.TryParse(match.Groups["minutes"].Value, out var parsedMinutes) ? parsedMinutes : 0;
        var seconds = int.TryParse(match.Groups["seconds"].Value, out var parsedSeconds) ? parsedSeconds : 0;
        return (hours * 3600) + (minutes * 60) + seconds;
    }

    private static bool LooksInterestingComment(string text)
    {
        var lowered = text.ToLowerInvariant();
        return lowered.Contains("track") ||
            lowered.Contains("song") ||
            lowered.Contains("what is") ||
            lowered.Contains("name of") ||
            lowered.Contains("playlist") ||
            lowered.Contains("ai") ||
            lowered.Contains("cd");
    }

    private static bool LooksLikePlaylistRequest(string text)
    {
        var lowered = text.ToLowerInvariant();
        return lowered.Contains("playlist") ||
            lowered.Contains("tracklist") ||
            lowered.Contains("timestamps") ||
            lowered.Contains("time stamps") ||
            lowered.Contains("song list") ||
            lowered.Contains("id this mix");
    }

    private static bool LooksSyntheticComment(string text)
    {
        var lowered = text.ToLowerInvariant();
        return lowered.Contains("suno") ||
            lowered.Contains("udio") ||
            lowered.Contains("ai generated") ||
            lowered.Contains("generated with ai") ||
            lowered.Contains("made with ai") ||
            lowered.Contains("not a real band") ||
            lowered.Contains("fake artist");
    }

    private static List<string> ExtractTranscriptQueries(string text)
    {
        return text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => string.Join(" ", line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim())
            .Where(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static string? ExtractSpotifyTrackId(string source)
    {
        var match = Regex.Match(source, @"track/(?<id>[A-Za-z0-9]+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private static string? TryGetMetadataValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) ? value : null;
    }

    private static int ParseInt(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static string DetectSourceType(string source)
    {
        if (File.Exists(source))
        {
            return "local_file";
        }

        if (YouTubeRegex.IsMatch(source))
        {
            return "youtube_url";
        }

        if (SpotifyTrackRegex.IsMatch(source))
        {
            return "spotify_url";
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out _))
        {
            return "url";
        }

        return "text_query";
    }

    private static string BuildBestQuery(params string?[] parts)
    {
        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim())).Trim();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static Dictionary<string, string> ExtractOgMeta(string html)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in OgMetaRegex.Matches(html))
        {
            var key = match.Groups["key"].Value.Trim();
            var value = System.Net.WebUtility.HtmlDecode(match.Groups["value"].Value.Trim());
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                result[key.ToLowerInvariant()] = value;
            }
        }

        return result;
    }

    private static List<string> BuildEmbeddedMetadataKeys(SongIdMetadata metadata)
    {
        var keys = new List<string>();
        if (!string.IsNullOrWhiteSpace(metadata.Title))
        {
            keys.Add("title");
        }

        if (!string.IsNullOrWhiteSpace(metadata.Artist))
        {
            keys.Add("artist");
        }

        if (!string.IsNullOrWhiteSpace(metadata.Album))
        {
            keys.Add("album");
        }

        if (metadata.Extra.TryGetValue("chapter_count", out var chapterCount) && int.TryParse(chapterCount, out var parsedCount) && parsedCount > 0)
        {
            keys.Add("chapters");
        }

        return keys;
    }

    private static List<SongIdChapterFinding> ParseChapterFindings(JsonElement root)
    {
        if (!root.TryGetProperty("chapters", out var chaptersElement) || chaptersElement.ValueKind != JsonValueKind.Array)
        {
            return new List<SongIdChapterFinding>();
        }

        var chapters = new List<SongIdChapterFinding>();
        foreach (var chapter in chaptersElement.EnumerateArray())
        {
            var startMilliseconds = TryGetInt(chapter, "start_time_ms");
            var start = TryGetInt(chapter, "start_time") ?? (startMilliseconds.HasValue ? startMilliseconds.Value / 1000 : null);
            if (!start.HasValue)
            {
                continue;
            }

            var endMilliseconds = TryGetInt(chapter, "end_time_ms");
            var end = TryGetInt(chapter, "end_time") ?? (endMilliseconds.HasValue ? endMilliseconds.Value / 1000 : null);
            chapters.Add(new SongIdChapterFinding
            {
                ChapterId = TryGetString(chapter, "title") ?? $"chapter-{start.Value}",
                Title = TryGetString(chapter, "title") ?? string.Empty,
                StartSeconds = start.Value,
                EndSeconds = end,
            });
        }

        return chapters;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.Number when property.TryGetDouble(out var doubleValue) => (int)Math.Round(doubleValue),
            _ => null,
        };
    }

    private static string CleanSegmentTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = Regex.Replace(value, @"\[[^\]]+\]|\([^\)]*\)", " ");
        cleaned = Regex.Replace(cleaned, @"\b(chapter|track|part|timestamp)\b\s*\d*", " ", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9'&\-\s]+", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var lowered = cleaned.ToLowerInvariant();
        if (lowered is "intro" or "outro" or "interlude" or "credits" or "mix" or "playlist")
        {
            return string.Empty;
        }

        return cleaned;
    }

    private static string RemoveTimestampText(string value)
    {
        return TimestampRegex.Replace(value ?? string.Empty, " ").Trim();
    }

    private static string NormalizeSegmentQuery(string value)
    {
        return Regex.Replace((value ?? string.Empty).ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }

    private static string FormatTimestamp(int seconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.Hours > 0
            ? span.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : span.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    private static void CollectMetadataStrings(SongIdMetadata metadata, List<string> output)
    {
        output.Add(metadata.Title.ToLowerInvariant());
        output.Add(metadata.Artist.ToLowerInvariant());
        output.Add(metadata.Album.ToLowerInvariant());
        foreach (var pair in metadata.Extra)
        {
            output.Add(pair.Key.ToLowerInvariant());
            output.Add((pair.Value ?? string.Empty).ToLowerInvariant());
        }
    }

    private static void CollectJsonStrings(JsonElement element, List<string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                output.Add((element.GetString() ?? string.Empty).ToLowerInvariant());
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectJsonStrings(item, output);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    output.Add(property.Name.ToLowerInvariant());
                    CollectJsonStrings(property.Value, output);
                }

                break;
        }
    }

    private static double ParseFpcalcDuration(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith("DURATION=", StringComparison.OrdinalIgnoreCase));
        return line != null &&
            double.TryParse(line.Split('=', 2)[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static string ParseFpcalcFingerprint(string text)
    {
        var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith("FINGERPRINT=", StringComparison.OrdinalIgnoreCase));
        return line != null ? line.Split('=', 2)[1] : string.Empty;
    }

    private static string ShellEscape(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'") + "'";
    }

    private static string GetCorpusDirectory()
    {
        return Path.Combine(Program.AppDirectory, "songid", "corpus");
    }

    private static double CompareFingerprints(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return 0;
        }

        var leftTokens = ParseFingerprintTokens(left);
        var rightTokens = ParseFingerprintTokens(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
        {
            return 0;
        }

        var intersection = leftTokens.Intersect(rightTokens).Count();
        var union = leftTokens.Union(rightTokens).Count();
        return union == 0 ? 0 : intersection / (double)union;
    }

    private static HashSet<int> ParseFingerprintTokens(string fingerprint)
    {
        return fingerprint
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => int.TryParse(token, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : int.MinValue)
            .Where(value => value != int.MinValue)
            .ToHashSet();
    }

    private static double[] MovingAverage(double[] values, int windowSize)
    {
        var halfWindow = windowSize / 2;
        var result = new double[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var start = Math.Max(0, index - halfWindow);
            var end = Math.Min(values.Length - 1, index + halfWindow);
            var sum = 0.0;
            for (var inner = start; inner <= end; inner++)
            {
                sum += values[inner];
            }

            result[index] = sum / ((end - start) + 1);
        }

        return result;
    }

    private static double StandardDeviation(double[] values, double mean)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        var variance = values.Sum(value => Math.Pow(value - mean, 2)) / values.Length;
        return Math.Sqrt(variance);
    }

    private static void FastFourierTransform(Complex[] buffer)
    {
        var n = buffer.Length;
        var bits = (int)Math.Log2(n);
        for (var index = 0; index < n; index++)
        {
            var reversed = ReverseBits(index, bits);
            if (reversed > index)
            {
                (buffer[index], buffer[reversed]) = (buffer[reversed], buffer[index]);
            }
        }

        for (var size = 2; size <= n; size <<= 1)
        {
            var angle = -2.0 * Math.PI / size;
            var wPhase = Complex.FromPolarCoordinates(1, angle);
            for (var start = 0; start < n; start += size)
            {
                var w = Complex.One;
                for (var index = 0; index < size / 2; index++)
                {
                    var even = buffer[start + index];
                    var odd = w * buffer[start + index + (size / 2)];
                    buffer[start + index] = even + odd;
                    buffer[start + index + (size / 2)] = even - odd;
                    w *= wPhase;
                }
            }
        }
    }

    private static int ReverseBits(int value, int bitCount)
    {
        var reversed = 0;
        for (var index = 0; index < bitCount; index++)
        {
            reversed = (reversed << 1) | (value & 1);
            value >>= 1;
        }

        return reversed;
    }

    private sealed class SongIdAnalysis
    {
        public string SourceType { get; set; } = string.Empty;

        public string Query { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public List<string> Evidence { get; set; } = new();

        public SongIdMetadata Metadata { get; set; } = new();

        public TrackTarget? ExactTrack { get; set; }

        public AlbumTarget? ExactAlbum { get; set; }

        public List<SongIdChapterFinding> Chapters { get; set; } = new();
    }

    private sealed class PreparedAnalysisAssets
    {
        public string WorkspacePath { get; set; } = string.Empty;

        public string? AudioPath { get; set; }

        public string? VideoPath { get; set; }

        public string? CommentsPath { get; set; }

        public int DurationSeconds { get; set; }

        public string AnalysisAudioSource { get; set; } = string.Empty;
    }

    private sealed class PanakoStoreResult
    {
        public string? JarPath { get; set; }

        public string Output { get; set; } = string.Empty;
    }

    private sealed class SongIdCorpusEntry
    {
        public string? RunId { get; set; }

        public string? Source { get; set; }

        public string? Label { get; set; }

        public string? FingerprintPath { get; set; }

        public string? RecordingId { get; set; }

        public string? Artist { get; set; }

        public string? Title { get; set; }

        public string? FamilyLabel { get; set; }

        public int KnownFamilyScore { get; set; }
    }

    private sealed class SongIdSegmentQuery
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string Query { get; set; } = string.Empty;

        public string SourceLabel { get; set; } = string.Empty;

        public int StartSeconds { get; set; }

        public double Confidence { get; set; }
    }

    private sealed record CommandResult(string StandardOutput, string StandardError, string? StandardOutputBytesBase64);
}
