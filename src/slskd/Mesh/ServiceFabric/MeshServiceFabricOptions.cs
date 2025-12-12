namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Configuration options for the mesh service fabric.
/// </summary>
public class MeshServiceFabricOptions
{
    /// <summary>
    /// Maximum number of service descriptors returned per lookup (default: 20).
    /// </summary>
    public int MaxDescriptorsPerLookup { get; set; } = 20;

    /// <summary>
    /// Maximum size of a single service descriptor in bytes (default: 16KB).
    /// </summary>
    public int MaxDescriptorBytes { get; set; } = 16384;

    /// <summary>
    /// Maximum size of DHT value containing multiple descriptors (default: 256KB).
    /// </summary>
    public int MaxDhtValueBytes { get; set; } = 262144;

    /// <summary>
    /// Maximum allowable timestamp skew in seconds (default: 300 = 5 minutes).
    /// </summary>
    public int MaxTimestampSkewSeconds { get; set; } = 300;

    /// <summary>
    /// Maximum number of metadata entries per descriptor (default: 10).
    /// </summary>
    public int MaxMetadataEntries { get; set; } = 10;

    /// <summary>
    /// Whether to validate DHT signatures (default: true).
    /// </summary>
    public bool ValidateDhtSignatures { get; set; } = true;

    /// <summary>
    /// Service descriptor TTL in seconds (default: 3600 = 1 hour).
    /// </summary>
    public int DescriptorTtlSeconds { get; set; } = 3600;

    /// <summary>
    /// How often to republish local service descriptors in seconds (default: 1800 = 30 minutes).
    /// </summary>
    public int RepublishIntervalSeconds { get; set; } = 1800;
}
