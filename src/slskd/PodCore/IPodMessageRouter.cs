// <copyright file="IPodMessageRouter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Service for routing pod messages through the decentralized overlay network.
/// </summary>
public interface IPodMessageRouter
{
    /// <summary>
    /// Routes a pod message to all members of the pod via the overlay network.
    /// </summary>
    /// <param name="message">The pod message to route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The routing result.</returns>
    Task<PodMessageRoutingResult> RouteMessageAsync(PodMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Routes a pod message to a specific subset of pod members.
    /// </summary>
    /// <param name="message">The pod message to route.</param>
    /// <param name="targetPeerIds">The specific peer IDs to route to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The routing result.</returns>
    Task<PodMessageRoutingResult> RouteMessageToPeersAsync(PodMessage message, IEnumerable<string> targetPeerIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current routing statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Routing statistics.</returns>
    Task<PodMessageRoutingStats> GetRoutingStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a message as seen to prevent duplicate routing.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="podId">The pod ID.</param>
    /// <returns>True if the message was newly registered, false if it was already seen.</returns>
    bool RegisterMessageSeen(string messageId, string podId);

    /// <summary>
    /// Checks if a message has already been seen for deduplication.
    /// </summary>
    /// <param name="messageId">The message ID.</param>
    /// <param name="podId">The pod ID.</param>
    /// <returns>True if the message has been seen before.</returns>
    bool IsMessageSeen(string messageId, string podId);

    /// <summary>
    /// Cleans up old seen message entries to prevent memory leaks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cleanup result.</returns>
    Task<PodMessageCleanupResult> CleanupSeenMessagesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a pod message routing operation.
/// </summary>
public record PodMessageRoutingResult(
    bool Success,
    string MessageId,
    string PodId,
    int TargetPeerCount,
    int SuccessfullyRoutedCount,
    int FailedRoutingCount,
    TimeSpan RoutingDuration,
    string? ErrorMessage = null,
    IReadOnlyList<string>? FailedPeerIds = null);

/// <summary>
/// Pod message routing statistics.
/// </summary>
public record PodMessageRoutingStats(
    long TotalMessagesRouted,
    long TotalRoutingAttempts,
    long SuccessfulRoutingCount,
    long FailedRoutingCount,
    double AverageRoutingTimeMs,
    long ActiveDeduplicationItems,
    double BloomFilterFillRatio,
    double EstimatedFalsePositiveRate,
    DateTimeOffset LastRoutingOperation,
    IReadOnlyDictionary<string, long> RoutingStatsByPod);

/// <summary>
/// Result of seen message cleanup operation.
/// </summary>
public record PodMessageCleanupResult(
    int MessagesCleaned,
    int MessagesRetained,
    TimeSpan CleanupDuration,
    DateTimeOffset CompletedAt);
