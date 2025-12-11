namespace slskd.MediaCore;

/// <summary>
/// MediaCore feature flags and limits.
/// </summary>
public class MediaCoreOptions
{
    public bool EnableIpldExport { get; set; } = false;
    public bool EnablePerceptualMatching { get; set; } = false;
    public bool PublishToIpfs { get; set; } = false; // guarded, default off
    public int MaxDescriptorBytes { get; set; } = 10 * 1024;
    public int MaxTtlMinutes { get; set; } = 60;
}

