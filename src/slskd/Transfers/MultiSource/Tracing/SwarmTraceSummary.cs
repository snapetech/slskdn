namespace slskd.Transfers.MultiSource.Tracing
{
    using System;
    using System.Collections.Generic;

    public class SwarmTraceSummary
    {
        public string JobId { get; set; }

        public DateTimeOffset? FirstEventAt { get; set; }

        public DateTimeOffset? LastEventAt { get; set; }

        public TimeSpan? Duration => (FirstEventAt.HasValue && LastEventAt.HasValue)
            ? LastEventAt - FirstEventAt
            : null;

        public int TotalEvents { get; set; }

        public Dictionary<string, int> EventCounts { get; set; } = new();

        public Dictionary<string, long> BytesBySource { get; set; } = new(); // key: source enum string

        public Dictionary<string, long> BytesByBackend { get; set; } = new(); // key: backend string

        public List<PeerContribution> Peers { get; set; } = new();

        public bool RescueInvoked { get; set; }
    }

    public class PeerContribution
    {
        public string PeerId { get; set; }

        public int ChunksCompleted { get; set; }

        public int ChunksFailed { get; set; }

        public int ChunksTimedOut { get; set; }

        public int ChunksCorrupted { get; set; }

        public long BytesServed { get; set; }
    }
}
















