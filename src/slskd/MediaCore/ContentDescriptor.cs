namespace slskd.MediaCore;

/// <summary>
/// Hash descriptor entry.
/// </summary>
public record ContentHash(string Algorithm, string Hex);

/// <summary>
/// Perceptual hash entry (optional).
/// </summary>
public record PerceptualHash(string Algorithm, string Hex);

/// <summary>
/// Signature envelope for descriptors.
/// </summary>
public record DescriptorSignature(string PublicKey, string Signature, long TimestampUnixMs);

/// <summary>
/// Media descriptor for mesh publishing.
/// </summary>
public class ContentDescriptor
{
    public string ContentId { get; set; } = string.Empty; // e.g., content:mb:recording:<mbid>
    public List<ContentHash> Hashes { get; set; } = new();
    public List<PerceptualHash> PerceptualHashes { get; set; } = new();
    public long? SizeBytes { get; set; }
    public string? Codec { get; set; }
    public double? Confidence { get; set; } // for fuzzy matches (local-only)
    public DescriptorSignature? Signature { get; set; }
}

