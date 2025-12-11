// <copyright file="IMultiSourceDownloadService.cs" company="slskdn Team">
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

namespace slskd.Transfers.MultiSource
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     Service for downloading files from multiple verified sources in parallel.
    /// </summary>
    public interface IMultiSourceDownloadService
    {
        /// <summary>
        ///     Finds and verifies alternative sources for a file.
        /// </summary>
        /// <param name="filename">The filename to search for.</param>
        /// <param name="fileSize">The expected file size.</param>
        /// <param name="excludeUsername">Username to exclude (current failed source).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Verification result with grouped sources.</returns>
        Task<ContentVerificationResult> FindVerifiedSourcesAsync(
            string filename,
            long fileSize,
            string excludeUsername = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Selects sources preferring canonical variants when available.
        /// </summary>
        Task<List<VerifiedSource>> SelectCanonicalSourcesAsync(
            ContentVerificationResult verificationResult,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Determines whether to skip download if local variant quality is sufficient.
        /// </summary>
        Task<bool> ShouldSkipDownloadAsync(
            string recordingId,
            Audio.AudioVariant proposedVariant,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Downloads a file from multiple verified sources in parallel.
        /// </summary>
        /// <param name="request">The multi-source download request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The download result.</returns>
        Task<MultiSourceDownloadResult> DownloadAsync(
            MultiSourceDownloadRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///     Gets the status of an ongoing multi-source download.
        /// </summary>
        /// <param name="downloadId">The download ID.</param>
        /// <returns>The download status, or null if not found.</returns>
        MultiSourceDownloadStatus GetStatus(Guid downloadId);

        /// <summary>
        ///     Gets all active downloads.
        /// </summary>
        System.Collections.Concurrent.ConcurrentDictionary<Guid, MultiSourceDownloadStatus> ActiveDownloads { get; }
    }

    /// <summary>
    ///     Request for a multi-source download.
    /// </summary>
    public class MultiSourceDownloadRequest
    {
        /// <summary>
        ///     Gets or sets the download ID.
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        ///     Gets or sets the filename to download.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the file size in bytes.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        ///     Gets or sets the expected content hash.
        /// </summary>
        public string ExpectedHash { get; set; }

        /// <summary>
        ///     Gets or sets the verified sources to download from.
        /// </summary>
        public List<VerifiedSource> Sources { get; set; } = new();

        /// <summary>
        ///     Gets or sets the local output path.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        ///     Gets or sets the chunk size in bytes. Default is 1MB.
        /// </summary>
        public long ChunkSize { get; set; } = 512 * 1024;  // 512KB default

        /// <summary>
        ///     Gets or sets the semantic group key derived from MusicBrainz recording and codec.
        /// </summary>
        public string TargetSemanticKey { get; set; }

        /// <summary>
        ///     Gets or sets the MusicBrainz recording ID targeted by this download.
        /// </summary>
        public string TargetMusicBrainzRecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the fingerprint expected for the target.
        /// </summary>
        public string TargetFingerprint { get; set; }

        /// <summary>
        ///     Gets or sets the expected duration in milliseconds for the recording.
        /// </summary>
        public int? TargetDurationMs { get; set; }
    }

    /// <summary>
    ///     Result of a multi-source download.
    /// </summary>
    public class MultiSourceDownloadResult
    {
        /// <summary>
        ///     Gets or sets the download ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether the download succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Gets or sets the filename.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the final file path.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        ///     Gets or sets the total bytes downloaded.
        /// </summary>
        public long BytesDownloaded { get; set; }

        /// <summary>
        ///     Gets or sets the total time taken in milliseconds.
        /// </summary>
        public long TotalTimeMs { get; set; }

        /// <summary>
        ///     Gets or sets the number of sources used.
        /// </summary>
        public int SourcesUsed { get; set; }

        /// <summary>
        ///     Gets or sets the chunk results.
        /// </summary>
        public List<ChunkResult> Chunks { get; set; } = new();

        /// <summary>
        ///     Gets or sets the error message if failed.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        ///     Gets or sets the verified content hash of the downloaded file.
        /// </summary>
        public string FinalHash { get; set; }

        /// <summary>
        ///     Gets or sets the fingerprint extracted from the completed download.
        /// </summary>
        public string Fingerprint { get; set; }

        /// <summary>
        ///     Gets or sets whether the fingerprint matches the target semantic key/recording.
        /// </summary>
        public bool FingerprintVerified { get; set; }

        /// <summary>
        ///     Gets or sets the resolved MusicBrainz recording ID from the fingerprint.
        /// </summary>
        public string ResolvedRecordingId { get; set; }

    }

    /// <summary>
    ///     Result of downloading a single chunk.
    /// </summary>
    public class ChunkResult
    {
        /// <summary>
        ///     Gets or sets the source username.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the MusicBrainz recording ID used for this chunk.
        /// </summary>
        public string MusicBrainzRecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the fingerprint associated with this chunk.
        /// </summary>
        public string Fingerprint { get; set; }

        /// <summary>
        ///     Gets or sets the start offset.
        /// </summary>
        public long StartOffset { get; set; }

        /// <summary>
        ///     Gets or sets the end offset.
        /// </summary>
        public long EndOffset { get; set; }

        /// <summary>
        ///     Gets or sets the bytes downloaded.
        /// </summary>
        public long BytesDownloaded { get; set; }

        /// <summary>
        ///     Gets or sets the total time taken in milliseconds (start to finish).
        /// </summary>
        public long TimeMs { get; set; }

        /// <summary>
        ///     Gets or sets time to first byte in milliseconds (connection + handshake overhead).
        /// </summary>
        public long TimeToFirstByteMs { get; set; }

        /// <summary>
        ///     Gets or sets actual transfer time in milliseconds (first byte to last byte).
        /// </summary>
        public long TransferTimeMs { get; set; }

        /// <summary>
        ///     Gets the overhead percentage (non-transfer time / total time).
        /// </summary>
        public double OverheadPercent => TimeMs > 0 ? ((TimeMs - TransferTimeMs) * 100.0) / TimeMs : 0;

        /// <summary>
        ///     Gets or sets a value indicating whether the chunk succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Gets or sets the error message if failed.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        ///     Gets the average speed in bytes per second (total time).
        /// </summary>
        public double SpeedBps => TimeMs > 0 ? (BytesDownloaded * 1000.0) / TimeMs : 0;

        /// <summary>
        ///     Gets the transfer speed in bytes per second (transfer time only, excludes overhead).
        /// </summary>
        public double TransferSpeedBps => TransferTimeMs > 0 ? (BytesDownloaded * 1000.0) / TransferTimeMs : 0;
    }

    /// <summary>
    ///     Status of an ongoing multi-source download.
    /// </summary>
    public class MultiSourceDownloadStatus
    {
        private long bytesDownloaded;
        private int activeChunks;
        private int completedChunks;
        private int activeWorkers;
        private long bestSpeedBps;

        /// <summary>
        ///     Gets or sets the download ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        ///     Gets or sets the filename.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the file size.
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        ///     Gets or sets the state.
        /// </summary>
        public MultiSourceDownloadState State { get; set; }

        /// <summary>
        ///     Gets or sets the bytes downloaded so far.
        /// </summary>
        public long BytesDownloaded
        {
            get => System.Threading.Interlocked.Read(ref bytesDownloaded);
            set => System.Threading.Interlocked.Exchange(ref bytesDownloaded, value);
        }

        /// <summary>
        ///     Gets or sets the number of active chunks.
        /// </summary>
        public int ActiveChunks
        {
            get => System.Threading.Interlocked.CompareExchange(ref activeChunks, 0, 0);
            set => System.Threading.Interlocked.Exchange(ref activeChunks, value);
        }

        /// <summary>
        ///     Gets or sets the number of active workers.
        /// </summary>
        public int ActiveWorkers
        {
            get => System.Threading.Interlocked.CompareExchange(ref activeWorkers, 0, 0);
            set => System.Threading.Interlocked.Exchange(ref activeWorkers, value);
        }

        /// <summary>
        ///     Gets or sets the number of completed chunks.
        /// </summary>
        public int CompletedChunks
        {
            get => System.Threading.Interlocked.CompareExchange(ref completedChunks, 0, 0);
            set => System.Threading.Interlocked.Exchange(ref completedChunks, value);
        }

        /// <summary>
        ///     Gets or sets the total number of chunks.
        /// </summary>
        public int TotalChunks { get; set; }

        /// <summary>
        ///     Fingerprint extracted from the completed download.
        /// </summary>
        public string Fingerprint { get; set; }

        /// <summary>
        ///     Whether the fingerprint matched the target (MBID/semantic key).
        /// </summary>
        public bool FingerprintVerified { get; set; }

        /// <summary>
        ///     Resolved MusicBrainz recording ID for this download.
        /// </summary>
        public string ResolvedRecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the target MusicBrainz recording ID being satisfied.
        /// </summary>
        public string TargetMusicBrainzRecordingId { get; set; }

        /// <summary>
        ///     Gets or sets the fingerprint expected for the download.
        /// </summary>
        public string TargetFingerprint { get; set; }


        /// <summary>
        ///     Gets or sets the semantic group key for this download.
        /// </summary>
        public string TargetSemanticKey { get; set; }

        /// <summary>
        ///     Gets the percent complete.
        /// </summary>
        public double PercentComplete => FileSize > 0 ? (BytesDownloaded * 100.0) / FileSize : 0;

        /// <summary>
        ///     Gets or sets the best observed speed in bytes per second.
        /// </summary>
        public long BestSpeedBps
        {
            get => System.Threading.Interlocked.Read(ref bestSpeedBps);
            set => System.Threading.Interlocked.Exchange(ref bestSpeedBps, value);
        }

        /// <summary>
        ///     Peers in timeout (username -> timeout expiry).
        /// </summary>
        public System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> PeerTimeouts { get; } = new();

        /// <summary>
        ///     Thread-safe increment of active chunks.
        /// </summary>
        public void IncrementActiveChunks() => System.Threading.Interlocked.Increment(ref activeChunks);

        /// <summary>
        ///     Thread-safe decrement of active chunks.
        /// </summary>
        public void DecrementActiveChunks() => System.Threading.Interlocked.Decrement(ref activeChunks);

        /// <summary>
        ///     Thread-safe increment of active workers.
        /// </summary>
        public void IncrementActiveWorkers() => System.Threading.Interlocked.Increment(ref activeWorkers);

        /// <summary>
        ///     Thread-safe decrement of active workers.
        /// </summary>
        public void DecrementActiveWorkers() => System.Threading.Interlocked.Decrement(ref activeWorkers);

        /// <summary>
        ///     Thread-safe increment of completed chunks.
        /// </summary>
        public void IncrementCompletedChunks() => System.Threading.Interlocked.Increment(ref completedChunks);

        /// <summary>
        ///     Thread-safe add bytes downloaded.
        /// </summary>
        /// <param name="bytes">Bytes to add.</param>
        public void AddBytesDownloaded(long bytes) => System.Threading.Interlocked.Add(ref bytesDownloaded, bytes);

        /// <summary>
        ///     Thread-safe update of best speed (only if new speed is higher).
        /// </summary>
        /// <param name="speedBps">Speed in bytes per second.</param>
        public void UpdateBestSpeed(long speedBps)
        {
            long current;
            do
            {
                current = System.Threading.Interlocked.Read(ref bestSpeedBps);
                if (speedBps <= current) return;
            }
            while (System.Threading.Interlocked.CompareExchange(ref bestSpeedBps, speedBps, current) != current);
        }

        /// <summary>
        ///     Check if peer is in timeout.
        /// </summary>
        public bool IsPeerInTimeout(string username)
        {
            if (PeerTimeouts.TryGetValue(username, out var expiry))
            {
                if (DateTime.UtcNow < expiry) return true;
                PeerTimeouts.TryRemove(username, out _);
            }
            return false;
        }

        /// <summary>
        ///     Put peer in timeout.
        /// </summary>
        public void SetPeerTimeout(string username, TimeSpan duration)
        {
            PeerTimeouts[username] = DateTime.UtcNow + duration;
        }

        /// <summary>
        ///     Gets or sets the start time for rate calculation.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        ///     Gets the chunks per second rate.
        /// </summary>
        public double ChunksPerSecond
        {
            get
            {
                var elapsed = (DateTime.UtcNow - StartTime).TotalSeconds;
                return elapsed > 0 ? CompletedChunks / elapsed : 0;
            }
        }

        /// <summary>
        ///     Gets the estimated seconds remaining.
        /// </summary>
        public double EstimatedSecondsRemaining
        {
            get
            {
                var rate = ChunksPerSecond;
                var remaining = TotalChunks - CompletedChunks;
                return rate > 0 ? remaining / rate : -1;
            }
        }
    }

    /// <summary>
    ///     State of a multi-source download.
    /// </summary>
    public enum MultiSourceDownloadState
    {
        /// <summary>
        ///     Verifying sources.
        /// </summary>
        Verifying,

        /// <summary>
        ///     Downloading chunks.
        /// </summary>
        Downloading,

        /// <summary>
        ///     Assembling chunks.
        /// </summary>
        Assembling,

        /// <summary>
        ///     Verifying final hash.
        /// </summary>
        VerifyingFinal,

        /// <summary>
        ///     Completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        ///     Failed.
        /// </summary>
        Failed,
    }
}

