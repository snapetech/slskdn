using System;
using System.Collections.Generic;
using MessagePack;

namespace slskd.Mesh.Dht;

/// <summary>
/// Mesh peer descriptor published to DHT.
/// </summary>
[MessagePackObject]
public class MeshPeerDescriptor
{
    [Key(0)]
    public string PeerId { get; set; } = string.Empty;

    [Key(1)]
    public List<string> Endpoints { get; set; } = new(); // e.g., udp://host:port, quic://host:port

    [Key(2)]
    public string? NatType { get; set; } // unknown|direct|symmetric|restricted

    [Key(3)]
    public bool RelayRequired { get; set; }

    [Key(4)]
    public long TimestampUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // New signed fields for transport endpoints (Phase 1)

    /// <summary>
    /// Sequence number for anti-rollback protection.
    /// </summary>
    [Key(5)]
    public long SequenceNumber { get; set; } = 1;

    /// <summary>
    /// Expiry timestamp for the descriptor (Unix milliseconds).
    /// </summary>
    [Key(6)]
    public long ExpiresAtUnixMs { get; set; }

    /// <summary>
    /// List of transport endpoints for different connectivity methods.
    /// </summary>
    [Key(7)]
    public List<TransportEndpoint> TransportEndpoints { get; set; } = new();

    /// <summary>
    /// SPKI SHA256 pins for QUIC/TLS certificates (control and data planes).
    /// </summary>
    [Key(8)]
    public List<string> CertificatePins { get; set; } = new();

    /// <summary>
    /// Ed25519 public keys for signing control envelopes.
    /// </summary>
    [Key(9)]
    public List<string> ControlSigningKeys { get; set; } = new();

    /// <summary>
    /// Cryptographic signature of the descriptor (base64-encoded).
    /// </summary>
    [Key(10)]
    public string? Signature { get; set; }

    /// <summary>
    /// Initializes a new instance with default expiry (24 hours from now).
    /// </summary>
    public MeshPeerDescriptor()
    {
        ExpiresAtUnixMs = DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Checks if the descriptor is expired.
    /// </summary>
    public bool IsExpired()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > ExpiresAtUnixMs;
    }

    /// <summary>
    /// Gets the data that should be signed for descriptor validation.
    /// Returns a canonical MessagePack byte representation of all signed fields.
    /// </summary>
    public byte[] GetSignableData()
    {
        return CanonicalSerialization.SerializeForSigning(this);
    }
}
