// <copyright file="IPodMessageStorage.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Interface for persistent storage and retrieval of pod messages.
/// </summary>
public interface IPodMessageStorage
{
    /// <summary>
    ///     Stores a message in the specified pod and channel.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="message">The message to store.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the message was stored successfully.</returns>
    Task<bool> StoreMessageAsync(string podId, string channelId, PodMessage message, CancellationToken ct = default);

    /// <summary>
    ///     Retrieves messages from the specified pod and channel.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="sinceTimestamp">Optional timestamp to retrieve messages after.</param>
    /// <param name="limit">Maximum number of messages to retrieve.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The list of messages.</returns>
    Task<IReadOnlyList<PodMessage>> GetMessagesAsync(string podId, string channelId, long? sinceTimestamp = null, int limit = 100, CancellationToken ct = default);

    /// <summary>
    ///     Searches messages in the specified pod using full-text search.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="query">The search query.</param>
    /// <param name="channelId">Optional channel ID to limit search scope.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The list of matching messages.</returns>
    Task<IReadOnlyList<PodMessage>> SearchMessagesAsync(string podId, string query, string channelId = null, int limit = 50, CancellationToken ct = default);

    /// <summary>
    ///     Deletes messages older than the specified timestamp.
    /// </summary>
    /// <param name="olderThanTimestamp">The timestamp before which to delete messages.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of messages deleted.</returns>
    Task<long> DeleteMessagesOlderThanAsync(long olderThanTimestamp, CancellationToken ct = default);

    /// <summary>
    ///     Deletes messages from the specified pod and channel older than the specified timestamp.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="olderThanTimestamp">The timestamp before which to delete messages.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The number of messages deleted.</returns>
    Task<long> DeleteMessagesInChannelOlderThanAsync(string podId, string channelId, long olderThanTimestamp, CancellationToken ct = default);

    /// <summary>
    ///     Gets the total number of messages in the specified pod and channel.
    /// </summary>
    /// <param name="podId">The pod ID.</param>
    /// <param name="channelId">The channel ID.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The message count.</returns>
    Task<long> GetMessageCountAsync(string podId, string channelId, CancellationToken ct = default);

    /// <summary>
    ///     Gets storage statistics.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The storage statistics.</returns>
    Task<PodMessageStorageStats> GetStorageStatsAsync(CancellationToken ct = default);

    /// <summary>
    ///     Rebuilds the full-text search index.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if the rebuild was successful.</returns>
    Task<bool> RebuildSearchIndexAsync(CancellationToken ct = default);

    /// <summary>
    ///     Vacuums the database to reclaim space.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>True if vacuum was successful.</returns>
    Task<bool> VacuumAsync(CancellationToken ct = default);
}

/// <summary>
///     Pod message storage statistics.
/// </summary>
public record PodMessageStorageStats(
    long TotalMessages,
    long TotalSizeBytes,
    DateTimeOffset? OldestMessage,
    DateTimeOffset? NewestMessage,
    Dictionary<string, long> MessagesPerPod,
    Dictionary<string, long> MessagesPerChannel);
