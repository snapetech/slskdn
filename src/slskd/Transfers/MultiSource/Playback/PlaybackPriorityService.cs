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

















