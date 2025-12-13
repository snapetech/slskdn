namespace slskd.Transfers.MultiSource.Playback
{
    /// <summary>
    ///     Playback feedback payload (experimental).
    /// </summary>
    public class PlaybackFeedback
    {
        public string JobId { get; set; }

        public string? TrackId { get; set; }

        /// <summary>Current playback position in milliseconds.</summary>
        public long PositionMs { get; set; }

        /// <summary>Desired buffer ahead in milliseconds.</summary>
        public long BufferAheadMs { get; set; }
    }
}

















