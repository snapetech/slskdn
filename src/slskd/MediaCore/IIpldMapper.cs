// <copyright file="IIpldMapper.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.MediaCore;

/// <summary>
/// IPLD mapper for content graph traversal and link management.
/// </summary>
public interface IIpldMapper
{
    /// <summary>
    /// Adds IPLD links to a content descriptor.
    /// </summary>
    /// <param name="contentId">The ContentID to add links to.</param>
    /// <param name="links">The IPLD links to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddLinksAsync(string contentId, IEnumerable<IpldLink> links, CancellationToken cancellationToken = default);

    /// <summary>
    /// Traverses the content graph following a specific link type.
    /// </summary>
    /// <param name="startContentId">The starting ContentID.</param>
    /// <param name="linkName">The link name to follow.</param>
    /// <param name="maxDepth">Maximum traversal depth.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The traversal result containing visited nodes and paths.</returns>
    Task<ContentGraphTraversal> TraverseAsync(string startContentId, string linkName, int maxDepth = 3, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all content that links to the specified ContentID.
    /// </summary>
    /// <param name="targetContentId">The target ContentID.</param>
    /// <param name="linkName">Optional link name filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of ContentIDs that link to the target.</returns>
    Task<IReadOnlyList<string>> FindInboundLinksAsync(string targetContentId, string? linkName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the content graph for a specific ContentID.
    /// </summary>
    /// <param name="contentId">The ContentID to get the graph for.</param>
    /// <param name="maxDepth">Maximum graph depth.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The content graph structure.</returns>
    Task<ContentGraph> GetGraphAsync(string contentId, int maxDepth = 2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates IPLD link consistency in the registry.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation results with any broken links.</returns>
    Task<IpldValidationResult> ValidateLinksAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of content graph traversal.
/// </summary>
public record ContentGraphTraversal(
    string StartContentId,
    string LinkName,
    IReadOnlyList<ContentGraphNode> VisitedNodes,
    IReadOnlyList<ContentGraphPath> Paths,
    bool CompletedTraversal);

/// <summary>
/// Content graph node with links.
/// </summary>
public record ContentGraphNode(
    string ContentId,
    IReadOnlyList<IpldLink> OutgoingLinks,
    IReadOnlyList<string> IncomingLinks);

/// <summary>
/// Content graph path.
/// </summary>
public record ContentGraphPath(
    IReadOnlyList<string> ContentIds,
    IReadOnlyList<IpldLink> Links);

/// <summary>
/// Content graph structure.
/// </summary>
public record ContentGraph(
    string RootContentId,
    IReadOnlyList<ContentGraphNode> Nodes,
    IReadOnlyList<ContentGraphPath> Paths);

/// <summary>
/// IPLD validation result.
/// </summary>
public record IpldValidationResult(
    bool IsValid,
    IReadOnlyList<string> BrokenLinks,
    IReadOnlyList<string> OrphanedLinks,
    int TotalLinksValidated);
