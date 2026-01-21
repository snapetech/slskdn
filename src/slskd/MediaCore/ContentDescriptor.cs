// <copyright file="ContentDescriptor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore;

/// <summary>
/// Hash descriptor entry.
/// </summary>
public record ContentHash(string Algorithm, string Hex);

/// <summary>
/// Perceptual hash entry (optional).
/// </summary>
public record PerceptualHash(string Algorithm, string Hex, ulong? NumericHash = null);

/// <summary>
/// Signature envelope for descriptors.
/// </summary>
public record DescriptorSignature(string PublicKey, string Signature, long TimestampUnixMs);

/// <summary>
/// Media descriptor for mesh publishing.
/// </summary>
public class ContentDescriptor
{
    public string ContentId { get; set; } = string.Empty; // e.g., content:mb:recording:<mbid>
    public List<ContentHash> Hashes { get; set; } = new();
    public List<PerceptualHash> PerceptualHashes { get; set; } = new();
    public long? SizeBytes { get; set; }
    public string? Codec { get; set; }
    public double? Confidence { get; set; } // for fuzzy matches (local-only)
    public DescriptorSignature? Signature { get; set; }

    /// <summary>
    /// Gets or sets whether this content is advertisable (can be shared publicly).
    /// </summary>
    public bool IsAdvertisable { get; set; } = true;

    /// <summary>
    /// IPLD links to related content (parent, children, metadata, etc.).
    /// </summary>
    public IpldLinkCollection? Links { get; set; }

    /// <summary>
    /// Adds an IPLD link to the descriptor.
    /// </summary>
    /// <param name="name">The link name (e.g., "parent", "album").</param>
    /// <param name="target">The target ContentID.</param>
    /// <param name="linkName">Optional link name for disambiguation.</param>
    public void AddLink(string name, string target, string? linkName = null)
    {
        Links ??= new IpldLinkCollection();
        Links.AddLink(new IpldLink(name, target, linkName));
    }

    /// <summary>
    /// Gets all links with a specific name.
    /// </summary>
    /// <param name="name">The link name.</param>
    /// <returns>List of links with the specified name.</returns>
    public IReadOnlyList<IpldLink> GetLinks(string name)
    {
        return Links?.GetLinksByName(name) ?? Array.Empty<IpldLink>();
    }

    /// <summary>
    /// Gets the first link with a specific name.
    /// </summary>
    /// <param name="name">The link name.</param>
    /// <returns>The first link with the specified name, or null if not found.</returns>
    public IpldLink? GetLink(string name)
    {
        return GetLinks(name).FirstOrDefault();
    }

    /// <summary>
    /// Gets all links pointing to a specific target.
    /// </summary>
    /// <param name="target">The target ContentID.</param>
    /// <returns>List of links pointing to the target.</returns>
    public IReadOnlyList<IpldLink> GetLinksTo(string target)
    {
        return Links?.GetLinksByTarget(target) ?? Array.Empty<IpldLink>();
    }
}
