// <copyright file="IAdvancedDiscoveryService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Transfers.MultiSource.Discovery;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Advanced discovery service with enhanced algorithms and content-aware matching.
/// </summary>
public interface IAdvancedDiscoveryService
{
    /// <summary>
    ///     Discovers peers for content using advanced matching algorithms.
    /// </summary>
    Task<List<DiscoveredPeer>> DiscoverPeersForContentAsync(
        ContentDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Scores and ranks discovered peers based on multiple factors.
    /// </summary>
    Task<List<RankedPeer>> RankPeersAsync(
        List<DiscoveredPeer> peers,
        ContentDiscoveryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Finds similar content variants using fuzzy matching.
    /// </summary>
    Task<List<ContentVariant>> FindSimilarVariantsAsync(
        string filename,
        long fileSize,
        string? recordingId = null,
        CancellationToken cancellationToken = default);
}
