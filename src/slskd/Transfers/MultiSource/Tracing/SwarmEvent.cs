namespace slskd.Transfers.MultiSource.Tracing
{
    using System;

    /// <summary>
    ///     A structured event emitted during a multi-source swarm session.
    /// </summary>
    public class SwarmEvent
    {
        public string JobId { get; set; }

        public string? TrackId { get; set; }

        public string? VariantId { get; set; }

        public string? PeerId { get; set; }

        public SwarmEventType EventType { get; set; }

        public SwarmEventSource Source { get; set; }

        public string? Backend { get; set; } // soulseek | overlay | bittorrent-private | http | lan | ipfs

        public long? ChunkIndex { get; set; }

        public long? Bytes { get; set; }

        public string? Error { get; set; }

        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    public enum SwarmEventType
    {
        ChunkRequest,
        ChunkReceived,
        ChunkVerified,
        ChunkFailed,
        ChunkTimedOut,
        RescueInvoked,
        PeerDegraded,
        PeerRecovered,
        JobStarted,
        JobCompleted,
        JobFailed,
    }

    public enum SwarmEventSource
    {
        Soulseek,
        Overlay,
        Http,
        BitTorrentPrivate,
        Lan,
        Ipfs,
    }
}


