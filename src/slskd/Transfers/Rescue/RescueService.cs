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
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.HashDb;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.Chromaprint;
    using slskd.Mesh;
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
        private readonly IMultiSourceDownloadService multiSource;
        private readonly IRescueGuardrailService guardrails;
        private readonly ILogger log = Log.ForContext<RescueService>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="RescueService"/> class.
        /// </summary>
        public RescueService(
            IHashDbService hashDb = null,
            IFingerprintExtractionService fingerprinting = null,
            IAcoustIdClient acoustId = null,
            IMeshSyncService meshSync = null,
            IMultiSourceDownloadService multiSource = null,
            IRescueGuardrailService guardrails = null)
        {
            this.hashDb = hashDb;
            this.fingerprinting = fingerprinting;
            this.acoustId = acoustId;
            this.meshSync = meshSync;
            this.multiSource = multiSource;
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
            // TODO: Cancel multi-source job, clean up resources
            await Task.CompletedTask;
        }

        private async Task<string> ResolveRecordingIdAsync(string filename, long bytesTransferred, CancellationToken ct)
        {
            // Strategy 1: Check HashDb for existing fingerprint (TODO: need file hash to lookup)
            if (hashDb != null)
            {
                try
                {
                    // TODO: In full implementation, compute file hash and lookup
                    // For now, skip HashDb lookup
                    log.Debug("[RESCUE] HashDb lookup pending implementation (need file hash)");
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
                    // TODO: Get actual partial file path from transfer service
                    // For now, this is a placeholder
                    log.Debug("[RESCUE] Partial file fingerprinting not yet implemented (need {Bytes} MB downloaded)", bytesTransferred / (1024 * 1024));
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

            if (meshSync != null)
            {
                try
                {
                    // Query mesh for peers advertising this recording
                    // TODO: Implement actual DHT/overlay query
                    log.Debug("[RESCUE] Querying mesh for recording {RecordingId}", recordingId);

                    // Placeholder: return empty list for now
                    // In full implementation, this would query DHT keys like:
                    // - "mbid:recording:{recordingId}"
                    // - "fingerprint:bundle:{fingerprint_hash}"
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[RESCUE] Mesh query failed: {Message}", ex.Message);
                }
            }
            else
            {
                log.Debug("[RESCUE] MeshSyncService not available, skipping overlay peer discovery");
            }

            return peers;
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

