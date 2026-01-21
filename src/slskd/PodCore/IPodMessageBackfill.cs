// <copyright file="IPodMessageBackfill.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Interface for pod message backfill protocol.
///     Handles requesting and providing missed messages when peers rejoin pods.
/// </summary>
public interface IPodMessageBackfill
{
    /// <summary>
    ///     Initiates backfill synchronization when rejoining a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="lastSeenTimestamps">Last seen message timestamps per channel.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The backfill result.</returns>
    Task<PodBackfillResult> SyncOnRejoinAsync(string podId, IReadOnlyDictionary<string, long> lastSeenTimestamps, CancellationToken ct = default);

    /// <summary>
    ///     Handles a backfill request from another peer.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="requestingPeerId">The peer requesting backfill.</param>
    /// <param name="channelRanges">The message ranges being requested per channel.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The backfill response.</returns>
    Task<PodBackfillResponse> HandleBackfillRequestAsync(string podId, string requestingPeerId, IReadOnlyDictionary<string, MessageRange> channelRanges, CancellationToken ct = default);

    /// <summary>
    ///     Processes a backfill response containing missed messages.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="respondingPeerId">The peer that provided the backfill.</param>
    /// <param name="response">The backfill response with messages.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The processing result.</returns>
    Task<PodBackfillProcessingResult> ProcessBackfillResponseAsync(string podId, string respondingPeerId, PodBackfillResponse response, CancellationToken ct = default);

    /// <summary>
    ///     Updates the last seen timestamp for a channel.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="timestamp">The timestamp of the last seen message.</param>
    void UpdateLastSeenTimestamp(string podId, string channelId, long timestamp);

    /// <summary>
    ///     Gets the last seen timestamps for all channels in a pod.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <returns>The last seen timestamps per channel.</returns>
    IReadOnlyDictionary<string, long> GetLastSeenTimestamps(string podId);

    /// <summary>
    ///     Gets backfill statistics.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The backfill statistics.</returns>
    Task<PodBackfillStats> GetStatsAsync(CancellationToken ct = default);
}

/// <summary>
///     Represents a range of messages to request for backfill.
/// </summary>
public record MessageRange(
    long FromTimestampInclusive,
    long ToTimestampExclusive,
    int MaxMessages = 1000);

/// <summary>
///     Result of a backfill synchronization operation.
/// </summary>
public record PodBackfillResult(
    bool Success,
    string PodId,
    int ChannelsRequested,
    int TotalMessagesReceived,
    TimeSpan Duration,
    string? ErrorMessage = null);

/// <summary>
///     Response to a backfill request containing missed messages.
/// </summary>
public record PodBackfillResponse(
    string PodId,
    string RespondingPeerId,
    Dictionary<string, IReadOnlyList<PodMessage>> ChannelMessages,
    bool HasMoreData,
    DateTimeOffset ResponseTimestamp);

/// <summary>
///     Result of processing a backfill response.
/// </summary>
public record PodBackfillProcessingResult(
    bool Success,
    string PodId,
    string RespondingPeerId,
    int MessagesProcessed,
    int MessagesStored,
    int DuplicatesSkipped,
    TimeSpan ProcessingDuration,
    string? ErrorMessage = null);

/// <summary>
///     Pod backfill statistics.
/// </summary>
public record PodBackfillStats(
    long TotalBackfillRequestsSent,
    long TotalBackfillRequestsReceived,
    long TotalMessagesBackfilled,
    long TotalBackfillBytesTransferred,
    double AverageBackfillDurationMs,
    Dictionary<string, long> BackfillRequestsByPod,
    DateTimeOffset LastBackfillOperation);
