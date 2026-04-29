// <copyright file="OverlayOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Overlay;

/// <summary>
/// Overlay transport options (UDP control channel).
/// </summary>
public class OverlayOptions
{
    public bool Enable { get; set; } = true;
    public int ListenPort { get; set; } = 50400;
    public bool EnableQuic { get; set; } = false;

    // QUIC overlay server needs its own UDP port: QUIC takes exclusive ownership of its
    // UDP socket, so it cannot share ListenPort with the legacy UdpOverlayServer. Both
    // can run concurrently when QUIC is explicitly enabled.
    public int QuicListenPort { get; set; } = 50402;

    public int ReceiveBufferBytes { get; set; } = 128 * 1024;
    public int SendBufferBytes { get; set; } = 128 * 1024;
    public int MaxDatagramBytes { get; set; } = 8 * 1024; // control envelopes only

    /// <summary>Path to persist the overlay Ed25519 key (base64-encoded).</summary>
    public string KeyPath { get; set; } = "mesh-overlay.key";

    /// <summary>Rotate key after N days (0 = never).</summary>
    public int RotateDays { get; set; } = 30;
}
