// <copyright file="OverlayMessages.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Messages;

using System;
using System.Collections.Generic;

using System.Text.Json.Serialization;

/// <summary>
/// Magic string for overlay protocol identification.
/// </summary>
public static class OverlayProtocol
{
    /// <summary>
    /// Protocol magic identifier. Must match exactly.
    /// </summary>
    public const string Magic = "SLSKDNM1";
    
    /// <summary>
    /// Current protocol version.
    /// </summary>
    public const int Version = 1;
    
    /// <summary>
    /// Maximum message size in bytes.
    /// </summary>
    public const int MaxMessageSize = 4096;
}

/// <summary>
/// Message types for the overlay protocol.
/// </summary>
public static class OverlayMessageType
{
    public const string Hello = "mesh_hello";
    public const string HelloAck = "mesh_hello_ack";
    public const string Ping = "ping";
    public const string Pong = "pong";
    public const string Disconnect = "disconnect";
}

/// <summary>
/// Soulseek port information exchanged during handshake.
/// </summary>
public sealed class SoulseekPorts
{
    [JsonPropertyName("peer")]
    public int Peer { get; set; }
    
    [JsonPropertyName("file")]
    public int File { get; set; }
}

/// <summary>
/// Base class for overlay messages.
/// </summary>
public abstract class OverlayMessage
{
    [JsonPropertyName("magic")]
    public string Magic { get; set; } = OverlayProtocol.Magic;
    
    [JsonPropertyName("type")]
    public abstract string Type { get; }
    
    [JsonPropertyName("version")]
    public int Version { get; set; } = OverlayProtocol.Version;
}

/// <summary>
/// Initial handshake message sent by connecting client.
/// </summary>
public sealed class MeshHelloMessage : OverlayMessage
{
    [JsonPropertyName("type")]
    public override string Type => OverlayMessageType.Hello;
    
    /// <summary>
    /// Mesh peer ID (required, derived from Ed25519 public key).
    /// This is the canonical identity for mesh operations.
    /// </summary>
    [JsonPropertyName("mesh_peer_id")]
    public string MeshPeerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Ed25519 public key (32 bytes, base64-encoded).
    /// Used for signature verification and deriving mesh peer ID.
    /// </summary>
    [JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }
    
    /// <summary>
    /// Ed25519 signature of the handshake (64 bytes, base64-encoded).
    /// Signs: MeshPeerId + Features + Timestamp
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
    
    /// <summary>
    /// Unix timestamp (seconds) when the handshake was created.
    /// Used for replay protection and signature verification.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
    
    /// <summary>
    /// Soulseek username (optional - may be null for mesh-only peers).
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    
    /// <summary>
    /// List of supported features.
    /// </summary>
    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = new();
    
    /// <summary>
    /// Soulseek listening ports (optional - null for mesh-only peers).
    /// </summary>
    [JsonPropertyName("soulseek_ports")]
    public SoulseekPorts? SoulseekPorts { get; set; }
    
    /// <summary>
    /// Optional nonce for replay attack prevention.
    /// </summary>
    [JsonPropertyName("nonce")]
    public string? Nonce { get; set; }
}

/// <summary>
/// Handshake acknowledgment sent by server/beacon.
/// </summary>
public sealed class MeshHelloAckMessage : OverlayMessage
{
    [JsonPropertyName("type")]
    public override string Type => OverlayMessageType.HelloAck;
    
    /// <summary>
    /// Mesh peer ID (required, derived from Ed25519 public key).
    /// This is the canonical identity for mesh operations.
    /// </summary>
    [JsonPropertyName("mesh_peer_id")]
    public string MeshPeerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Ed25519 public key (32 bytes, base64-encoded).
    /// </summary>
    [JsonPropertyName("public_key")]
    public string? PublicKey { get; set; }
    
    /// <summary>
    /// Ed25519 signature of the handshake (64 bytes, base64-encoded).
    /// </summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
    
    /// <summary>
    /// Soulseek username (optional - may be null for mesh-only peers).
    /// </summary>
    [JsonPropertyName("username")]
    public string? Username { get; set; }
    
    /// <summary>
    /// List of supported features.
    /// </summary>
    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = new();
    
    /// <summary>
    /// Soulseek listening ports (optional - null for mesh-only peers).
    /// </summary>
    [JsonPropertyName("soulseek_ports")]
    public SoulseekPorts? SoulseekPorts { get; set; }
    
    /// <summary>
    /// Echo of client's nonce if provided.
    /// </summary>
    [JsonPropertyName("nonce_echo")]
    public string? NonceEcho { get; set; }
}

/// <summary>
/// Keepalive ping message.
/// </summary>
public sealed class PingMessage : OverlayMessage
{
    [JsonPropertyName("type")]
    public override string Type => OverlayMessageType.Ping;
    
    /// <summary>
    /// Timestamp for RTT calculation.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// Keepalive pong response.
/// </summary>
public sealed class PongMessage : OverlayMessage
{
    [JsonPropertyName("type")]
    public override string Type => OverlayMessageType.Pong;
    
    /// <summary>
    /// Echo of ping timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

/// <summary>
/// Graceful disconnect message.
/// </summary>
public sealed class DisconnectMessage : OverlayMessage
{
    [JsonPropertyName("type")]
    public override string Type => OverlayMessageType.Disconnect;
    
    /// <summary>
    /// Reason for disconnect.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>
/// Known feature identifiers.
/// </summary>
public static class OverlayFeatures
{
    public const string Mesh = "mesh";
    public const string FlacHash = "flac_hash";
    public const string Multipart = "multipart";
    public const string Swarm = "swarm";
    public const string DeltaSync = "delta_sync";
    
    /// <summary>
    /// All features supported by this client.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Mesh,
        FlacHash,
        Multipart,
        Swarm,
        DeltaSync,
    };
}

