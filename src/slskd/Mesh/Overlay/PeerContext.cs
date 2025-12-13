// <copyright file="PeerContext.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Overlay;

using System.Collections.Generic;
using System.Net;

/// <summary>
/// Context about a peer connection for control-plane message handling.
/// </summary>
public record PeerContext
{
    /// <summary>
    /// Gets the mesh peer ID (derived from identity public key).
    /// </summary>
    public required string PeerId { get; init; }

    /// <summary>
    /// Gets the remote endpoint (IP + port).
    /// </summary>
    public required IPEndPoint RemoteEndPoint { get; init; }

    /// <summary>
    /// Gets the transport protocol used ("udp" or "quic").
    /// </summary>
    public required string Transport { get; init; }

    /// <summary>
    /// Gets the allowed control signing public keys for this peer (for signature verification).
    /// These should be fetched from the peer's signed descriptor.
    /// </summary>
    public required IReadOnlyList<byte[]> AllowedControlSigningKeys { get; init; }
}

