namespace slskd.Transfers.MultiSource.Playback
{
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;

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

        public Task RecordAsync(PlaybackFeedback feedback, CancellationToken ct = default)
        {
            if (feedback != null && !string.IsNullOrWhiteSpace(feedback.JobId))
            {
                latest[feedback.JobId] = feedback;
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

            if (actual < desired)
            {
                return PriorityZone.High;
            }

            if (actual >= desired * 2)
            {
                return PriorityZone.Low;
            }

            return PriorityZone.Mid;
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

