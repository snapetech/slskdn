using System;
using System.Collections.Generic;
using MessagePack;

namespace slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Compact shadow index shard (stored in DHT).
/// </summary>
[MessagePackObject]
public class ShadowIndexShard
{
    [Key(0)]
    public string ShardVersion { get; set; } = "1.0";
    
    [Key(1)]
    public DateTimeOffset Timestamp { get; set; }
    
    [Key(2)]
    public int TTLSeconds { get; set; } = 3600;  // 1 hour default
    
    [Key(3)]
    public List<byte[]> PeerIdHints { get; set; } = new();  // Each 8 bytes
    
    [Key(4)]
    public List<VariantHint> CanonicalVariants { get; set; } = new();
    
    [Key(5)]
    public int ApproximatePeerCount { get; set; }
}

/// <summary>
/// Variant hint for shadow index (compact representation).
/// </summary>
[MessagePackObject]
public class VariantHint
{
    [Key(0)]
    public string Codec { get; set; } = string.Empty;  // "FLAC", "MP3", etc.
    
    [Key(1)]
    public int BitrateKbps { get; set; }
    
    [Key(2)]
    public long SizeBytes { get; set; }
    
    [Key(3)]
    public byte[] HashPrefix { get; set; } = Array.Empty<byte>();  // First 16 bytes of SHA256
    
    [Key(4)]
    public double QualityScore { get; set; }
}

/// <summary>
/// Serialization helper for shadow index shards.
/// </summary>
public static class ShardSerializer
{
    /// <summary>
    /// Serialize a shard to MessagePack format.
    /// </summary>
    public static byte[] Serialize(ShadowIndexShard shard)
    {
        return MessagePackSerializer.Serialize(shard);
    }

    /// <summary>
    /// Deserialize a shard from MessagePack format.
    /// </summary>
    public static ShadowIndexShard? Deserialize(byte[] data)
    {
        try
        {
            return MessagePackSerializer.Deserialize<ShadowIndexShard>(data);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Estimate shard size in bytes.
    /// </summary>
    public static int EstimateSize(ShadowIndexShard shard)
    {
        // Rough estimate: version(10) + timestamp(8) + TTL(4) + peerHints(N*8) + variants(M*50)
        return 32 + (shard.PeerIdHints.Count * 8) + (shard.CanonicalVariants.Count * 50);
    }
}
