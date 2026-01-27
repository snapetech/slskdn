// <copyright file="ShareGrant.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/// <summary>
/// Grants a user or share group access to a collection with a policy. "Share" in the design; named ShareGrant to avoid clash with IShareRepository (files).
/// </summary>
[Table("ShareGrants")]
public class ShareGrant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CollectionId { get; set; }
    public Collection Collection { get; set; } = null!;

    /// <summary>User or ShareGroup.</summary>
    [Required]
    [MaxLength(32)]
    public string AudienceType { get; set; } = AudienceTypes.User;

    /// <summary>Username, ShareGroup.Id.ToString(), or Contact PeerId (Identity & Friends).</summary>
    [Required]
    [MaxLength(256)]
    public string AudienceId { get; set; } = string.Empty;

    /// <summary>Contact PeerId when AudienceType is User and audience is Contact-based (Identity & Friends). Optional.</summary>
    [MaxLength(128)]
    public string? AudiencePeerId { get; set; }

    /// <summary>
    /// Optional: when this grant was discovered from another node, the owner's base URL used to stream/manifest (e.g. "http://host:port").
    /// </summary>
    [MaxLength(512)]
    public string? OwnerEndpoint { get; set; }

    /// <summary>
    /// Optional: share token for remote streaming/manifest access (when OwnerEndpoint is set).
    /// </summary>
    [MaxLength(2048)]
    public string? ShareToken { get; set; }

    // Policy (design: AllowStream, AllowDownload, AllowReshare, ExpiryUtc, MaxConcurrentStreams, MaxBitrateKbps)
    public bool AllowStream { get; set; }
    public bool AllowDownload { get; set; }
    public bool AllowReshare { get; set; }

    public DateTime? ExpiryUtc { get; set; }
    public int MaxConcurrentStreams { get; set; } = 1;
    public int? MaxBitrateKbps { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Audience type for a share grant.</summary>
public static class AudienceTypes
{
    public const string User = "User";
    public const string ShareGroup = "ShareGroup";
}
