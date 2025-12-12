// <copyright file="MeshPeer.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System;
using slskd.Mesh.Dht;

/// <summary>
/// Represents a verified mesh peer with optional Soulseek username alias.
/// This is the canonical peer representation used throughout the mesh subsystem.
/// </summary>
public sealed record MeshPeer
{
    /// <summary>
    /// Gets the unique mesh identity (derived from public key).
    /// This is the primary key for all mesh operations.
    /// </summary>
    public required MeshPeerId Id { get; init; }

    /// <summary>
    /// Gets the full mesh peer descriptor from DHT/BT.
    /// </summary>
    public required MeshPeerDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets a value indicating whether this peer has passed signature verification.
    /// Only verified peers should be used for mesh operations.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Gets when this peer was last seen (descriptor update or connection).
    /// </summary>
    public DateTimeOffset LastSeen { get; init; }

    /// <summary>
    /// Gets the optional Soulseek username if this peer has logged into Soulseek.
    /// This is an alias/metadata only; all mesh operations key on Id.
    /// </summary>
    public string? SoulseekUsername { get; init; }

    /// <summary>
    /// Gets a value indicating whether this peer has a known Soulseek identity.
    /// </summary>
    public bool HasSoulseekIdentity => !string.IsNullOrWhiteSpace(SoulseekUsername);

    /// <summary>
    /// Gets a display-friendly name for this peer.
    /// Uses Soulseek username if known, otherwise short mesh ID.
    /// </summary>
    public string DisplayName => HasSoulseekIdentity ? SoulseekUsername! : $"peer-{Id.ToShortString()}";
}
