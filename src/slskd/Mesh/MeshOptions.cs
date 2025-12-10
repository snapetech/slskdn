namespace slskd.Mesh;

/// <summary>
/// Mesh transport preference for routing decisions.
/// </summary>
public enum MeshTransportPreference
{
    DhtFirst,
    Mirrored,
    OverlayFirst
}

/// <summary>
/// Mesh options for transport and discovery.
/// </summary>
public class MeshOptions
{
    public MeshTransportPreference TransportPreference { get; set; } = MeshTransportPreference.DhtFirst;
    public bool EnableOverlay { get; set; } = true;
    public bool EnableDht { get; set; } = true;
    public bool EnableMirrored { get; set; } = false;
}
