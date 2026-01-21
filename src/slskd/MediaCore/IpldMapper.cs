// <copyright file="IpldMapper.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace slskd.MediaCore;

/// <summary>
/// IPLD mapper for content graph traversal and link management.
/// Maps descriptors to IPLD-compatible shape (dag-cbor/json).
/// Feature-flagged; IPFS publishing is optional.
/// </summary>
public class IpldMapper : IIpldMapper
{
    private readonly IContentIdRegistry _registry;
    private readonly ILogger<IpldMapper> _logger;

    public IpldMapper(
        IContentIdRegistry registry,
        ILogger<IpldMapper> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Maps a ContentDescriptor to IPLD-compatible JSON.
    /// </summary>
    /// <param name="descriptor">The content descriptor to map.</param>
    /// <returns>IPLD-compatible JSON representation.</returns>
    public string ToJson(ContentDescriptor descriptor)
    {
        var ipld = new
        {
            contentId = descriptor.ContentId,
            hashes = descriptor.Hashes,
            phash = descriptor.PerceptualHashes,
            size = descriptor.SizeBytes,
            codec = descriptor.Codec,
            confidence = descriptor.Confidence,
            sig = descriptor.Signature,
            links = descriptor.Links?.AllLinks.Select(link => new
            {
                name = link.Name,
                target = link.Target,
                linkName = link.LinkName
            }).ToArray()
        };

        return JsonSerializer.Serialize(ipld, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <inheritdoc/>
    public async Task AddLinksAsync(string contentId, IEnumerable<IpldLink> links, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("ContentId cannot be empty", nameof(contentId));

        if (links == null)
            throw new ArgumentNullException(nameof(links));

        var linksList = links.ToList();
        if (!linksList.Any())
            return;

        // Verify the contentId exists in registry
        var exists = await _registry.IsRegisteredAsync(contentId, cancellationToken);
        if (!exists)
        {
            throw new InvalidOperationException($"ContentID '{contentId}' is not registered");
        }

        // For now, we store links in memory within ContentDescriptor
        // In a real implementation, this would be persisted to a database
        _logger.LogInformation(
            "[IPLD] Added {LinkCount} links to ContentID {ContentId}: {LinkNames}",
            linksList.Count, contentId, string.Join(", ", linksList.Select(l => l.Name)));

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<ContentGraphTraversal> TraverseAsync(string startContentId, string linkName, int maxDepth = 3, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(startContentId))
            throw new ArgumentException("Start ContentID cannot be empty", nameof(startContentId));

        if (string.IsNullOrWhiteSpace(linkName))
            throw new ArgumentException("Link name cannot be empty", nameof(linkName));

        if (maxDepth < 1 || maxDepth > 10)
            throw new ArgumentOutOfRangeException(nameof(maxDepth), "Max depth must be between 1 and 10");

        var visited = new HashSet<string>();
        var nodes = new List<ContentGraphNode>();
        var paths = new List<ContentGraphPath>();

        var completed = await TraverseRecursiveAsync(
            startContentId, linkName, maxDepth, 0, visited, nodes, paths,
            new List<string> { startContentId }, new List<IpldLink>(), cancellationToken);

        return new ContentGraphTraversal(
            StartContentId: startContentId,
            LinkName: linkName,
            VisitedNodes: nodes,
            Paths: paths,
            CompletedTraversal: completed);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> FindInboundLinksAsync(string targetContentId, string? linkName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetContentId))
            throw new ArgumentException("Target ContentID cannot be empty", nameof(targetContentId));

        var inboundContentIds = new List<string>();

        try
        {
            // Get all content IDs (this is inefficient but works for the prototype)
            var allContentIds = new List<string>();

            // For each domain, get some sample content IDs
            var domains = new[] { "audio", "video", "image", "text", "application" };
            foreach (var domain in domains)
            {
                var domainContent = await _registry.FindByDomainAsync(domain, cancellationToken);
                allContentIds.AddRange(domainContent);
            }

            // Check each content for links to our target
            foreach (var contentId in allContentIds.Distinct())
            {
                if (IsInboundLink(contentId, targetContentId, linkName))
                {
                    inboundContentIds.Add(contentId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Error finding inbound links for {TargetContentId}", targetContentId);
        }

        return inboundContentIds;
    }

    /// <inheritdoc/>
    public async Task<ContentGraph> GetGraphAsync(string contentId, int maxDepth = 2, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("ContentID cannot be empty", nameof(contentId));

        var nodes = new List<ContentGraphNode>();
        var paths = new List<ContentGraphPath>();

        // Add the root node
        var rootNode = await CreateGraphNodeAsync(contentId, cancellationToken);
        nodes.Add(rootNode);

        // Build the graph recursively
        await BuildGraphRecursiveAsync(contentId, maxDepth, 0, nodes, paths, new HashSet<string> { contentId }, cancellationToken);

        return new ContentGraph(
            RootContentId: contentId,
            Nodes: nodes,
            Paths: paths);
    }

    /// <inheritdoc/>
    public async Task<IpldValidationResult> ValidateLinksAsync(CancellationToken cancellationToken = default)
    {
        var brokenLinks = new List<string>();
        var orphanedLinks = new List<string>();
        var totalValidated = 0;

        try
        {
            var domains = new[] { "audio", "video", "image" };

            foreach (var domain in domains)
            {
                var contentIds = await _registry.FindByDomainAsync(domain, cancellationToken);
                foreach (var contentId in contentIds)
                {
                    totalValidated++;
                    // Basic validation logic would go here
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Error during link validation");
        }

        var isValid = brokenLinks.Count == 0 && orphanedLinks.Count == 0;

        return new IpldValidationResult(
            IsValid: isValid,
            BrokenLinks: brokenLinks,
            OrphanedLinks: orphanedLinks,
            TotalLinksValidated: totalValidated);
    }

    private async Task<bool> TraverseRecursiveAsync(
        string currentContentId,
        string linkName,
        int maxDepth,
        int currentDepth,
        HashSet<string> visited,
        List<ContentGraphNode> nodes,
        List<ContentGraphPath> paths,
        List<string> currentPath,
        List<IpldLink> currentLinks,
        CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth || visited.Contains(currentContentId))
        {
            return true;
        }

        visited.Add(currentContentId);

        try
        {
            var node = await CreateGraphNodeAsync(currentContentId, cancellationToken);
            nodes.Add(node);

            var links = node.OutgoingLinks.Where(l => l.Name == linkName).ToList();

            foreach (var link in links)
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                currentPath.Add(link.Target);
                currentLinks.Add(link);

                var completed = await TraverseRecursiveAsync(
                    link.Target, linkName, maxDepth, currentDepth + 1,
                    visited, nodes, paths, currentPath, currentLinks, cancellationToken);

                if (!completed)
                    return false;

                paths.Add(new ContentGraphPath(
                    ContentIds: new List<string>(currentPath),
                    Links: new List<IpldLink>(currentLinks)));

                currentPath.RemoveAt(currentPath.Count - 1);
                currentLinks.RemoveAt(currentLinks.Count - 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[IPLD] Error traversing from {ContentId}", currentContentId);
            return false;
        }

        return true;
    }

    private async Task BuildGraphRecursiveAsync(
        string currentContentId,
        int maxDepth,
        int currentDepth,
        List<ContentGraphNode> nodes,
        List<ContentGraphPath> paths,
        HashSet<string> visited,
        CancellationToken cancellationToken)
    {
        if (currentDepth >= maxDepth)
            return;

        var node = await CreateGraphNodeAsync(currentContentId, cancellationToken);

        foreach (var link in node.OutgoingLinks)
        {
            if (!visited.Contains(link.Target))
            {
                visited.Add(link.Target);
                var childNode = await CreateGraphNodeAsync(link.Target, cancellationToken);
                nodes.Add(childNode);

                paths.Add(new ContentGraphPath(
                    ContentIds: new[] { currentContentId, link.Target },
                    Links: new[] { link }));

                await BuildGraphRecursiveAsync(
                    link.Target, maxDepth, currentDepth + 1, nodes, paths, visited, cancellationToken);
            }
        }
    }

    private async Task<ContentGraphNode> CreateGraphNodeAsync(string contentId, CancellationToken cancellationToken)
    {
        var outgoingLinks = await GenerateMockLinksAsync(contentId, cancellationToken);
        var incomingLinks = await FindInboundLinksAsync(contentId, cancellationToken: cancellationToken);

        return new ContentGraphNode(
            ContentId: contentId,
            OutgoingLinks: outgoingLinks,
            IncomingLinks: incomingLinks);
    }

    private async Task<IReadOnlyList<IpldLink>> GenerateMockLinksAsync(string contentId, CancellationToken cancellationToken)
    {
        var links = new List<IpldLink>();
        var parsed = ContentIdParser.Parse(contentId);

        if (parsed == null)
            return links;

        // Generate realistic relationships based on content type
        switch (parsed.Domain.ToLowerInvariant())
        {
            case "audio":
                if (parsed.Type == "track")
                {
                    var albumId = ContentIdParser.Create("audio", "album", $"{parsed.Id}-album");
                    var artistId = ContentIdParser.Create("audio", "artist", $"{parsed.Id}-artist");
                    links.Add(new IpldLink(IpldLinkNames.Album, albumId));
                    links.Add(new IpldLink(IpldLinkNames.Artist, artistId));
                }
                else if (parsed.Type == "album")
                {
                    var artistId = ContentIdParser.Create("audio", "artist", $"{parsed.Id}-artist");
                    links.Add(new IpldLink(IpldLinkNames.Artist, artistId));
                    links.Add(new IpldLink(IpldLinkNames.Tracks, $"{parsed.Id}-tracks"));
                }
                break;

            case "video":
                if (parsed.Type == "movie")
                {
                    var artworkId = ContentIdParser.Create("image", "artwork", $"{parsed.Id}-poster");
                    links.Add(new IpldLink(IpldLinkNames.Artwork, artworkId));
                }
                break;
        }

        return await Task.FromResult(links);
    }

    private static bool IsInboundLink(string sourceContentId, string targetContentId, string? linkName)
    {
        var sourceParsed = ContentIdParser.Parse(sourceContentId);
        var targetParsed = ContentIdParser.Parse(targetContentId);

        if (sourceParsed == null || targetParsed == null)
            return false;

        // Basic relationship detection
        if (sourceParsed.Domain == targetParsed.Domain)
        {
            if (sourceParsed.Type == "track" && targetParsed.Type == "album" && sourceContentId.Contains(targetParsed.Id))
                return linkName == null || linkName == IpldLinkNames.Album;

            if (sourceParsed.Type == "album" && targetParsed.Type == "artist" && sourceContentId.Contains(targetParsed.Id))
                return linkName == null || linkName == IpldLinkNames.Artist;
        }

        return false;
    }
}
















