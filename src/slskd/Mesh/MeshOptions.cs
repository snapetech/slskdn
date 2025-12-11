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

    /// <summary>
    /// Bootstrap nodes for DHT join (host:port).
    /// </summary>
    public List<string> BootstrapNodes { get; set; } = new();

    /// <summary>
    /// Our peer identifier (pseudonymous).
    /// </summary>
    public string SelfPeerId { get; set; } = "peer:mesh:self";

    /// <summary>
    /// Endpoints we advertise (e.g., udp://host:port, quic://host:port).
    /// </summary>
    public List<string> SelfEndpoints { get; set; } = new();

    /// <summary>
    /// Relay endpoints (relay://host:port) we volunteer for others.
    /// </summary>
    public List<string> RelayEndpoints { get; set; } = new();

    /// <summary>
    /// Enable STUN-based NAT detection.
    /// </summary>
    public bool EnableStun { get; set; } = true;

    /// <summary>
    /// STUN servers (host:port).
    /// </summary>
    public List<string> StunServers { get; set; } = new()
    {
        "stun.l.google.com:19302",
        "stun1.l.google.com:19302"
    };
}
