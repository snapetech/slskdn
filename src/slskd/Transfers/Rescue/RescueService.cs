// <copyright file="RescueService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Transfers.Rescue
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.HashDb;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.Chromaprint;
    using slskd.Mesh;
    using slskd.Transfers.Downloads;
    using slskd.Transfers.MultiSource;
    using slskd.Transfers.MultiSource.Metrics;

    /// <summary>
    ///     Service for activating rescue mode on underperforming transfers.
    /// </summary>
    public interface IRescueService
    {
        /// <summary>
        ///     Activate rescue mode for a transfer.
        /// </summary>
        Task<RescueJob> ActivateRescueModeAsync(
            string transferId,
            string username,
            string filename,
            long totalBytes,
            long bytesTransferred,
            UnderperformanceReason reason,
            CancellationToken ct = default);

        /// <summary>
        ///     Deactivate rescue mode (transfer recovered or completed).
        /// </summary>
        Task DeactivateRescueModeAsync(string transferId, CancellationToken ct = default);

        /// <summary>
        ///     Returns whether rescue mode is currently active (or pending) for the given transfer.
        /// </summary>
        bool IsRescueActive(string transferId);
    }

    /// <summary>
    ///     Implements rescue mode for underperforming Soulseek transfers.
    /// </summary>
    public class RescueService : IRescueService
    {
        private readonly IHashDbService hashDb;
        private readonly IFingerprintExtractionService fingerprinting;
        private readonly IAcoustIdClient acoustId;
        private readonly IMeshSyncService meshSync;
        private readonly IMeshDirectory meshDirectory;
        private readonly IMultiSourceDownloadService multiSource;
        private readonly IDownloadService downloadService;
        private readonly IRescueGuardrailService guardrails;
        private readonly ConcurrentDictionary<string, string> activeRescueJobs = new(); // transferId -> multiSourceJobId (or "" when pending)
        private readonly ILogger log = Log.ForContext<RescueService>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="RescueService"/> class.
        /// </summary>
        public RescueService(
            IHashDbService hashDb = null,
            IFingerprintExtractionService fingerprinting = null,
            IAcoustIdClient acoustId = null,
            IMeshSyncService meshSync = null,
            IMeshDirectory meshDirectory = null,
            IMultiSourceDownloadService multiSource = null,
            IDownloadService downloadService = null,
            IRescueGuardrailService guardrails = null)
        {
            this.hashDb = hashDb;
            this.fingerprinting = fingerprinting;
            this.acoustId = acoustId;
            this.meshSync = meshSync;
            this.meshDirectory = meshDirectory;
            this.multiSource = multiSource;
            this.downloadService = downloadService;
            this.guardrails = guardrails ?? new RescueGuardrailService();  // Default if not provided
        }

        /// <inheritdoc/>
        public async Task<RescueJob> ActivateRescueModeAsync(
            string transferId,
            string username,
            string filename,
            long totalBytes,
            long bytesTransferred,
            UnderperformanceReason reason,
            CancellationToken ct = default)
        {
            log.Information("[RESCUE] Activating rescue mode for {File} (reason: {Reason})", filename, reason);

            // Step 0: Check guardrails
            var (allowed, guardReason) = await guardrails.CheckRescueAllowedAsync(transferId, filename, ct);
            if (!allowed)
            {
                log.Warning("[RESCUE] Rescue mode not allowed: {Reason}", guardReason);
                return null;
            }

            // Step 1: Resolve MusicBrainz Recording ID
            string recordingId = await ResolveRecordingIdAsync(filename, bytesTransferred, ct);

            if (recordingId == null)
            {
                log.Warning("[RESCUE] Cannot activate rescue: unable to resolve MusicBrainz Recording ID for {File}", filename);
                return null;
            }

            log.Information("[RESCUE] Resolved recording ID: {RecordingId}", recordingId);

            // Step 2: Query overlay mesh for peers with this recording
            var overlayPeers = await DiscoverOverlayPeersAsync(recordingId, ct);

            if (overlayPeers.Count == 0)
            {
                log.Warning("[RESCUE] Cannot activate rescue: no overlay peers found for recording {RecordingId}", recordingId);
                return null;
            }

            log.Information("[RESCUE] Found {Count} overlay peers with recording {RecordingId}", overlayPeers.Count, recordingId);

            // Step 2.5: Check guardrails for multi-source job
            // For now, assume original Soulseek transfer counts as 1 Soulseek peer
            int soulseekPeerCount = 1;  // The underperforming transfer
            var (jobAllowed, jobReason) = await guardrails.CheckMultiSourceJobAllowedAsync(
                overlayPeers.Count,
                soulseekPeerCount,
                ct);

            if (!jobAllowed)
            {
                log.Warning("[RESCUE] Multi-source job not allowed: {Reason}", jobReason);
                return null;
            }

            // Step 3: Determine missing byte ranges
            var missingRanges = ComputeMissingRanges(totalBytes, bytesTransferred);

            log.Information("[RESCUE] Computed {Count} missing ranges totaling {Bytes} bytes", missingRanges.Count, missingRanges.Sum(r => r.Length));

            // Step 4: Create rescue job
            var rescueJob = new RescueJob
            {
                RescueJobId = Guid.NewGuid().ToString(),
                OriginalTransferId = transferId,
                RecordingId = recordingId,
                Filename = filename,
                MissingRanges = missingRanges,
                OverlayPeerCount = overlayPeers.Count,
                ActivatedAt = DateTimeOffset.UtcNow,
                Reason = reason,
            };

            // Mark as rescue-active immediately so underperformance detector does not re-trigger
            activeRescueJobs[transferId] = "";

            // Step 5: Start overlay chunk transfers with IMultiSourceDownloadService
            if (multiSource != null && overlayPeers.Count > 0)
            {
                try
                {
                    var multiSourceRequest = new MultiSourceDownloadRequest
                    {
                        Filename = filename,
                        FileSize = totalBytes,
                        Sources = overlayPeers.Select(p => new VerifiedSource
                        {
                            Username = p.PeerId,
                            FullPath = filename,  // Use filename as path for overlay peers
                            MusicBrainzRecordingId = recordingId,
                            Method = VerificationMethod.None,  // Overlay peers are trusted
                        }).ToList(),
                        TargetMusicBrainzRecordingId = recordingId,
                        // TODO: Get proper output path from transfer service
                        OutputPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"rescue_{transferId}.tmp"),
                    };

                    log.Information("[RESCUE] Creating multi-source download job for {RangeCount} missing ranges totaling {Bytes} bytes",
                        missingRanges.Count, missingRanges.Sum(r => r.Length));

                    // Start the download asynchronously (fire and forget for now)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await multiSource.DownloadAsync(multiSourceRequest, ct);
                            if (result.Success)
                            {
                                log.Information("[RESCUE] Multi-source download completed successfully: {File}, {Bytes} bytes in {TimeMs}ms",
                                    result.Filename, result.BytesDownloaded, result.TotalTimeMs);
                                rescueJob.MultiSourceJobId = result.Id.ToString();
                                activeRescueJobs[transferId] = result.Id.ToString();
                            }
                            else
                            {
                                log.Warning("[RESCUE] Multi-source download failed: {File}, error: {Error}",
                                    result.Filename, result.Error);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex, "[RESCUE] Multi-source download threw exception: {Message}", ex.Message);
                        }
                    }, ct);

                    log.Information("[RESCUE] Rescue mode activated: job {JobId}, {PeerCount} overlay peers, multi-source download started",
                        rescueJob.RescueJobId, overlayPeers.Count);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "[RESCUE] Failed to create multi-source download: {Message}", ex.Message);
                }
            }
            else
            {
                log.Warning("[RESCUE] Multi-source download service not available or no overlay peers - rescue activation is placeholder only");
            }

            return rescueJob;
        }

        /// <inheritdoc/>
        public async Task DeactivateRescueModeAsync(string transferId, CancellationToken ct = default)
        {
            log.Information("[RESCUE] Deactivating rescue mode for transfer {TransferId}", transferId);
            
            if (activeRescueJobs.TryGetValue(transferId, out var jobId))
            {
                if (!string.IsNullOrEmpty(jobId) && Guid.TryParse(jobId, out var jobGuid))
                {
                    try
                    {
                        var status = multiSource?.GetStatus(jobGuid);
                        if (status != null && status.State != MultiSourceDownloadState.Completed && status.State != MultiSourceDownloadState.Failed)
                        {
                            log.Information("[RESCUE] Marking multi-source job {JobId} for transfer {TransferId} as inactive", jobId, transferId);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, "[RESCUE] Error checking multi-source job status for {TransferId}", transferId);
                    }
                }

                activeRescueJobs.TryRemove(transferId, out _);
            }
            
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public bool IsRescueActive(string transferId)
        {
            return !string.IsNullOrEmpty(transferId) && activeRescueJobs.ContainsKey(transferId);
        }

        private string GetOutputPathForTransfer(string transferId)
        {
            if (downloadService == null || !Guid.TryParse(transferId, out var transferGuid))
            {
                return Path.Combine(Path.GetTempPath(), $"rescue_{transferId}.tmp");
            }

            try
            {
                var transfer = downloadService.Find(t => t.Id == transferGuid);
                if (transfer != null)
                {
                    // Try to get the actual download path from transfer
                    // Note: Transfer model may not have LocalPath, so fallback to temp
                    var downloadDir = Path.Combine(Path.GetTempPath(), "slskd", "downloads");
                    Directory.CreateDirectory(downloadDir);
                    return Path.Combine(downloadDir, Path.GetFileName(transfer.Filename));
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[RESCUE] Failed to get output path for transfer {TransferId}, using temp", transferId);
            }

            return Path.Combine(Path.GetTempPath(), $"rescue_{transferId}.tmp");
        }

        private async Task<string> ResolveRecordingIdAsync(string filename, long bytesTransferred, CancellationToken ct)
        {
            // Strategy 1: Check HashDb for existing fingerprint by file hash
            if (hashDb != null)
            {
                try
                {
                    var partialFilePath = GetPartialFilePath(filename);
                    if (partialFilePath != null && File.Exists(partialFilePath))
                    {
                        // Compute file hash for lookup
                        var fileHash = await ComputeFileHashAsync(partialFilePath, ct);
                        if (!string.IsNullOrEmpty(fileHash))
                        {
                            // Lookup by hash in HashDb
                            var flacKey = slskd.HashDb.Models.HashDbEntry.GenerateFlacKey(filename, bytesTransferred);
                            var hashEntry = await hashDb.LookupHashAsync(flacKey, ct);
                            
                            if (hashEntry != null && !string.IsNullOrEmpty(hashEntry.MusicBrainzId))
                            {
                                log.Debug("[RESCUE] Found recording ID in HashDb: {RecordingId}", hashEntry.MusicBrainzId);
                                return hashEntry.MusicBrainzId;
                            }

                            // Try lookup by size
                            var entriesBySize = await hashDb.LookupHashesBySizeAsync(bytesTransferred, ct);
                            var matchingEntry = entriesBySize.FirstOrDefault(e => e.ByteHash == fileHash);
                            if (matchingEntry != null && !string.IsNullOrEmpty(matchingEntry.MusicBrainzId))
                            {
                                log.Debug("[RESCUE] Found recording ID in HashDb by size/hash: {RecordingId}", matchingEntry.MusicBrainzId);
                                return matchingEntry.MusicBrainzId;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[RESCUE] HashDb lookup failed, continuing");
                }
            }

            // Strategy 2: Try fingerprinting partial file if enough data downloaded
            if (fingerprinting != null && acoustId != null && bytesTransferred > 5 * 1024 * 1024) // At least 5 MB
            {
                try
                {
                    var partialFilePath = GetPartialFilePath(filename);
                    if (partialFilePath != null && File.Exists(partialFilePath))
                    {
                    var fingerprint = await fingerprinting.ExtractFingerprintAsync(partialFilePath, ct);
                    if (!string.IsNullOrEmpty(fingerprint))
                    {
                        // Estimate duration from bytes transferred (rough estimate: ~1MB per minute for FLAC)
                        var estimatedDurationSeconds = Math.Max(30, (int)(bytesTransferred / (1024.0 * 1024.0))); // At least 30 seconds
                        var sampleRate = 44100; // Default, could be improved by reading file metadata
                        
                        var lookupResult = await acoustId.LookupAsync(fingerprint, sampleRate, estimatedDurationSeconds, ct);
                        if (lookupResult != null && lookupResult.Recordings != null && lookupResult.Recordings.Any())
                        {
                            var recordingId = lookupResult.Recordings[0].Id;
                            log.Debug("[RESCUE] Resolved recording ID via AcoustID fingerprint: {RecordingId}", recordingId);
                            return recordingId;
                        }
                    }
                    }
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[RESCUE] Fingerprinting failed, continuing");
                }
            }

            // Strategy 3: Parse filename for MBID hint (e.g., "Song [mbid-abc123].flac")
            var mbidFromFilename = ExtractMbidFromFilename(filename);
            if (mbidFromFilename != null)
            {
                log.Debug("[RESCUE] Extracted recording ID from filename: {RecordingId}", mbidFromFilename);
                return mbidFromFilename;
            }

            log.Debug("[RESCUE] Unable to resolve recording ID via any strategy");
            return null;
        }

        private async Task<List<OverlayPeerInfo>> DiscoverOverlayPeersAsync(string recordingId, CancellationToken ct)
        {
            var peers = new List<OverlayPeerInfo>();

            if (meshDirectory != null)
            {
                try
                {
                    // Query mesh DHT for peers advertising this recording
                    var contentId = $"mbid:recording:{recordingId}";
                    log.Debug("[RESCUE] Querying mesh DHT for content ID: {ContentId}", contentId);

                    var meshPeers = await meshDirectory.FindPeersByContentAsync(contentId, ct);
                    
                    foreach (var meshPeer in meshPeers)
                    {
                        peers.Add(new OverlayPeerInfo
                        {
                            PeerId = meshPeer.PeerId,
                            Endpoint = meshPeer.Address != null && meshPeer.Port.HasValue 
                                ? $"{meshPeer.Address}:{meshPeer.Port.Value}" 
                                : null,
                            AvailabilityScore = 1.0 // Default score, could be improved with peer metrics
                        });
                    }

                    log.Debug("[RESCUE] Found {Count} mesh peers for recording {RecordingId}", peers.Count, recordingId);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[RESCUE] Mesh query failed: {Message}", ex.Message);
                }
            }
            else
            {
                log.Debug("[RESCUE] MeshDirectory not available, skipping overlay peer discovery");
            }

            return peers;
        }

        private string GetPartialFilePath(string filename)
        {
            if (downloadService == null)
            {
                return null;
            }

            try
            {
                // Try to find transfer by filename
                var transfer = downloadService.Find(t => t.Filename == filename && t.Direction == Soulseek.TransferDirection.Download);
                if (transfer != null)
                {
                    // Construct partial file path (typically in downloads directory)
                    var downloadDir = Path.Combine(Path.GetTempPath(), "slskd", "downloads");
                    var partialPath = Path.Combine(downloadDir, $"{transfer.Id}.partial");
                    
                    if (File.Exists(partialPath))
                    {
                        return partialPath;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[RESCUE] Failed to get partial file path for {Filename}", filename);
            }

            return null;
        }

        private async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = await sha256.ComputeHashAsync(stream, ct);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[RESCUE] Failed to compute file hash for {Path}", filePath);
                return null;
            }
        }

        private List<ByteRange> ComputeMissingRanges(long totalBytes, long bytesTransferred)
        {
            var missingRanges = new List<ByteRange>();

            // For now, assume missing bytes are at the end (simple case)
            // In full implementation, would need to track which chunks are complete
            if (bytesTransferred < totalBytes)
            {
                missingRanges.Add(new ByteRange
                {
                    Offset = bytesTransferred,
                    Length = totalBytes - bytesTransferred,
                });
            }

            return missingRanges;
        }

        private string ExtractMbidFromFilename(string filename)
        {
            // Simple regex to extract MBID from filename like "[mbid-UUID]"
            var match = System.Text.RegularExpressions.Regex.Match(
                filename,
                @"\[mbid-([a-f0-9\-]{36})\]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }
    }

    /// <summary>
    ///     Represents a rescue job for an underperforming transfer.
    /// </summary>
    public class RescueJob
    {
        /// <summary>
        ///     Gets or sets the rescue job ID.
        /// </summary>
        public string RescueJobId { get; set; }

        /// <summary>
        ///     Gets or sets the original transfer ID.
        /// </summary>
        public string OriginalTransferId { get; set; }

        /// <summary>
        ///     Gets or sets the MusicBrainz Recording ID.
        /// </summary>
        public string RecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the filename.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the missing byte ranges.
        /// </summary>
        public List<ByteRange> MissingRanges { get; set; }

        /// <summary>
        ///     Gets or sets the number of overlay peers available.
        /// </summary>
        public int OverlayPeerCount { get; set; }

        /// <summary>
        ///     Gets or sets the activation timestamp.
        /// </summary>
        public DateTimeOffset ActivatedAt { get; set; }

        /// <summary>
        ///     Gets or sets the underperformance reason.
        /// </summary>
        public UnderperformanceReason Reason { get; set; }

        /// <summary>
        ///     Gets or sets the multi-source job ID (if created).
        /// </summary>
        public string MultiSourceJobId { get; set; }
    }

    /// <summary>
    ///     Represents a byte range in a file.
    /// </summary>
    public class ByteRange
    {
        /// <summary>
        ///     Gets or sets the offset in bytes.
        /// </summary>
        public long Offset { get; set; }

        /// <summary>
        ///     Gets or sets the length in bytes.
        /// </summary>
        public long Length { get; set; }
    }

    /// <summary>
    ///     Information about an overlay peer.
    /// </summary>
    public class OverlayPeerInfo
    {
        /// <summary>
        ///     Gets or sets the peer ID.
        /// </summary>
        public string PeerId { get; set; }

        /// <summary>
        ///     Gets or sets the peer endpoint (IP:Port or overlay address).
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        ///     Gets or sets the availability score (0.0 - 1.0).
        /// </summary>
        public double AvailabilityScore { get; set; }
    }

    /// <summary>
    ///     Reasons for transfer underperformance.
    /// </summary>
    public enum UnderperformanceReason
    {
        /// <summary>
        ///     Transfer queued for too long.
        /// </summary>
        QueuedTooLong,

        /// <summary>
        ///     Throughput below minimum threshold.
        /// </summary>
        ThroughputTooLow,

        /// <summary>
        ///     Transfer stalled (no progress).
        /// </summary>
        Stalled,

        /// <summary>
        ///     Peer disconnected.
        /// </summary>
        PeerDisconnected,
    }
}

