namespace slskd.HashDb.Models
{
    /// <summary>
    ///     Aggregated traffic counters for overlay and Soulseek.
    /// </summary>
    public class TrafficTotals
    {
        public long OverlayUploadBytes { get; set; }

        public long OverlayDownloadBytes { get; set; }

        public long SoulseekUploadBytes { get; set; }

        public long SoulseekDownloadBytes { get; set; }
    }
}

