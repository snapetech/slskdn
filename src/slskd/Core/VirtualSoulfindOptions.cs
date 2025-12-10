namespace slskd.Core;

using slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Virtual Soulfind configuration options.
/// </summary>
public class VirtualSoulfindOptions
{
    /// <summary>
    /// Capture configuration.
    /// </summary>
    public CaptureOptions? Capture { get; set; }
    
    /// <summary>
    /// Privacy configuration.
    /// </summary>
    public PrivacyOptions? Privacy { get; set; }
    
    /// <summary>
    /// Shadow index configuration.
    /// </summary>
    public ShadowIndexOptions? ShadowIndex { get; set; }
}

/// <summary>
/// Capture configuration.
/// </summary>
public class CaptureOptions
{
    /// <summary>
    /// Enable traffic capture and normalization.
    /// </summary>
    public bool Enabled { get; set; } = false;
    
    /// <summary>
    /// Minimum file size to capture (bytes).
    /// </summary>
    public long MinimumFileSizeBytes { get; set; } = 1024 * 1024;  // 1 MB
    
    /// <summary>
    /// Audio file extensions to capture.
    /// </summary>
    public List<string> AudioExtensions { get; set; } = new()
    {
        ".flac", ".mp3", ".m4a", ".aac", ".opus", ".ogg", ".wav"
    };
}

/// <summary>
/// Privacy configuration.
/// </summary>
public class PrivacyOptions
{
    /// <summary>
    /// Anonymization level: None, Pseudonymized, Aggregate.
    /// </summary>
    public string AnonymizationLevel { get; set; } = "Pseudonymized";
    
    /// <summary>
    /// Raw observation retention (days).
    /// </summary>
    public int RawObservationRetentionDays { get; set; } = 7;
    
    /// <summary>
    /// Variant cache retention (days).
    /// </summary>
    public int VariantCacheRetentionDays { get; set; } = 30;
    
    /// <summary>
    /// Persist raw observations to disk (for debugging).
    /// </summary>
    public bool PersistRawObservations { get; set; } = false;
}
