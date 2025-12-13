using System;
using System.Collections.Generic;
using MessagePack;

namespace slskd.Mesh.Dht;

/// <summary>
/// Mesh peer descriptor published to DHT.
/// Contains identity, TLS pins, signing keys, and a cryptographic signature.
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

    /// <summary>
    /// Ed25519 identity public key (base64). PeerId MUST be derived from this.
    /// </summary>
    [Key(5)]
    public string IdentityPublicKey { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the SPKI for the control plane TLS certificate (base64).
    /// Used for certificate pinning.
    /// </summary>
    [Key(6)]
    public string TlsControlSpkiSha256 { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the SPKI for the data plane TLS certificate (base64).
    /// Used for certificate pinning.
    /// </summary>
    [Key(7)]
    public string TlsDataSpkiSha256 { get; set; } = string.Empty;

    /// <summary>
    /// List of control signing public keys (base64 Ed25519 keys).
    /// Includes current + previous keys for rotation overlap.
    /// </summary>
    [Key(8)]
    public List<string> ControlSigningPublicKeys { get; set; } = new();

    /// <summary>
    /// Ed25519 signature over canonicalized descriptor fields.
    /// Signed by the identity key.
    /// </summary>
    [Key(9)]
    public string Signature { get; set; } = string.Empty;
}
