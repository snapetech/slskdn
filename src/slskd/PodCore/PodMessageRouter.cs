// <copyright file="PodMessageRouter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Overlay;

/// <summary>
/// Service for routing pod messages through the decentralized overlay network.
/// </summary>
public class PodMessageRouter : IPodMessageRouter
{
    private readonly ILogger<PodMessageRouter> _logger;
    private readonly IPodService _podService;
    private readonly IOverlayClient _overlayClient;

    // Time-windowed Bloom filter for efficient deduplication
    private readonly TimeWindowedBloomFilter _deduplicationFilter;

    // Routing statistics
    private long _totalMessagesRouted;
    private long _totalRoutingAttempts;
    private long _successfulRoutingCount;
    private long _failedRoutingCount;
    private long _totalRoutingTimeMs;
    private readonly ConcurrentDictionary<string, long> _routingStatsByPod = new();
    private DateTimeOffset _lastRoutingOperation = DateTimeOffset.MinValue;


    // Cleanup configuration
    private static readonly TimeSpan SeenMessageExpiration = TimeSpan.FromHours(24);
    private static readonly int MaxSeenMessagesPerPod = 10000;

    public PodMessageRouter(
        ILogger<PodMessageRouter> logger,
        IPodService podService,
        IOverlayClient overlayClient)
    {
        _logger = logger;
        _podService = podService;
        _overlayClient = overlayClient;

        // Initialize time-windowed Bloom filter for efficient deduplication
        // 24-hour windows, expected 10,000 messages per window, 1% false positive rate
        _deduplicationFilter = new TimeWindowedBloomFilter(10_000, TimeSpan.FromHours(24), 0.01);
    }

