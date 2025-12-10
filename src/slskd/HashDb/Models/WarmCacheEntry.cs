namespace slskd.HashDb.Models
{
    public class WarmCacheEntry
    {
        public string ContentId { get; set; }

        public string Path { get; set; }

        public long SizeBytes { get; set; }

        public bool Pinned { get; set; }

        public long LastAccessed { get; set; } // unix seconds
    }
}
