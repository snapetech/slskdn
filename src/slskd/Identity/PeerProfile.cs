// <copyright file="PeerProfile.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;

/// <summary>
/// Public profile that any peer can publish. Signed to prevent spoofing.
/// </summary>
public sealed class PeerProfile
{
    /// <summary>Canonical peer ID (derived from public key).</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Public key (raw bytes, base64-encoded).</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Human-friendly display name (e.g., "Keith – Office").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional avatar URL or data URI.</summary>
    public string? Avatar { get; set; }

    /// <summary>Capabilities bitmask: stream, download, mesh search, etc.</summary>
    public int Capabilities { get; set; }

    /// <summary>Endpoints to reach this peer: direct HTTP/QUIC, relay hints, etc.</summary>
    public List<PeerEndpoint> Endpoints { get; set; } = new();

    /// <summary>When this profile was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When this profile expires (short TTL for rotating presence).</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Signature over canonical JSON bytes, signed with peer's private key.</summary>
    public string Signature { get; set; } = string.Empty;
}

/// <summary>Endpoint to reach a peer.</summary>
public sealed class PeerEndpoint
{
    /// <summary>Endpoint type: Direct, Relay, QUIC, etc.</summary>
    public string Type { get; set; } = string.Empty; // "Direct", "Relay", "QUIC"

    /// <summary>Endpoint URL or address.</summary>
    public string Address { get; set; } = string.Empty; // "https://host:port", "relay://relayId/peerId", "quic://..."

    /// <summary>Priority (lower = preferred).</summary>
    public int Priority { get; set; }
}
