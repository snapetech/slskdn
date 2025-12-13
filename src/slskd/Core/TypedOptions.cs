namespace slskd.Core;

/// <summary>
/// Strongly-typed options containers.
/// </summary>
public class SwarmOptions
{
    public int MaxConcurrentDownloads { get; set; } = 4;
    public int ChunkSizeKb { get; set; } = 256;
}

public class SecurityOptions
{
    public bool EnableConsensus { get; set; } = true;
    public bool EnableHoneypotChecks { get; set; } = true;
}

public class BrainzOptions
{
    public string? ApiBaseUrl { get; set; }
    public int CacheSeconds { get; set; } = 300;
}
















