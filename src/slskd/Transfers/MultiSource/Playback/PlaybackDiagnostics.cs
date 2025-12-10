namespace slskd.Transfers.MultiSource.Playback
{
    public class PlaybackDiagnostics
    {
        public string JobId { get; set; }

        public string? TrackId { get; set; }

        public long PositionMs { get; set; }

        public long BufferAheadMs { get; set; }

        public PriorityZone Priority { get; set; } = PriorityZone.Mid;
    }
}
