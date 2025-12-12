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

    /// <summary>
    /// Default maximum calls per peer per minute (default: 100).
    /// Can be overridden per-service.
    /// </summary>
    public int DefaultMaxCallsPerMinute { get; set; } = 100;

    /// <summary>
    /// Global maximum calls per peer per minute across all services (default: 500).
    /// </summary>
    public int GlobalMaxCallsPerPeer { get; set; } = 500;

    /// <summary>
    /// Per-service rate limits (service name -> max calls per minute).
    /// If not specified, uses DefaultMaxCallsPerMinute.
    /// </summary>
    public Dictionary<string, int> PerServiceRateLimits { get; set; } = new();

    /// <summary>
    /// Per-service timeout overrides in seconds (service name -> timeout seconds).
    /// If not specified, uses default of 30 seconds.
    /// </summary>
    public Dictionary<string, int> PerServiceTimeoutSeconds { get; set; } = new()
    {
        ["shadow-index"] = 60,      // Complex MBID lookups need more time
        ["mesh-stats"] = 5,          // Introspection should be fast
        ["pod-chat"] = 10            // Chat operations should be quick
    };

    /// <summary>
    /// Maximum work units per call (default: 10).
    /// Prevents a single call from triggering excessive downstream work (Soulseek searches, etc.).
    /// </summary>
    public int MaxWorkUnitsPerCall { get; set; } = 10;

    /// <summary>
    /// Maximum work units per peer per minute (default: 50).
    /// Prevents a single peer from monopolizing expensive operations.
    /// </summary>
    public int MaxWorkUnitsPerPeerPerMinute { get; set; } = 50;
}
