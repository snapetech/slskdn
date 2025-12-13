namespace slskd.Transfers.MultiSource.Playback
{
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;

    public interface IPlaybackFeedbackService
    {
        Task RecordAsync(PlaybackFeedback feedback, CancellationToken ct = default);
    }

    /// <summary>
    ///     Playback feedback sink (placeholder for future scheduling integration).
    /// </summary>
    public class PlaybackFeedbackService : IPlaybackFeedbackService
    {
        private readonly ILogger log = Log.ForContext<PlaybackFeedbackService>();
        private readonly IPlaybackPriorityService priorities;

        public PlaybackFeedbackService(IPlaybackPriorityService priorities)
        {
            this.priorities = priorities;
        }

        public async Task RecordAsync(PlaybackFeedback feedback, CancellationToken ct = default)
        {
            if (feedback == null)
            {
                return;
            }

            log.Debug("[PlaybackFeedback] job={JobId} track={TrackId} pos={Pos}ms buffer={Buf}ms",
                feedback.JobId, feedback.TrackId, feedback.PositionMs, feedback.BufferAheadMs);

            await priorities.RecordAsync(feedback, ct).ConfigureAwait(false);
        }
    }
}
















