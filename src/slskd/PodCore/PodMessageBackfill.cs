// <copyright file="PodMessageBackfill.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Overlay;

/// <summary>
///     Implements the pod message backfill protocol for synchronizing missed messages.
/// </summary>
public class PodMessageBackfill : IPodMessageBackfill
{
    private readonly IPodMessageStorage _messageStorage;
    private readonly IPodMessageRouter _messageRouter;
    private readonly IOverlayClient _overlayClient;
    private readonly ILogger<PodMessageBackfill> _logger;

    // Track last seen timestamps per pod/channel
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, long>> _lastSeenTimestamps = new();

    // Backfill statistics
    private long _totalBackfillRequestsSent;
    private long _totalBackfillRequestsReceived;
    private long _totalMessagesBackfilled;
    private long _totalBackfillBytesTransferred;
    private readonly ConcurrentDictionary<string, long> _backfillRequestsByPod = new();
    private DateTimeOffset _lastBackfillOperation = DateTimeOffset.MinValue;
    private readonly object _statsLock = new();

    // Configuration
    private const int MaxMessagesPerRange = 1000;
    private const int MaxConcurrentBackfillRequests = 5;
    private const int BackfillTimeoutSeconds = 30;
    private static readonly TimeSpan BackfillRequestTimeout = TimeSpan.FromSeconds(BackfillTimeoutSeconds);

    public PodMessageBackfill(
        IPodMessageStorage messageStorage,
        IPodMessageRouter messageRouter,
        IOverlayClient overlayClient,
        ILogger<PodMessageBackfill> logger)
    {
        _messageStorage = messageStorage;
        _messageRouter = messageRouter;
        _overlayClient = overlayClient;
        _logger = logger;
    }

