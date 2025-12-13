// <copyright file="MeshPeerDescriptor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Dht;

using System;
using System.Collections.Generic;
using MessagePack;

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
    /// List of TLS SPKI pins for control plane (current + previous for rotation).
    /// Each entry is SHA-256 hash of SPKI (base64).
    /// </summary>
    [Key(6)]
    public List<TlsPin> TlsControlPins { get; set; } = new();

    /// <summary>
    /// List of TLS SPKI pins for data plane (current + previous for rotation).
    /// Each entry is SHA-256 hash of SPKI (base64).
    /// </summary>
    [Key(7)]
    public List<TlsPin> TlsDataPins { get; set; } = new();

    /// <summary>
    /// List of control signing keys (current + previous for rotation).
    /// Maximum 3 keys.
    /// </summary>
    [Key(8)]
    public List<ControlSigningKey> ControlSigningKeys { get; set; } = new();

    /// <summary>
    /// Descriptor schema version. Must be 1 for this format.
    /// </summary>
    [Key(9)]
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Unix timestamp (ms) when this descriptor was issued.
    /// </summary>
    [Key(10)]
    public long IssuedAtUnixMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Unix timestamp (ms) when this descriptor expires.
    /// Default: 7 days from issuance.
    /// </summary>
    [Key(11)]
    public long ExpiresAtUnixMs { get; set; } = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeMilliseconds();

    /// <summary>
    /// Monotonically increasing sequence number for anti-rollback.
    /// Each new descriptor MUST have seq > previous.
    /// </summary>
    [Key(12)]
    public ulong DescriptorSeq { get; set; }

    /// <summary>
    /// Ed25519 signature over canonicalized descriptor fields.
    /// Signed by the identity key.
    /// Signature is computed over DescriptorToSign (excludes this field).
    /// </summary>
    [Key(13)]
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Converts this descriptor to the canonical signing payload.
    /// </summary>
    public DescriptorToSign ToSigningPayload() => new()
    {
        PeerId = PeerId,
        Endpoints = Endpoints,
        NatType = NatType,
        RelayRequired = RelayRequired,
        TimestampUnixMs = TimestampUnixMs,
        IdentityPublicKey = IdentityPublicKey,
        TlsControlPins = TlsControlPins,
        TlsDataPins = TlsDataPins,
        ControlSigningKeys = ControlSigningKeys,
        SchemaVersion = SchemaVersion,
        IssuedAtUnixMs = IssuedAtUnixMs,
        ExpiresAtUnixMs = ExpiresAtUnixMs,
        DescriptorSeq = DescriptorSeq,
    };
}

/// <summary>
/// Canonical signing payload for MeshPeerDescriptor.
/// MessagePack serialization of this DTO (with fixed Key ordering) is what gets signed.
/// </summary>
[MessagePackObject]
public class DescriptorToSign
{
    [Key(0)] public string PeerId { get; set; } = string.Empty;
    [Key(1)] public List<string> Endpoints { get; set; } = new();
    [Key(2)] public string? NatType { get; set; }
    [Key(3)] public bool RelayRequired { get; set; }
    [Key(4)] public long TimestampUnixMs { get; set; }
    [Key(5)] public string IdentityPublicKey { get; set; } = string.Empty;
    [Key(6)] public List<TlsPin> TlsControlPins { get; set; } = new();
    [Key(7)] public List<TlsPin> TlsDataPins { get; set; } = new();
    [Key(8)] public List<ControlSigningKey> ControlSigningKeys { get; set; } = new();
    [Key(9)] public int SchemaVersion { get; set; } = 1;
    [Key(10)] public long IssuedAtUnixMs { get; set; }
    [Key(11)] public long ExpiresAtUnixMs { get; set; }
    [Key(12)] public ulong DescriptorSeq { get; set; }
}

/// <summary>
/// TLS certificate pin with validity period.
/// </summary>
[MessagePackObject]
public class TlsPin
{
    /// <summary>
    /// SHA-256 hash of SPKI (base64).
    /// </summary>
    [Key(0)]
    public string SpkiSha256 { get; set; } = string.Empty;

    /// <summary>
    /// Unix timestamp (ms) when this pin becomes valid.
    /// </summary>
    [Key(1)]
    public long ValidFromUnixMs { get; set; }

    /// <summary>
    /// Unix timestamp (ms) when this pin expires.
    /// </summary>
    [Key(2)]
    public long ValidToUnixMs { get; set; }
}

/// <summary>
/// Control signing public key with validity period.
/// </summary>
[MessagePackObject]
public class ControlSigningKey
{
    /// <summary>
    /// Ed25519 public key (base64).
    /// </summary>
    [Key(0)]
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>
    /// Unix timestamp (ms) when this key becomes valid.
    /// </summary>
    [Key(1)]
    public long ValidFromUnixMs { get; set; }

    /// <summary>
    /// Unix timestamp (ms) when this key expires.
    /// </summary>
    [Key(2)]
    public long ValidToUnixMs { get; set; }
}
