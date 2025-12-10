namespace slskd.Mesh.Overlay;

/// <summary>
/// Overlay transport options (UDP control channel).
/// </summary>
public class OverlayOptions
{
    public bool Enable { get; set; } = true;
    public int ListenPort { get; set; } = 50400;
    public int ReceiveBufferBytes { get; set; } = 128 * 1024;
    public int SendBufferBytes { get; set; } = 128 * 1024;
    public int MaxDatagramBytes { get; set; } = 8 * 1024; // control envelopes only
}