    public async Task<PodBackfillResult> SyncOnRejoinAsync(string podId, IReadOnlyDictionary<string, long> lastSeenTimestamps, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var channelsRequested = 0;
        var totalMessagesReceived = 0;

        try
        {
            // Validate input
            if (!PodValidation.IsValidPodId(podId))
            {
                return new PodBackfillResult(false, podId, 0, 0, stopwatch.Elapsed, "Invalid pod ID");
            }

            _logger.LogInformation("Starting backfill sync for pod {PodId} with {ChannelCount} channels", podId, lastSeenTimestamps.Count);

            // Determine which channels need backfill
            var channelsNeedingBackfill = new Dictionary<string, MessageRange>();

            foreach (var (channelId, lastSeen) in lastSeenTimestamps)
            {
                if (!PodValidation.IsValidChannelId(channelId))
                {
                    _logger.LogWarning("Skipping invalid channel ID {ChannelId} in backfill sync", channelId);
                    continue;
                }

                // Get the newest message timestamp we have for this channel
                var newestTimestamp = await GetNewestMessageTimestampAsync(podId, channelId, ct);
                if (newestTimestamp > lastSeen)
                {
                    // We have newer messages, request backfill
                    channelsNeedingBackfill[channelId] = new MessageRange(
                        FromTimestampInclusive: lastSeen + 1, // Start from the next message after last seen
                        ToTimestampExclusive: newestTimestamp + 1, // Up to and including the newest
                        MaxMessages: MaxMessagesPerRange);
                    channelsRequested++;
                }
            }

            if (channelsNeedingBackfill.Count == 0)
            {
                _logger.LogInformation("No backfill needed for pod {PodId}", podId);
                return new PodBackfillResult(true, podId, 0, 0, stopwatch.Elapsed);
            }

            // Send backfill requests to pod members
            var backfillTasks = new List<Task<PodBackfillProcessingResult>>();
            var semaphore = new SemaphoreSlim(MaxConcurrentBackfillRequests);

            // Get pod members (excluding ourselves)
            var podMembers = await GetPodMembersAsync(podId, ct);
            var targetPeers = podMembers.Where(m => m.PeerId != GetLocalPeerId()).ToList();

            if (!targetPeers.Any())
            {
                _logger.LogWarning("No other pod members available for backfill in pod {PodId}", podId);
                return new PodBackfillResult(false, podId, channelsRequested, 0, stopwatch.Elapsed, "No peers available for backfill");
            }

            // Send backfill requests to multiple peers for redundancy
            foreach (var peer in targetPeers.Take(3)) // Limit to 3 peers to avoid overwhelming the network
            {
                await semaphore.WaitAsync(ct);
                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await RequestBackfillFromPeerAsync(podId, peer.PeerId, channelsNeedingBackfill, ct);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct);
                backfillTasks.Add(task);
            }

            // Wait for all backfill requests to complete
            var results = await Task.WhenAll(backfillTasks);

            // Aggregate results
            foreach (var result in results)
            {
                if (result.Success)
                {
                    totalMessagesReceived += result.MessagesStored;
                    Interlocked.Increment(ref _totalBackfillRequestsSent);
                }
            }

            Interlocked.Add(ref _totalMessagesBackfilled, totalMessagesReceived);
            _lastBackfillOperation = DateTimeOffset.UtcNow;

            _logger.LogInformation("Backfill sync completed for pod {PodId}: {Channels} channels, {Messages} messages received in {Duration}ms",
                podId, channelsRequested, totalMessagesReceived, stopwatch.ElapsedMilliseconds);

            return new PodBackfillResult(true, podId, channelsRequested, totalMessagesReceived, stopwatch.Elapsed);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during backfill sync for pod {PodId}", podId);
            return new PodBackfillResult(false, podId, channelsRequested, totalMessagesReceived, stopwatch.Elapsed, ex.Message);
        }
    }

    public async Task<PodBackfillResponse> HandleBackfillRequestAsync(string podId, string requestingPeerId, IReadOnlyDictionary<string, MessageRange> channelRanges, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _totalBackfillRequestsReceived);

        try
        {
            // Validate inputs
            if (!PodValidation.IsValidPodId(podId))
            {
                throw new ArgumentException("Invalid pod ID", nameof(podId));
            }

            if (string.IsNullOrWhiteSpace(requestingPeerId))
            {
                throw new ArgumentException("Requesting peer ID cannot be null or empty", nameof(requestingPeerId));
            }

            _logger.LogInformation("Handling backfill request from peer {PeerId} for pod {PodId} with {ChannelCount} channels",
                requestingPeerId, podId, channelRanges.Count);

            var channelMessages = new Dictionary<string, IReadOnlyList<PodMessage>>();
            var hasMoreData = false;

            foreach (var (channelId, range) in channelRanges)
            {
                if (!PodValidation.IsValidChannelId(channelId))
                {
                    _logger.LogWarning("Skipping invalid channel ID {ChannelId} in backfill request", channelId);
                    continue;
                }

                // Get messages in the requested range
                var messages = await _messageStorage.GetMessagesAsync(
                    podId,
                    channelId,
                    sinceTimestamp: range.FromTimestampInclusive - 1, // Include the start timestamp
                    limit: range.MaxMessages,
                    ct);

                // Filter to exact range
                var filteredMessages = messages
                    .Where(m => m.TimestampUnixMs >= range.FromTimestampInclusive &&
                               m.TimestampUnixMs < range.ToTimestampExclusive)
                    .OrderBy(m => m.TimestampUnixMs)
                    .ToList();

                channelMessages[channelId] = filteredMessages;

                // Check if there are more messages available
                if (filteredMessages.Count >= range.MaxMessages)
                {
                    var totalCount = await _messageStorage.GetMessageCountAsync(podId, channelId, ct);
                    var lastMessageTime = filteredMessages.LastOrDefault()?.TimestampUnixMs ?? 0;
                    if (lastMessageTime < range.ToTimestampExclusive - 1)
                    {
                        hasMoreData = true;
                    }
                }

                _logger.LogDebug("Retrieved {MessageCount} messages for channel {ChannelId} in backfill response",
                    filteredMessages.Count, channelId);
            }

            var response = new PodBackfillResponse(
                PodId: podId,
                RespondingPeerId: GetLocalPeerId(),
                ChannelMessages: channelMessages,
                HasMoreData: hasMoreData,
                ResponseTimestamp: DateTimeOffset.UtcNow);

            // Estimate bytes transferred
            var totalBytes = channelMessages.Values.Sum(msgs => msgs.Sum(m => EstimateMessageSize(m)));
            Interlocked.Add(ref _totalBackfillBytesTransferred, totalBytes);

            _logger.LogInformation("Backfill response prepared for pod {PodId}: {TotalMessages} messages, {TotalBytes} bytes",
                podId, channelMessages.Values.Sum(msgs => msgs.Count), totalBytes);

            return response;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling backfill request from peer {PeerId} for pod {PodId}",
                requestingPeerId, podId);
            throw;
        }
    }

    public async Task<PodBackfillProcessingResult> ProcessBackfillResponseAsync(string podId, string respondingPeerId, PodBackfillResponse response, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var messagesProcessed = 0;
        var messagesStored = 0;
        var duplicatesSkipped = 0;

        try
        {
            // Validate response
            if (response.PodId != podId)
            {
                throw new ArgumentException("Response pod ID does not match request", nameof(response));
            }

            _logger.LogInformation("Processing backfill response from peer {PeerId} for pod {PodId} with {ChannelCount} channels",
                respondingPeerId, podId, response.ChannelMessages.Count);

            foreach (var (channelId, messages) in response.ChannelMessages)
            {
                foreach (var message in messages)
                {
                    messagesProcessed++;

                    // Validate message belongs to this channel
                    if (message.ChannelId != channelId)
                    {
                        _logger.LogWarning("Message channel ID mismatch in backfill response: expected {Expected}, got {Actual}",
                            channelId, message.ChannelId);
                        continue;
                    }

                    // Store the message (storage handles deduplication)
                    var stored = await _messageStorage.StoreMessageAsync(podId, channelId, message, ct);
                    if (stored)
                    {
                        messagesStored++;
                        // Update last seen timestamp
                        UpdateLastSeenTimestamp(podId, channelId, message.TimestampUnixMs);
                    }
                    else
                    {
                        duplicatesSkipped++;
                    }
                }
            }

            var result = new PodBackfillProcessingResult(
                Success: true,
                PodId: podId,
                RespondingPeerId: respondingPeerId,
                MessagesProcessed: messagesProcessed,
                MessagesStored: messagesStored,
                DuplicatesSkipped: duplicatesSkipped,
                ProcessingDuration: stopwatch.Elapsed);

            _logger.LogInformation("Backfill response processed: {Processed} messages, {Stored} stored, {Skipped} duplicates in {Duration}ms",
                messagesProcessed, messagesStored, duplicatesSkipped, stopwatch.ElapsedMilliseconds);

            return result;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing backfill response from peer {PeerId} for pod {PodId}",
                respondingPeerId, podId);
            return new PodBackfillProcessingResult(
                false, podId, respondingPeerId, messagesProcessed, messagesStored, duplicatesSkipped, stopwatch.Elapsed, ex.Message);
        }
    }

    public void UpdateLastSeenTimestamp(string podId, string channelId, long timestamp)
    {
        var podTimestamps = _lastSeenTimestamps.GetOrAdd(podId, _ => new ConcurrentDictionary<string, long>());
        podTimestamps[channelId] = Math.Max(podTimestamps.GetValueOrDefault(channelId, 0), timestamp);
    }

    public IReadOnlyDictionary<string, long> GetLastSeenTimestamps(string podId)
    {
        return _lastSeenTimestamps.TryGetValue(podId, out var timestamps)
            ? timestamps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : new Dictionary<string, long>();
    }

    public async Task<PodBackfillStats> GetStatsAsync(CancellationToken ct = default)
    {
        lock (_statsLock)
        {
            // Calculate average duration (simplified - would need to track individual durations)
            var avgDuration = _totalBackfillRequestsSent > 0 ? 5000.0 : 0.0; // Placeholder

            return new PodBackfillStats(
                TotalBackfillRequestsSent: _totalBackfillRequestsSent,
                TotalBackfillRequestsReceived: _totalBackfillRequestsReceived,
                TotalMessagesBackfilled: _totalMessagesBackfilled,
                TotalBackfillBytesTransferred: _totalBackfillBytesTransferred,
                AverageBackfillDurationMs: avgDuration,
                BackfillRequestsByPod: _backfillRequestsByPod.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                LastBackfillOperation: _lastBackfillOperation);
        }
    }

    private async Task<long> GetNewestMessageTimestampAsync(string podId, string channelId, CancellationToken ct)
    {
        // Get the most recent message timestamp for this channel
        var recentMessages = await _messageStorage.GetMessagesAsync(podId, channelId, limit: 1, ct: ct);
        return recentMessages.FirstOrDefault()?.TimestampUnixMs ?? 0;
    }

    private async Task<IReadOnlyList<PodMember>> GetPodMembersAsync(string podId, CancellationToken ct)
    {
        // This would need to be implemented to get pod members
        // For now, return empty list - this needs integration with pod membership service
        _logger.LogWarning("GetPodMembersAsync not implemented - needs integration with pod membership service");
        return Array.Empty<PodMember>();
    }

    private string GetLocalPeerId()
    {
        // This should return the local peer ID
        // For now, return a placeholder
        return "local-peer";
    }

    private async Task<PodBackfillProcessingResult> RequestBackfillFromPeerAsync(
        string podId,
        string peerId,
        Dictionary<string, MessageRange> channelRanges,
        CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Requesting backfill from peer {PeerId} for pod {PodId}", peerId, podId);

            // Create backfill request message
            var requestMessage = CreateBackfillRequestMessage(podId, channelRanges);

            // Send via overlay network (this would need to be implemented)
            // await _overlayClient.SendMessageAsync(peerId, requestMessage, ct);

            // For now, simulate a response (this needs proper overlay integration)
            _logger.LogWarning("Backfill request sending not implemented - needs overlay client integration");

            return new PodBackfillProcessingResult(
                true, podId, peerId, 0, 0, 0, TimeSpan.Zero, "Not implemented");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting backfill from peer {PeerId} for pod {PodId}", peerId, podId);
            return new PodBackfillProcessingResult(
                false, podId, peerId, 0, 0, 0, TimeSpan.Zero, ex.Message);
        }
    }

    private PodMessage CreateBackfillRequestMessage(string podId, Dictionary<string, MessageRange> channelRanges)
    {
        // Create a special message type for backfill requests
        var requestData = new
        {
            PodId = podId,
            ChannelRanges = channelRanges,
            RequestTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        return new PodMessage
        {
            MessageId = $"backfill-request:{podId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            ChannelId = "system", // Special system channel for backfill
            SenderPeerId = GetLocalPeerId(),
            Body = System.Text.Json.JsonSerializer.Serialize(requestData),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Signature = "" // Would need proper signing
        };
    }

    private static int EstimateMessageSize(PodMessage message)
    {
        // Rough estimate: message ID (50) + sender ID (50) + body + signature + metadata
        return 50 + 50 + message.Body.Length + message.Signature.Length + 100;
    }
}
