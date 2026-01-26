// <copyright file="ShareGroupMember.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Membership of a user in a share group. 
/// - UserId: Soulseek username (legacy, v1)
/// - PeerId: Contact PeerId (Identity & Friends, v2) - optional, takes precedence when set
/// </summary>
[Table("ShareGroupMembers")]
public class ShareGroupMember
{
    public Guid ShareGroupId { get; set; }
    public ShareGroup ShareGroup { get; set; } = null!;

    /// <summary>Soulseek username (legacy). Required for backward compatibility.</summary>
    [Required]
    [MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Contact PeerId (Identity & Friends). When set, this member is Contact-based rather than Soulseek-based.</summary>
    [MaxLength(128)]
    public string? PeerId { get; set; }
}