    /// <inheritdoc/>
    public async Task<PodMessageRoutingResult> RouteMessageAsync(PodMessage message, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogDebug("[PodMessageRouter] Routing message {MessageId} to pod {PodId}", message.MessageId, message.ChannelId);

            // Extract pod ID from channel (format: "podId:channelId")
            var channelParts = message.ChannelId.Split(':', 2);
            if (channelParts.Length != 2)
            {
                return new PodMessageRoutingResult(
                    Success: false,
                    MessageId: message.MessageId,
                    PodId: message.ChannelId,
                    TargetPeerCount: 0,
                    SuccessfullyRoutedCount: 0,
                    FailedRoutingCount: 0,
                    RoutingDuration: DateTimeOffset.UtcNow - startTime,
                    ErrorMessage: "Invalid channel ID format");
            }

            var podId = channelParts[0];
            var simpleChannelId = channelParts[1];

            // Validate that the channel exists in the pod
            var channel = await _podService.GetChannelAsync(podId, simpleChannelId, cancellationToken);
            if (channel == null)
            {
                _logger.LogWarning("[PodMessageRouter] Attempted to route message to non-existent channel {ChannelId} in pod {PodId}", simpleChannelId, podId);
                return new PodMessageRoutingResult(
                    Success: false,
                    MessageId: message.MessageId,
                    PodId: podId,
                    TargetPeerCount: 0,
                    SuccessfullyRoutedCount: 0,
                    FailedRoutingCount: 0,
                    RoutingDuration: DateTimeOffset.UtcNow - startTime,
                    ErrorMessage: $"Channel {simpleChannelId} does not exist in pod {podId}");
            }

            // Check for duplicate routing using Bloom filter
            if (IsMessageSeen(message.MessageId, podId))
            {
                _logger.LogDebug("[PodMessageRouter] Skipping duplicate message {MessageId} for pod {PodId}", message.MessageId, podId);
                return new PodMessageRoutingResult(
                    Success: true,
                    MessageId: message.MessageId,
                    PodId: podId,
                    TargetPeerCount: 0,
                    SuccessfullyRoutedCount: 0,
                    FailedRoutingCount: 0,
                    RoutingDuration: DateTimeOffset.UtcNow - startTime,
                    ErrorMessage: "Message already routed (duplicate)");
            }

            // Mark message as seen
            RegisterMessageSeen(message.MessageId, podId);

            // Get pod members (excluding sender to avoid echo)
            var members = await _podService.GetMembersAsync(podId, cancellationToken);
            var targetPeerIds = members
                .Where(m => m.PeerId != message.SenderPeerId && !m.IsBanned)
                .Select(m => m.PeerId)
                .ToList();

            if (!targetPeerIds.Any())
            {
                _logger.LogDebug("[PodMessageRouter] No target peers for message {MessageId} in pod {PodId}", message.MessageId, podId);
                return new PodMessageRoutingResult(
                    Success: true,
                    MessageId: message.MessageId,
                    PodId: podId,
                    TargetPeerCount: 0,
                    SuccessfullyRoutedCount: 0,
                    FailedRoutingCount: 0,
                    RoutingDuration: DateTimeOffset.UtcNow - startTime);
            }

            // Route to all target peers
            var routingResult = await RouteMessageToPeersAsync(message, targetPeerIds, cancellationToken);

            // Update statistics
            var duration = DateTimeOffset.UtcNow - startTime;
            Interlocked.Add(ref _totalMessagesRouted, 1);
            Interlocked.Add(ref _totalRoutingAttempts, targetPeerIds.Count);
            Interlocked.Add(ref _successfulRoutingCount, routingResult.SuccessfullyRoutedCount);
            Interlocked.Add(ref _failedRoutingCount, routingResult.FailedRoutingCount);
            Interlocked.Add(ref _totalRoutingTimeMs, (long)duration.TotalMilliseconds);
            _routingStatsByPod.AddOrUpdate(podId, 1, (_, count) => count + 1);
            _lastRoutingOperation = DateTimeOffset.UtcNow;

            _logger.LogDebug(
                "[PodMessageRouter] Routed message {MessageId} to {Success}/{Total} peers in pod {PodId} ({Duration}ms)",
                message.MessageId, routingResult.SuccessfullyRoutedCount, targetPeerIds.Count, podId, duration.TotalMilliseconds);

            return routingResult with
            {
                RoutingDuration = duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouter] Error routing message {MessageId}", message.MessageId);
            return new PodMessageRoutingResult(
                Success: false,
                MessageId: message.MessageId,
                PodId: message.ChannelId.Split(':', 2).FirstOrDefault() ?? "unknown",
                TargetPeerCount: 0,
                SuccessfullyRoutedCount: 0,
                FailedRoutingCount: 0,
                RoutingDuration: DateTimeOffset.UtcNow - startTime,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodMessageRoutingResult> RouteMessageToPeersAsync(PodMessage message, IEnumerable<string> targetPeerIds, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var targetList = targetPeerIds.ToList();
        var successCount = 0;
        var failureCount = 0;
        var failedPeers = new List<string>();

        // Extract pod ID for context
        var podId = message.ChannelId.Split(':', 2).FirstOrDefault() ?? "unknown";

        _logger.LogDebug("[PodMessageRouter] Routing message {MessageId} to {Count} specific peers", message.MessageId, targetList.Count);

        // Route message to each target peer via overlay
        var routingTasks = targetList.Select(async peerId =>
        {
            try
            {
                var routeResult = await RouteMessageToPeerAsync(message, peerId, cancellationToken);
                if (routeResult)
                {
                    Interlocked.Increment(ref successCount);
                }
                else
                {
                    Interlocked.Increment(ref failureCount);
                    lock (failedPeers)
                    {
                        failedPeers.Add(peerId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[PodMessageRouter] Failed to route message {MessageId} to peer {PeerId}", message.MessageId, peerId);
                Interlocked.Increment(ref failureCount);
                lock (failedPeers)
                {
                    failedPeers.Add(peerId);
                }
            }
        });

        await Task.WhenAll(routingTasks);

        var duration = DateTimeOffset.UtcNow - startTime;

        return new PodMessageRoutingResult(
            Success: failureCount == 0,
            MessageId: message.MessageId,
            PodId: podId,
            TargetPeerCount: targetList.Count,
            SuccessfullyRoutedCount: successCount,
            FailedRoutingCount: failureCount,
            RoutingDuration: duration,
            FailedPeerIds: failedPeers);
    }

    /// <inheritdoc/>
    public async Task<PodMessageRoutingStats> GetRoutingStatsAsync(CancellationToken cancellationToken = default)
    {
        // Clean up expired entries
        await CleanupSeenMessagesAsync(cancellationToken);

        var averageRoutingTime = _totalMessagesRouted > 0
            ? (double)_totalRoutingTimeMs / _totalMessagesRouted
            : 0.0;

        // Get Bloom filter statistics
        var (deduplicationItems, fillRatio, estimatedFalsePositiveRate) = _deduplicationFilter.GetStats();

        return new PodMessageRoutingStats(
            TotalMessagesRouted: _totalMessagesRouted,
            TotalRoutingAttempts: _totalRoutingAttempts,
            SuccessfulRoutingCount: _successfulRoutingCount,
            FailedRoutingCount: _failedRoutingCount,
            AverageRoutingTimeMs: averageRoutingTime,
            ActiveDeduplicationItems: deduplicationItems,
            BloomFilterFillRatio: fillRatio,
            EstimatedFalsePositiveRate: estimatedFalsePositiveRate,
            LastRoutingOperation: _lastRoutingOperation,
            RoutingStatsByPod: _routingStatsByPod.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    /// <inheritdoc/>
    public bool RegisterMessageSeen(string messageId, string podId)
    {
        var deduplicationKey = $"{messageId}:{podId}";
        var wasAdded = _deduplicationFilter.Add(deduplicationKey);

        if (wasAdded)
        {
            _logger.LogTrace("[PodMessageRouter] Registered message {MessageId} as seen for pod {PodId}", messageId, podId);
        }

        return wasAdded;
    }

    /// <inheritdoc/>
    public bool IsMessageSeen(string messageId, string podId)
    {
        var deduplicationKey = $"{messageId}:{podId}";
        return _deduplicationFilter.Contains(deduplicationKey);
    }

    /// <inheritdoc/>
    public async Task<PodMessageCleanupResult> CleanupSeenMessagesAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Force cleanup of the time-windowed Bloom filter
        _deduplicationFilter.ForceCleanup();

        var duration = DateTimeOffset.UtcNow - startTime;

        // Get current filter stats
        var (itemCount, fillRatio, estimatedFalsePositiveRate) = _deduplicationFilter.GetStats();

        _logger.LogDebug(
            "[PodMessageRouter] Bloom filter cleanup completed in {Duration}ms. Stats: {Items} items, {FillRatio:P2} fill ratio, {FPRate:P4} estimated false positive rate",
            duration.TotalMilliseconds, itemCount, fillRatio, estimatedFalsePositiveRate);

        return new PodMessageCleanupResult(
            MessagesCleaned: 0, // Time-windowed filter handles cleanup automatically
            MessagesRetained: (int)itemCount,
            CleanupDuration: duration,
            CompletedAt: DateTimeOffset.UtcNow);
    }

    // Helper method to route a message to a single peer via overlay
    private async Task<bool> RouteMessageToPeerAsync(PodMessage message, string peerId, CancellationToken cancellationToken)
    {
        try
        {
            // Create overlay control envelope for pod message
            var messageJson = JsonSerializer.Serialize(message);
            var payload = System.Text.Encoding.UTF8.GetBytes(messageJson);

            var envelope = new ControlEnvelope
            {
                Type = "pod_message",
                Payload = payload,
                PublicKey = string.Empty, // TODO: Add proper public key from pod identity
                Signature = string.Empty, // TODO: Add proper signature
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            // TODO: Implement peer address resolution service
            // For now, create a placeholder IPEndPoint (this would need real peer discovery)
            var placeholderEndpoint = new IPEndPoint(IPAddress.Loopback, 5000);

            _logger.LogInformation("[PodMessageRouter] Attempting to route message {MessageId} to peer {PeerId} at {Endpoint}",
                message.MessageId, peerId, placeholderEndpoint);

            // Send via overlay client
            var sendResult = await _overlayClient.SendAsync(envelope, placeholderEndpoint, cancellationToken);

            if (sendResult)
            {
                _logger.LogTrace("[PodMessageRouter] Successfully routed message {MessageId} to peer {PeerId}", message.MessageId, peerId);
                return true;
            }
            else
            {
                _logger.LogWarning("[PodMessageRouter] Failed to route message {MessageId} to peer {PeerId}", message.MessageId, peerId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodMessageRouter] Error routing message {MessageId} to peer {PeerId}", message.MessageId, peerId);
            return false;
        }
    }
}
