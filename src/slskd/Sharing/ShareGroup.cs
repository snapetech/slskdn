// <copyright file="ShareGroup.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// A group of users that can be granted access to a collection. For v1, OwnerUserId = Soulseek username.
/// </summary>
public class ShareGroup
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Owner; for v1 = Soulseek username.</summary>
    [Required]
    [MaxLength(256)]
    public string OwnerUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
