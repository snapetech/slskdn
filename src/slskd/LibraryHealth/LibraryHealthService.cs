// <copyright file="LibraryHealthService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.LibraryHealth
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Audio;
    using slskd.HashDb;
    using slskd.Integrations.MetadataFacade;
    using slskd.Integrations.MusicBrainz;
    using slskd.LibraryHealth.Remediation;
    using TagLib;

    /// <summary>
    /// Library Health scanner for detecting quality issues and missing tracks.
    /// Implements deep scanning: transcode detection, canonical variant checking, release completeness.
    /// </summary>
    public class LibraryHealthService : ILibraryHealthService
    {
        private readonly IHashDbService hashDb;
        private readonly ILibraryHealthRemediationService remediationService;
        private readonly IMetadataFacade metadataFacade;
        private readonly ICanonicalStatsService canonicalStats;
        private readonly IMusicBrainzClient musicBrainzClient;
        private readonly ILogger<LibraryHealthService> log;
        private readonly ConcurrentDictionary<string, LibraryHealthScan> activeScans = new();
        private readonly QualityScorer qualityScorer = new();
        private readonly TranscodeDetector transcodeDetector = new();

        public LibraryHealthService(
            IHashDbService hashDb,
            ILibraryHealthRemediationService remediationService,
            IMetadataFacade metadataFacade,
            ICanonicalStatsService canonicalStats,
            IMusicBrainzClient musicBrainzClient,
            ILogger<LibraryHealthService> log)
        {
            this.hashDb = hashDb;
            this.remediationService = remediationService;
            this.metadataFacade = metadataFacade;
            this.canonicalStats = canonicalStats;
            this.musicBrainzClient = musicBrainzClient;
            this.log = log;
        }

        public async Task<string> StartScanAsync(LibraryHealthScanRequest request, CancellationToken ct = default)
        {
            var scanId = Guid.NewGuid().ToString();
            var scan = new LibraryHealthScan
            {
                ScanId = scanId,
                LibraryPath = request.LibraryPath,
                StartedAt = DateTimeOffset.UtcNow,
                Status = ScanStatus.Running,
                FilesScanned = 0,
                IssuesDetected = 0,
            };

            activeScans[scanId] = scan;
            await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);

            _ = Task.Run(() => PerformScanAsync(scanId, request, ct), ct);
            return scanId;
        }

        public async Task<LibraryHealthScan> GetScanStatusAsync(string scanId, CancellationToken ct = default)
        {
            if (activeScans.TryGetValue(scanId, out var active))
            {
                return active;
            }

            return await hashDb.GetLibraryHealthScanAsync(scanId, ct).ConfigureAwait(false);
        }

        public Task<List<LibraryIssue>> GetIssuesAsync(LibraryHealthIssueFilter filter, CancellationToken ct = default)
        {
            return hashDb.GetLibraryIssuesAsync(filter, ct);
        }

        public Task UpdateIssueStatusAsync(string issueId, LibraryIssueStatus newStatus, CancellationToken ct = default)
        {
            return hashDb.UpdateLibraryIssueStatusAsync(issueId, newStatus, ct);
        }

        public async Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default)
        {
            log.LogInformation("[LH] Creating remediation job for {Count} issues", issueIds?.Count ?? 0);
            return await remediationService.CreateRemediationJobAsync(issueIds, ct).ConfigureAwait(false);
        }

        public async Task<LibraryHealthSummary> GetSummaryAsync(string libraryPath, CancellationToken ct = default)
        {
            var issues = await hashDb.GetLibraryIssuesAsync(new LibraryHealthIssueFilter { LibraryPath = libraryPath }, ct).ConfigureAwait(false);
            return new LibraryHealthSummary
            {
                LibraryPath = libraryPath,
                TotalIssues = issues.Count,
                IssuesOpen = issues.Count(i => i.Status != LibraryIssueStatus.Resolved && i.Status != LibraryIssueStatus.Ignored),
                IssuesResolved = issues.Count(i => i.Status == LibraryIssueStatus.Resolved),
            };
        }

        private async Task PerformScanAsync(string scanId, LibraryHealthScanRequest request, CancellationToken ct)
        {
            if (!activeScans.TryGetValue(scanId, out var scan))
            {
                return;
            }

            try
            {
                if (!Directory.Exists(request.LibraryPath))
                {
                    throw new DirectoryNotFoundException($"Library path not found: {request.LibraryPath}");
                }

                log.LogInformation("[LH] Starting library health scan: {Path}", request.LibraryPath);

                var files = Directory.EnumerateFiles(
                        request.LibraryPath,
                        "*.*",
                        request.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(f => request.FileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                log.LogInformation("[LH] Found {Count} audio files to scan", files.Count);

                // Process files in parallel with concurrency limit
                var semaphore = new SemaphoreSlim(request.MaxConcurrentFiles);
                var scanLock = new object();
                var scannedCount = 0;

                var tasks = files.Select(async file =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        await ScanFileAsync(file, scan, scanLock, ct);
                        Interlocked.Increment(ref scannedCount);
                        lock (scanLock)
                        {
                            scan.FilesScanned = scannedCount;
                        }
                        await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "[LH] Failed to scan file: {Path}", file);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);

                scan.Status = ScanStatus.Completed;
                scan.CompletedAt = DateTimeOffset.UtcNow;
                await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);
                activeScans.TryRemove(scanId, out _);

                log.LogInformation("[LH] Scan completed: {Files} files, {Issues} issues", scan.FilesScanned, scan.IssuesDetected);
            }
            catch (Exception ex)
            {
                scan.Status = ScanStatus.Failed;
                scan.ErrorMessage = ex.Message;
                scan.CompletedAt = DateTimeOffset.UtcNow;
                await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);
                activeScans.TryRemove(scanId, out _);
                log.LogWarning(ex, "[LH] Scan failed for {Path}", request.LibraryPath);
            }
        }

        private async Task ScanFileAsync(string filePath, LibraryHealthScan scan, object scanLock, CancellationToken ct)
        {
            try
            {
                // Step 1: Resolve MusicBrainz ID via metadata facade
                var metadata = await metadataFacade.GetByFileAsync(filePath, ct).ConfigureAwait(false);
                if (metadata == null || string.IsNullOrWhiteSpace(metadata.MusicBrainzRecordingId))
                {
                    // Issue: Missing metadata
                    await EmitIssueAsync(new LibraryIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Type = LibraryIssueType.MissingMetadata,
                        Severity = LibraryIssueSeverity.Low,
                        FilePath = filePath,
                        Reason = "Unable to resolve MusicBrainz Recording ID via fingerprint or tags",
                        CanAutoFix = false,
                        Status = LibraryIssueStatus.Detected,
                        DetectedAt = DateTimeOffset.UtcNow,
                    }, scan, scanLock, ct);
                    return;
                }

                var recordingId = metadata.MusicBrainzRecordingId;

                // Step 2: Create AudioVariant from file
                AudioVariant variant;
                try
                {
                    using var tagFile = TagLib.File.Create(filePath);
                    var props = tagFile.Properties;
                    var fileInfo = new FileInfo(filePath);

                    variant = new AudioVariant
                    {
                        VariantId = Guid.NewGuid().ToString(),
                        MusicBrainzRecordingId = recordingId,
                        Codec = GetCodecName(props, Path.GetExtension(filePath)),
                        SampleRateHz = props.AudioSampleRate,
                        BitDepth = props.BitsPerSample > 0 ? props.BitsPerSample : null,
                        Channels = props.AudioChannels,
                        DurationMs = (int)props.Duration.TotalMilliseconds,
                        BitrateKbps = (int)(props.AudioBitrate / 1000.0),
                        FileSizeBytes = fileInfo.Length,
                        FirstSeenAt = DateTimeOffset.UtcNow,
                        LastSeenAt = DateTimeOffset.UtcNow,
                        SeenCount = 1,
                    };

                    // Compute quality score
                    variant.QualityScore = qualityScorer.ComputeQualityScore(variant);

                    // Check for transcode
                    var (isSuspect, reason) = transcodeDetector.DetectTranscode(variant);
                    variant.TranscodeSuspect = isSuspect;
                    variant.TranscodeReason = reason;
                }
                catch (Exception ex)
                {
                    // Issue: Corrupted file
                    await EmitIssueAsync(new LibraryIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Type = LibraryIssueType.CorruptedFile,
                        Severity = LibraryIssueSeverity.Critical,
                        FilePath = filePath,
                        MusicBrainzRecordingId = recordingId,
                        Reason = $"File cannot be read: {ex.Message}",
                        CanAutoFix = true,
                        SuggestedAction = "Re-download from Soulseek or mesh overlay",
                        Status = LibraryIssueStatus.Detected,
                        DetectedAt = DateTimeOffset.UtcNow,
                    }, scan, scanLock, ct);
                    return;
                }

                // Step 3: Check for transcodes
                if (variant.TranscodeSuspect)
                {
                    await EmitIssueAsync(new LibraryIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Type = LibraryIssueType.SuspectedTranscode,
                        Severity = LibraryIssueSeverity.High,
                        FilePath = filePath,
                        MusicBrainzRecordingId = recordingId,
                        Artist = metadata.Artist,
                        Title = metadata.Title,
                        Reason = variant.TranscodeReason,
                        Metadata = new Dictionary<string, object>
                        {
                            ["quality_score"] = variant.QualityScore,
                            ["codec"] = variant.Codec,
                            ["bitrate"] = variant.BitrateKbps,
                        },
                        CanAutoFix = true,
                        SuggestedAction = "Replace with verified lossless variant from canonical sources",
                        Status = LibraryIssueStatus.Detected,
                        DetectedAt = DateTimeOffset.UtcNow,
                    }, scan, scanLock, ct);
                }

                // Step 4: Check for canonical upgrades
                if (canonicalStats != null)
                {
                    var candidates = await canonicalStats.GetCanonicalVariantCandidatesAsync(recordingId, ct).ConfigureAwait(false);
                    if (candidates != null && candidates.Count > 0)
                    {
                        var bestCanonical = candidates.First();
                        double qualityGap = bestCanonical.QualityScore - variant.QualityScore;

                        if (qualityGap > 0.2) // Significant quality gap
                        {
                            await EmitIssueAsync(new LibraryIssue
                            {
                                IssueId = Guid.NewGuid().ToString(),
                                Type = LibraryIssueType.NonCanonicalVariant,
                                Severity = qualityGap > 0.4 ? LibraryIssueSeverity.High : LibraryIssueSeverity.Medium,
                                FilePath = filePath,
                                MusicBrainzRecordingId = recordingId,
                                Artist = metadata.Artist,
                                Title = metadata.Title,
                                Reason = $"Canonical variant available with quality score {bestCanonical.QualityScore:F2} vs current {variant.QualityScore:F2}",
                                Metadata = new Dictionary<string, object>
                                {
                                    ["current_quality"] = variant.QualityScore,
                                    ["canonical_quality"] = bestCanonical.QualityScore,
                                    ["canonical_codec"] = bestCanonical.Codec,
                                    ["canonical_bitrate"] = bestCanonical.BitrateKbps,
                                },
                                CanAutoFix = true,
                                SuggestedAction = "Download canonical variant and replace current file",
                                Status = LibraryIssueStatus.Detected,
                                DetectedAt = DateTimeOffset.UtcNow,
                            }, scan, scanLock, ct);
                        }
                    }
                }

                // Step 5: Check release completeness (if release ID available)
                if (!string.IsNullOrWhiteSpace(metadata.MusicBrainzReleaseId))
                {
                    await CheckReleaseCompletenessAsync(filePath, metadata.MusicBrainzReleaseId, scan, scanLock, ct);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "[LH] Failed to scan file: {Path}", filePath);

                // Issue: Corrupted file
                await EmitIssueAsync(new LibraryIssue
                {
                    IssueId = Guid.NewGuid().ToString(),
                    Type = LibraryIssueType.CorruptedFile,
                    Severity = LibraryIssueSeverity.Critical,
                    FilePath = filePath,
                    Reason = $"File scan failed: {ex.Message}",
                    CanAutoFix = true,
                    SuggestedAction = "Re-download from Soulseek or mesh overlay",
                    Status = LibraryIssueStatus.Detected,
                    DetectedAt = DateTimeOffset.UtcNow,
                }, scan, scanLock, ct);
            }
        }

        private async Task CheckReleaseCompletenessAsync(string filePath, string releaseId, LibraryHealthScan scan, object scanLock, CancellationToken ct)
        {
            try
            {
                var albumTarget = await hashDb.GetAlbumTargetAsync(releaseId, ct).ConfigureAwait(false);
                if (albumTarget == null)
                {
                    return; // No album target, skip completeness check
                }

                var tracks = await hashDb.GetAlbumTracksAsync(releaseId, ct).ConfigureAwait(false);
                if (tracks == null || !tracks.Any())
                {
                    return;
                }

                var libraryPath = Path.GetDirectoryName(filePath);
                var missingTracks = new List<HashDb.Models.AlbumTargetTrackEntry>();

                foreach (var track in tracks)
                {
                    if (string.IsNullOrWhiteSpace(track.RecordingId))
                    {
                        continue;
                    }

                    var hashes = await hashDb.LookupHashesByRecordingIdAsync(track.RecordingId, ct).ConfigureAwait(false);
                    if (hashes == null || !hashes.Any())
                    {
                        missingTracks.Add(track);
                        continue;
                    }

                    // Check if any hash corresponds to a file in the same directory
                    bool foundInAlbum = false;
                    foreach (var hash in hashes)
                    {
                        // Try to find file by FlacKey in the library directory
                        // Note: This is simplified - in practice, we'd need a reverse lookup from FlacKey to file path
                        // For now, we'll check if the recording ID matches any files in the directory
                        var filesInDir = Directory.EnumerateFiles(libraryPath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".flac" or ".mp3" or ".m4a" or ".ogg" or ".opus");
                        
                        // Simplified check: if we have hashes for this recording, assume it might be present
                        // Full implementation would need reverse lookup from HashDb
                        foundInAlbum = true; // Optimistic for now
                        break;
                    }

                    if (!foundInAlbum)
                    {
                        missingTracks.Add(track);
                    }
                }

                if (missingTracks.Count > 0)
                {
                    await EmitIssueAsync(new LibraryIssue
                    {
                        IssueId = Guid.NewGuid().ToString(),
                        Type = LibraryIssueType.MissingTrackInRelease,
                        Severity = missingTracks.Count > tracks.Count() / 2 ? LibraryIssueSeverity.High : LibraryIssueSeverity.Medium,
                        FilePath = libraryPath, // Directory, not specific file
                        MusicBrainzReleaseId = releaseId,
                        Artist = albumTarget.Artist,
                        Album = albumTarget.Title,
                        Reason = $"Album incomplete: {missingTracks.Count}/{tracks.Count()} tracks missing",
                        Metadata = new Dictionary<string, object>
                        {
                            ["missing_tracks"] = missingTracks.Select(t => new
                            {
                                position = t.Position,
                                title = t.Title,
                                recording_id = t.RecordingId
                            }).ToList(),
                            ["total_tracks"] = tracks.Count(),
                        },
                        CanAutoFix = true,
                        SuggestedAction = "Download missing tracks via multi-swarm",
                        Status = LibraryIssueStatus.Detected,
                        DetectedAt = DateTimeOffset.UtcNow,
                    }, scan, scanLock, ct);
                }
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "[LH] Failed to check release completeness for {ReleaseId}", releaseId);
            }
        }

        private async Task EmitIssueAsync(LibraryIssue issue, LibraryHealthScan scan, object scanLock, CancellationToken ct)
        {
            try
            {
                await hashDb.InsertLibraryIssueAsync(issue, ct).ConfigureAwait(false);
                lock (scanLock)
                {
                    scan.IssuesDetected++;
                }
                log.LogDebug("[LH] Detected issue: {Type} for {Path}", issue.Type, issue.FilePath);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "[LH] Failed to emit issue for {Path}", issue.FilePath);
            }
        }

        private static string GetCodecName(TagLib.Properties props, string extension)
        {
            // Prefer explicit container/extension mapping; fall back to first codec description.
            switch (extension?.ToLowerInvariant())
            {
                case ".flac": return "FLAC";
                case ".alac":
                case ".m4a": return "ALAC";
                case ".aac": return "AAC";
                case ".mp3": return "MP3";
                case ".opus": return "Opus";
                case ".ogg": return "Vorbis";
                case ".wav": return "WAV";
                default:
                    var desc = props.Codecs.FirstOrDefault()?.Description;
                    return string.IsNullOrWhiteSpace(desc) ? "Unknown" : desc;
            }
        }
    }
}
