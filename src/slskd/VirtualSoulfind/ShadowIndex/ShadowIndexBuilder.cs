namespace slskd.VirtualSoulfind.ShadowIndex;

using slskd.Audio;

/// <summary>
/// Interface for shadow index builder.
/// </summary>
public interface IShadowIndexBuilder
{
    /// <summary>
    /// Add a variant observation from a specific username.
    /// </summary>
    Task AddVariantObservationAsync(
        string username,
        string recordingId,
        AudioVariant variant,
        CancellationToken ct = default);
    
    /// <summary>
    /// Build a shadow index shard for a specific MBID.
    /// </summary>
    Task<ShadowIndexShard?> BuildShardAsync(string mbid, CancellationToken ct = default);
}

/// <summary>
/// Compact shadow index shard (stored in DHT).
/// </summary>
public class ShadowIndexShard
{
    public string ShardVersion { get; set; } = "1.0";
    public DateTimeOffset Timestamp { get; set; }
    public int TTLSeconds { get; set; } = 3600;  // 1 hour default
    
    // Compact peer set (hashed overlay IDs, first 8 bytes)
    public List<byte[]> PeerIdHints { get; set; } = new();
    
    // Canonical variant hints
    public List<VariantHint> CanonicalVariants { get; set; } = new();
    
    public int ApproximatePeerCount { get; set; }
}

/// <summary>
/// Variant hint for shadow index.
/// </summary>
public class VariantHint
{
    public string Codec { get; set; } = string.Empty;
    public int BitrateKbps { get; set; }
    public long SizeBytes { get; set; }
    public byte[] HashPrefix { get; set; } = Array.Empty<byte>();  // First 16 bytes of SHA256
}

/// <summary>
/// Stub implementation of shadow index builder (Phase 6B will implement fully).
/// </summary>
public class ShadowIndexBuilderStub : IShadowIndexBuilder
{
    private readonly ILogger<ShadowIndexBuilderStub> logger;

    public ShadowIndexBuilderStub(ILogger<ShadowIndexBuilderStub> logger)
    {
        this.logger = logger;
    }

    public Task AddVariantObservationAsync(
        string username,
        string recordingId,
        AudioVariant variant,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-SHADOW] Received variant observation for {RecordingId} from {Username}",
            recordingId, username);
        
        // TODO: Phase 6B will implement actual shard building and DHT publishing
        return Task.CompletedTask;
    }

    public Task<ShadowIndexShard?> BuildShardAsync(string mbid, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SHADOW] Build shard requested for {MBID}", mbid);
        
        // TODO: Phase 6B will implement actual shard aggregation
        return Task.FromResult<ShadowIndexShard?>(null);
    }
}
