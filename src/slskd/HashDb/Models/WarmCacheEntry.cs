// <copyright file="WarmCacheEntry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.HashDb.Models
{
    public class WarmCacheEntry
    {
        public string ContentId { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public long SizeBytes { get; set; }

        public bool Pinned { get; set; }

        public long LastAccessed { get; set; } // unix seconds
    }
}
