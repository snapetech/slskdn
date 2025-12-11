namespace slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Merges multiple shadow index shards from different peers.
/// </summary>
public interface IShardMerger
{
    Task<ShadowIndexShard> MergeShardsAsync(List<ShadowIndexShard> shards, CancellationToken ct = default);
}

/// <summary>
/// Combines shards from multiple peers into aggregated view.
/// </summary>
public class ShardMerger : IShardMerger
{
    private readonly ILogger<ShardMerger> logger;

    public ShardMerger(ILogger<ShardMerger> logger)
    {
        this.logger = logger;
    }

    public Task<ShadowIndexShard> MergeShardsAsync(List<ShadowIndexShard> shards, CancellationToken ct)
    {
        logger.LogDebug("[VSF-MERGE] Merging {ShardCount} shards", shards.Count);

        if (shards.Count == 0)
        {
            throw new ArgumentException("Cannot merge empty shard list", nameof(shards));
        }

        if (shards.Count == 1)
        {
            return Task.FromResult(shards[0]);
        }

        // Aggregate peer ID hints (deduplicate)
        var allPeerHints = new HashSet<string>();
        foreach (var shard in shards)
        {
            foreach (var hint in shard.PeerIdHints)
            {
                allPeerHints.Add(Convert.ToHexString(hint));
            }
        }

        // Aggregate canonical variants (take top by quality per codec)
        var allVariants = shards
            .SelectMany(s => s.CanonicalVariants)
            .GroupBy(v => v.Codec)
            .SelectMany(group => group
                .OrderByDescending(v => v.QualityScore)
                .Take(5))  // Top 5 per codec across all shards
            .ToList();

        // Use most recent timestamp
        var latestTimestamp = shards.Max(s => s.Timestamp);

        // Use shortest TTL (most conservative)
        var minTTL = shards.Min(s => s.TTLSeconds);

        var merged = new ShadowIndexShard
        {
            ShardVersion = "1.0",
            Timestamp = latestTimestamp,
            TTLSeconds = minTTL,
            PeerIdHints = allPeerHints.Select(hex => Convert.FromHexString(hex)).ToList(),
            CanonicalVariants = allVariants,
            ApproximatePeerCount = allPeerHints.Count
        };

        logger.LogInformation("[VSF-MERGE] Merged {ShardCount} shards: {PeerCount} unique peers, {VariantCount} variants",
            shards.Count, merged.ApproximatePeerCount, merged.CanonicalVariants.Count);

        return Task.FromResult(merged);
    }
}

