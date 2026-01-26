// <copyright file="Contact.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Local-only contact list with petnames (like Signal). You talk to a verified key; the name is your label.
/// </summary>
[Table("Contacts")]
public class Contact
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Canonical peer ID (foreign key to PeerProfile).</summary>
    [Required]
    [MaxLength(128)]
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Local-only nickname (e.g., "Marisa", "PirateBuddy").</summary>
    [MaxLength(128)]
    public string Nickname { get; set; } = string.Empty;

    /// <summary>Whether signature was verified and key is pinned.</summary>
    public bool Verified { get; set; }

    /// <summary>Last time we saw this peer online.</summary>
    public DateTimeOffset? LastSeen { get; set; }

    /// <summary>Cached endpoints from last profile fetch (JSON).</summary>
    public string? CachedEndpointsJson { get; set; }

    /// <summary>When this contact was added.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
