// <copyright file="IpldLink.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace slskd.MediaCore;

/// <summary>
/// IPLD link representation for content-addressed linking.
/// </summary>
public record IpldLink(string Name, string Target, string? LinkName = null)
{
    /// <summary>
    /// Gets the full IPLD link path (name/target).
    /// </summary>
    public string Path => $"{Name}/{Target}";
}

/// <summary>
/// IPLD link collection for content relationships.
/// </summary>
public class IpldLinkCollection
{
    private readonly Dictionary<string, List<IpldLink>> _linksByName = new();
    private readonly Dictionary<string, List<IpldLink>> _linksByTarget = new();

    /// <summary>
    /// Adds a link to the collection.
    /// </summary>
    /// <param name="link">The IPLD link to add.</param>
    public void AddLink(IpldLink link)
    {
        // Index by name
        if (!_linksByName.ContainsKey(link.Name))
        {
            _linksByName[link.Name] = new List<IpldLink>();
        }
        _linksByName[link.Name].Add(link);

        // Index by target
        if (!_linksByTarget.ContainsKey(link.Target))
        {
            _linksByTarget[link.Target] = new List<IpldLink>();
        }
        _linksByTarget[link.Target].Add(link);
    }

    /// <summary>
    /// Gets all links with a specific name.
    /// </summary>
    /// <param name="name">The link name.</param>
    /// <returns>List of links with the specified name.</returns>
    public IReadOnlyList<IpldLink> GetLinksByName(string name)
    {
        return _linksByName.TryGetValue(name, out var links) ? links : Array.Empty<IpldLink>();
    }

    /// <summary>
    /// Gets all links pointing to a specific target.
    /// </summary>
    /// <param name="target">The target ContentID.</param>
    /// <returns>List of links pointing to the target.</returns>
    public IReadOnlyList<IpldLink> GetLinksByTarget(string target)
    {
        return _linksByTarget.TryGetValue(target, out var links) ? links : Array.Empty<IpldLink>();
    }

    /// <summary>
    /// Gets all link names in the collection.
    /// </summary>
    public IReadOnlyList<string> LinkNames => _linksByName.Keys.ToArray();

    /// <summary>
    /// Gets all targets in the collection.
    /// </summary>
    public IReadOnlyList<string> Targets => _linksByTarget.Keys.ToArray();

    /// <summary>
    /// Gets all links in the collection.
    /// </summary>
    public IEnumerable<IpldLink> AllLinks => _linksByName.Values.SelectMany(links => links);

    /// <summary>
    /// Clears all links from the collection.
    /// </summary>
    public void Clear()
    {
        _linksByName.Clear();
        _linksByTarget.Clear();
    }
}

/// <summary>
/// Standard IPLD link names for media content relationships.
/// </summary>
public static class IpldLinkNames
{
    // Content hierarchy
    public const string Parent = "parent";
    public const string Children = "children";
    public const string Parts = "parts";

    // Media relationships
    public const string Album = "album";
    public const string Artist = "artist";
    public const string Tracks = "tracks";
    public const string Artwork = "artwork";
    public const string Lyrics = "lyrics";

    // Content variants
    public const string Versions = "versions";
    public const string Formats = "formats";
    public const string Derivatives = "derivatives";

    // Metadata relationships
    public const string Metadata = "metadata";
    public const string Sources = "sources";
    public const string References = "references";
}
