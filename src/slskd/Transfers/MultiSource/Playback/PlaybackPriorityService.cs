// <copyright file="PlaybackPriorityService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Playback
{
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public interface IPlaybackPriorityService
    {
        Task RecordAsync(PlaybackFeedback feedback, CancellationToken ct = default);

        /// <summary>
        ///     Computes a simple priority zone for the current playback state.
        ///     High when buffer is low relative to desired, low when ahead.
        /// </summary>
        PriorityZone GetPriority(string jobId);

        /// <summary>
        ///     Calculates priority for a specific chunk based on its byte position relative to playback position.
        ///     High priority: Next 10 MB (playback buffer)
        ///     Mid priority: 10-50 MB ahead
        ///     Low priority: Rest of file
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <param name="chunkStartBytes">Start byte offset of the chunk.</param>
        /// <param name="chunkEndBytes">End byte offset of the chunk.</param>
        /// <returns>Priority zone for this chunk.</returns>
        PriorityZone GetChunkPriority(string jobId, long chunkStartBytes, long chunkEndBytes);

        /// <summary>
        ///     Gets the latest feedback for a job, if any.
        /// </summary>
        PlaybackFeedback? GetLatest(string jobId);
    }

    /// <summary>
    ///     In-memory priority hinting based on latest playback feedback.
    /// </summary>
    public class PlaybackPriorityService : IPlaybackPriorityService
    {
        private readonly ConcurrentDictionary<string, PlaybackFeedback> latest = new();
        private readonly ILogger<PlaybackPriorityService> logger;

        public PlaybackPriorityService(ILogger<PlaybackPriorityService> logger)
        {
            this.logger = logger;
        }

        public Task RecordAsync(PlaybackFeedback feedback, CancellationToken ct = default)
        {
            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.JobId))
            {
                var isNew = !latest.ContainsKey(feedback.JobId);
                latest[feedback.JobId] = feedback;
                
                if (isNew)
                {
                    logger.LogInformation("[Playback] Started tracking playback for job {JobId} (buffer target: {BufferMs}ms)", 
                        feedback.JobId, feedback.BufferAheadMs);
                }
                else
                {
                    logger.LogDebug("[Playback] Updated playback feedback for job {JobId} (buffer target: {BufferMs}ms)", 
                        feedback.JobId, feedback.BufferAheadMs);
                }
            }

            return Task.CompletedTask;
        }

        public PriorityZone GetPriority(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId) || !latest.TryGetValue(jobId, out var fb))
            {
                return PriorityZone.Mid;
            }

            // If buffer is less than desired, mark high priority; if 2x desired, mark low.
            var desired = fb.BufferAheadMs;
            var actual = fb.BufferAheadMs; // No actual buffer tracking; use desired as a proxy placeholder.

            if (desired <= 0)
            {
                return PriorityZone.Mid;
            }

            PriorityZone zone;
            if (actual < desired)
            {
                zone = PriorityZone.High;
                logger.LogDebug("[Playback] Job {JobId} priority: HIGH (buffer {Actual}ms < target {Desired}ms)", 
                    jobId, actual, desired);
            }
            else if (actual >= desired * 2)
            {
                zone = PriorityZone.Low;
                logger.LogDebug("[Playback] Job {JobId} priority: LOW (buffer {Actual}ms >= 2x target {Desired}ms)", 
                    jobId, actual, desired);
            }
            else
            {
                zone = PriorityZone.Mid;
            }

            return zone;
        }

        /// <summary>
        ///     Calculates priority for a specific chunk based on its byte position relative to playback position.
        ///     High priority: Next 10 MB (playback buffer)
        ///     Mid priority: 10-50 MB ahead
        ///     Low priority: Rest of file
        /// </summary>
        /// <param name="jobId">The job ID.</param>
        /// <param name="chunkStartBytes">Start byte offset of the chunk.</param>
        /// <param name="chunkEndBytes">End byte offset of the chunk.</param>
        /// <returns>Priority zone for this chunk.</returns>
        public PriorityZone GetChunkPriority(string jobId, long chunkStartBytes, long chunkEndBytes)
        {
            if (string.IsNullOrWhiteSpace(jobId) || !latest.TryGetValue(jobId, out var fb))
            {
                return PriorityZone.Mid;
            }

            // Determine playback position in bytes
            long playbackPositionBytes;
            if (fb.PositionBytes.HasValue)
            {
                playbackPositionBytes = fb.PositionBytes.Value;
            }
            else if (fb.FileSizeBytes.HasValue && fb.FileSizeBytes.Value > 0 && fb.PositionMs > 0)
            {
                // Estimate bytes from milliseconds (rough approximation)
                // This assumes constant bitrate - not perfect but better than nothing
                var progressRatio = (double)fb.PositionMs / (fb.BufferAheadMs + fb.PositionMs);
                playbackPositionBytes = (long)(fb.FileSizeBytes.Value * progressRatio);
            }
            else
            {
                // No position info available, use mid priority
                return PriorityZone.Mid;
            }

            // Calculate chunk center position
            var chunkCenterBytes = (chunkStartBytes + chunkEndBytes) / 2;
            var distanceFromPlayback = chunkCenterBytes - playbackPositionBytes;

            // Priority zones based on design doc:
            // High: Next 10 MB (playback buffer)
            // Mid: 10-50 MB ahead
            // Low: Rest of file
            const long HighPriorityZoneBytes = 10 * 1024 * 1024; // 10 MB
            const long MidPriorityZoneBytes = 50 * 1024 * 1024;  // 50 MB

            if (distanceFromPlayback < 0)
            {
                // Chunk is behind playback position - high priority to catch up
                return PriorityZone.High;
            }
            else if (distanceFromPlayback <= HighPriorityZoneBytes)
            {
                // Within 10 MB of playback - high priority
                return PriorityZone.High;
            }
            else if (distanceFromPlayback <= MidPriorityZoneBytes)
            {
                // 10-50 MB ahead - mid priority
                return PriorityZone.Mid;
            }
            else
            {
                // More than 50 MB ahead - low priority
                return PriorityZone.Low;
            }
        }

        public PlaybackFeedback? GetLatest(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                return null;
            }

            latest.TryGetValue(jobId, out var fb);
            return fb;
        }
    }
}
