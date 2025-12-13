namespace slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// TTL and eviction policy for shadow index shards.
/// </summary>
public static class ShardEvictionPolicy
{
    /// <summary>
    /// Default TTL for recording shards (1 hour).
    /// </summary>
    public const int DefaultRecordingTTLSeconds = 3600;

    /// <summary>
    /// Default TTL for release shards (4 hours).
    /// </summary>
    public const int DefaultReleaseTTLSeconds = 14400;

    /// <summary>
    /// Default TTL for scene shards (30 minutes).
    /// </summary>
    public const int DefaultSceneTTLSeconds = 1800;

    /// <summary>
    /// Maximum shard size in bytes (10 KB).
    /// </summary>
    public const int MaxShardSizeBytes = 10 * 1024;

    /// <summary>
    /// Maximum variants per shard.
    /// </summary>
    public const int MaxVariantsPerShard = 20;

    /// <summary>
    /// Maximum peer hints per shard.
    /// </summary>
    public const int MaxPeerHintsPerShard = 50;

    /// <summary>
    /// Check if a shard is expired based on its timestamp and TTL.
    /// </summary>
    public static bool IsExpired(ShadowIndexShard shard)
    {
        var expiresAt = shard.Timestamp.AddSeconds(shard.TTLSeconds);
        return DateTimeOffset.UtcNow > expiresAt;
    }

    /// <summary>
    /// Check if a shard exceeds size limits.
    /// </summary>
    public static bool ExceedsSizeLimit(ShadowIndexShard shard)
    {
        var estimatedSize = ShardSerializer.EstimateSize(shard);
        return estimatedSize > MaxShardSizeBytes;
    }

    /// <summary>
    /// Trim a shard to fit within size and count limits.
    /// </summary>
    public static ShadowIndexShard TrimShard(ShadowIndexShard shard)
    {
        var trimmed = new ShadowIndexShard
        {
            ShardVersion = shard.ShardVersion,
            Timestamp = shard.Timestamp,
            TTLSeconds = shard.TTLSeconds,
            PeerIdHints = shard.PeerIdHints.Take(MaxPeerHintsPerShard).ToList(),
            CanonicalVariants = shard.CanonicalVariants
                .OrderByDescending(v => v.QualityScore)
                .Take(MaxVariantsPerShard)
                .ToList(),
            ApproximatePeerCount = shard.ApproximatePeerCount
        };

        return trimmed;
    }

    /// <summary>
    /// Determine TTL for a shard based on its type.
    /// </summary>
    public static int GetTTLForKey(byte[] dhtKey)
    {
        // In practice, we'd inspect the key namespace
        // For now, return default
        return DefaultRecordingTTLSeconds;
    }
}
















