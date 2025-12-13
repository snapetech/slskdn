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

    // Deduplication tracking - messageId -> (podId -> seenTimestamp)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, DateTimeOffset>> _seenMessages = new();

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

            // Check for duplicate routing
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

        var activeSeenMessages = _seenMessages.Sum(kvp => kvp.Value.Count);

        return new PodMessageRoutingStats(
            TotalMessagesRouted: _totalMessagesRouted,
            TotalRoutingAttempts: _totalRoutingAttempts,
            SuccessfulRoutingCount: _successfulRoutingCount,
            FailedRoutingCount: _failedRoutingCount,
            AverageRoutingTimeMs: averageRoutingTime,
            ActiveSeenMessages: activeSeenMessages,
            ExpiredSeenMessages: 0, // Would track this in a more sophisticated implementation
            LastRoutingOperation: _lastRoutingOperation,
            RoutingStatsByPod: _routingStatsByPod.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
    }

    /// <inheritdoc/>
    public bool RegisterMessageSeen(string messageId, string podId)
    {
        var podMessages = _seenMessages.GetOrAdd(messageId, _ => new ConcurrentDictionary<string, DateTimeOffset>());
        var wasAdded = podMessages.TryAdd(podId, DateTimeOffset.UtcNow);

        if (wasAdded)
        {
            _logger.LogTrace("[PodMessageRouter] Registered message {MessageId} as seen for pod {PodId}", messageId, podId);
        }

        return wasAdded;
    }

    /// <inheritdoc/>
    public bool IsMessageSeen(string messageId, string podId)
    {
        if (_seenMessages.TryGetValue(messageId, out var podMessages))
        {
            return podMessages.ContainsKey(podId);
        }
        return false;
    }

    /// <inheritdoc/>
    public async Task<PodMessageCleanupResult> CleanupSeenMessagesAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var cleaned = 0;
        var retained = 0;
        var now = DateTimeOffset.UtcNow;

        // Clean up expired messages
        var expiredMessageIds = new List<string>();
        foreach (var (messageId, podMessages) in _seenMessages)
        {
            var expiredPods = new List<string>();
            foreach (var (podId, seenTime) in podMessages)
            {
                if (now - seenTime > SeenMessageExpiration)
                {
                    expiredPods.Add(podId);
                }
            }

            foreach (var podId in expiredPods)
            {
                podMessages.TryRemove(podId, out _);
                cleaned++;
            }

            if (podMessages.IsEmpty)
            {
                expiredMessageIds.Add(messageId);
            }
            else
            {
                retained += podMessages.Count;
            }
        }

        // Remove empty message entries
        foreach (var messageId in expiredMessageIds)
        {
            _seenMessages.TryRemove(messageId, out _);
        }

        // Enforce maximum per-pod limits (simplified - would be more sophisticated in production)
        foreach (var podMessages in _seenMessages.Values)
        {
            if (podMessages.Count > MaxSeenMessagesPerPod)
            {
                var toRemove = podMessages.Count - MaxSeenMessagesPerPod;
                var oldestEntries = podMessages
                    .OrderBy(kvp => kvp.Value)
                    .Take(toRemove)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var podId in oldestEntries)
                {
                    podMessages.TryRemove(podId, out _);
                    cleaned++;
                }
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        _logger.LogDebug(
            "[PodMessageRouter] Cleanup completed: {Cleaned} messages cleaned, {Retained} retained ({Duration}ms)",
            cleaned, retained, duration.TotalMilliseconds);

        return new PodMessageCleanupResult(
            MessagesCleaned: cleaned,
            MessagesRetained: retained,
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
