namespace slskd.Mesh.Overlay;

/// <summary>
/// Options for QUIC data-plane overlay transfers.
/// </summary>
public class DataOverlayOptions
{
    public bool Enable { get; set; } = true;
    public int ListenPort { get; set; } = 50401;
    public int MaxPayloadBytes { get; set; } = 512 * 1024; // 512 KB per message
    public int MaxConcurrentStreams { get; set; } = 8;
    public int ReceiveBufferBytes { get; set; } = 512 * 1024;
    public int SendBufferBytes { get; set; } = 512 * 1024;

    /// <summary>Path to persist the data plane TLS certificate (PFX).</summary>
    public string TlsCertPath { get; set; } = "mesh-overlay-data.pfx";

    /// <summary>Password for the data plane TLS certificate (optional).</summary>
    public string? TlsCertPassword { get; set; }
}






















