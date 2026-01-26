// <copyright file="Collection.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// A curated list of content (ShareList or Playlist). OwnerUserId = Soulseek username in v1.
/// </summary>
public class Collection
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(256)]
    public string OwnerUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Description { get; set; }

    /// <summary>ShareList or Playlist.</summary>
    [Required]
    [MaxLength(32)]
    public string Type { get; set; } = nameof(CollectionType.ShareList);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Collection type.</summary>
public static class CollectionType
{
    public const string ShareList = "ShareList";
    public const string Playlist = "Playlist";
}
