// <copyright file="FriendInvite.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;

/// <summary>
/// Self-contained invite for WAN or non-LAN discovery.
/// </summary>
public sealed class FriendInvite
{
    /// <summary>Invite format version.</summary>
    public int InviteVersion { get; set; } = 1;

    /// <summary>The signed PeerProfile of the inviter.</summary>
    public PeerProfile Profile { get; set; } = null!;

    /// <summary>Nonce for replay protection.</summary>
    public string Nonce { get; set; } = string.Empty;

    /// <summary>When this invite expires.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Optional signature over the whole invite (if signed by inviter).</summary>
    public string? InviteSignature { get; set; }
}
