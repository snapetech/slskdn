// <copyright file="CollectionItem.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// A content reference in a collection. ContentId maps to IShareRepository content_items / library.
/// </summary>
[Table("CollectionItems")]
public class CollectionItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    /// <summary>Order within the collection (0-based).</summary>
    public int Ordinal { get; set; }

    [Required]
    [MaxLength(512)]
    public string ContentId { get; set; } = string.Empty;

    /// <summary>Optional: Music, TV, Movie, Book, Other.</summary>
    [MaxLength(32)]
    public string? MediaKind { get; set; }

    [MaxLength(128)]
    public string? ContentHash { get; set; }
}
