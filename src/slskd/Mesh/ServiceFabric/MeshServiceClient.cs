// <copyright file="MeshServiceClient.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Messages;
using slskd.Mesh.Overlay;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Client for making service calls over the mesh overlay.
/// Handles request/response correlation and timeouts.
/// </summary>
public class MeshServiceClient : IMeshServiceClient
{
    private readonly ILogger<MeshServiceClient> _logger;
    private readonly IMeshServiceDirectory _serviceDirectory;
    private readonly IControlSigner _signer;
    private readonly MeshNeighborRegistry? _neighborRegistry;
    private readonly MeshOverlayRequestRouter? _requestRouter;
    private readonly MeshStatsCollector? _statsCollector;

    // Pending calls: correlationId -> TaskCompletionSource
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ServiceReply>> _pendingCalls = new();

    // Per-peer concurrent call tracking: peerId -> call count
    private readonly ConcurrentDictionary<string, int> _perPeerCallCounts = new();

    // Configuration
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(30);
    private readonly int _maxConcurrentCallsPerPeer = 10;
    private readonly int _maxTotalPendingCalls = 1000;

    public MeshServiceClient(
        ILogger<MeshServiceClient> logger,
        IMeshServiceDirectory serviceDirectory,
        IControlSigner signer,
        MeshNeighborRegistry? neighborRegistry = null,
        MeshOverlayRequestRouter? requestRouter = null,
        MeshStatsCollector? statsCollector = null)
    {
        _logger = logger;
        _serviceDirectory = serviceDirectory;
        _signer = signer;
        _neighborRegistry = neighborRegistry;
        _requestRouter = requestRouter;
        _statsCollector = statsCollector;
    }

    public async Task<ServiceReply> CallAsync(
        string targetPeerId,
        ServiceCall call,
        CancellationToken cancellationToken = default)
    {
        var normalizedTargetPeerId = targetPeerId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedTargetPeerId))
            throw new ArgumentException("Target peer ID cannot be empty", nameof(targetPeerId));

        if (call == null)
            throw new ArgumentNullException(nameof(call));

        var normalizedCall = new ServiceCall
        {
            ServiceName = call.ServiceName?.Trim() ?? string.Empty,
            Method = call.Method?.Trim() ?? string.Empty,
            CorrelationId = call.CorrelationId?.Trim() ?? string.Empty,
            Payload = call.Payload
        };

        if (string.IsNullOrWhiteSpace(normalizedCall.ServiceName) ||
            string.IsNullOrWhiteSpace(normalizedCall.Method) ||
            string.IsNullOrWhiteSpace(normalizedCall.CorrelationId))
        {
            throw new ArgumentException("Service name, method, and correlation ID are required", nameof(call));
        }

        // MEDIUM-3 FIX 1: Check global pending call limit
        if (_pendingCalls.Count >= _maxTotalPendingCalls)
        {
            _logger.LogWarning(
                "[ServiceClient] Max total pending calls reached: {Count}",
                _pendingCalls.Count);

            return new ServiceReply
            {
                CorrelationId = normalizedCall.CorrelationId,
                StatusCode = ServiceStatusCodes.RateLimited,
                ErrorMessage = "Too many pending calls (global limit)"
            };
        }

        // MEDIUM-3 FIX 2: Check per-peer concurrent call limit
        if (!TryIncrementPeerCallCount(normalizedTargetPeerId))
        {
            _logger.LogWarning(
                "[ServiceClient] Max concurrent calls to peer reached: {PeerId}, count: {Count}",
                normalizedTargetPeerId,
                _perPeerCallCounts.GetValueOrDefault(normalizedTargetPeerId));

            return new ServiceReply
            {
                CorrelationId = normalizedCall.CorrelationId,
                StatusCode = ServiceStatusCodes.RateLimited,
                ErrorMessage = "Too many concurrent calls to this peer"
            };
        }

        var peerCallReserved = true;

        try
        {
            // Create task completion source for this call
            var tcs = new TaskCompletionSource<ServiceReply>(TaskCreationOptions.RunContinuationsAsynchronously);

            if (!_pendingCalls.TryAdd(normalizedCall.CorrelationId, tcs))
            {
                _logger.LogWarning(
                    "[ServiceClient] Duplicate correlation ID: {CorrelationId}",
                    normalizedCall.CorrelationId);
                return new ServiceReply
                {
                    CorrelationId = normalizedCall.CorrelationId,
                    StatusCode = ServiceStatusCodes.InvalidPayload,
                    ErrorMessage = "Duplicate correlation ID"
                };
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "[ServiceClient] Call cancelled: {Service}.{Method}",
                    normalizedCall.ServiceName, normalizedCall.Method);
                _pendingCalls.TryRemove(normalizedCall.CorrelationId, out _);
                return await Task.FromCanceled<ServiceReply>(cancellationToken);
            }

            var connection = _neighborRegistry?.GetAllConnections()
                .Where(candidate =>
                    candidate.IsOutbound
                    && candidate.IsHandshakeComplete
                    && candidate.IsConnected
                    && string.Equals(candidate.Username, normalizedTargetPeerId, StringComparison.OrdinalIgnoreCase)
                    && candidate.Features.Contains(OverlayFeatures.MeshService, StringComparer.OrdinalIgnoreCase))
                .OrderByDescending(candidate => candidate.ConnectedAt)
                .FirstOrDefault();

            if (connection == null || _requestRouter == null)
            {
                _logger.LogWarning(
                    "[ServiceClient] No outbound mesh service transport to {PeerId} for {Service}.{Method}",
                    normalizedTargetPeerId,
                    normalizedCall.ServiceName,
                    normalizedCall.Method);

                return new ServiceReply
                {
                    CorrelationId = normalizedCall.CorrelationId,
                    StatusCode = ServiceStatusCodes.ServiceUnavailable,
                    ErrorMessage = "Mesh service transport is unavailable."
                };
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_defaultTimeout);

            try
            {
                var replyTask = _requestRouter.WaitForMeshServiceReplyAsync(connection, normalizedCall.CorrelationId, timeoutCts.Token);
                await connection.WriteMessageAsync(new MeshServiceCallMessage
                {
                    CorrelationId = normalizedCall.CorrelationId,
                    ServiceName = normalizedCall.ServiceName,
                    Method = normalizedCall.Method,
                    Payload = normalizedCall.Payload,
                }, timeoutCts.Token).ConfigureAwait(false);

                _statsCollector?.RecordMessageSent();
                var reply = await replyTask.ConfigureAwait(false);
                _statsCollector?.RecordMessageReceived();
                return reply;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return new ServiceReply
                {
                    CorrelationId = normalizedCall.CorrelationId,
                    StatusCode = ServiceStatusCodes.Timeout,
                    ErrorMessage = "Mesh service call timed out"
                };
            }
            finally
            {
                _requestRouter.RemoveMeshServiceReply(connection, normalizedCall.CorrelationId);
            }
        }
        catch (DhtRendezvous.Security.ProtocolViolationException ex)
        {
            _logger.LogDebug(ex, "[ServiceClient] Mesh service frame rejected for {Service}.{Method}", normalizedCall.ServiceName, normalizedCall.Method);
            return new ServiceReply
            {
                CorrelationId = normalizedCall.CorrelationId,
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "Mesh service transport rejected the payload."
            };
        }
        finally
        {
            _pendingCalls.TryRemove(normalizedCall.CorrelationId, out _);
            if (peerCallReserved)
            {
                ReleasePeerCallCount(normalizedTargetPeerId);
            }
        }
    }

    private bool TryIncrementPeerCallCount(string targetPeerId)
    {
        while (true)
        {
            if (_perPeerCallCounts.TryGetValue(targetPeerId, out var currentCount))
            {
                if (currentCount >= _maxConcurrentCallsPerPeer)
                {
                    return false;
                }

                if (_perPeerCallCounts.TryUpdate(targetPeerId, currentCount + 1, currentCount))
                {
                    return true;
                }

                continue;
            }

            if (_perPeerCallCounts.TryAdd(targetPeerId, 1))
            {
                return true;
            }
        }
    }

    private void ReleasePeerCallCount(string targetPeerId)
    {
        while (true)
        {
            if (!_perPeerCallCounts.TryGetValue(targetPeerId, out var currentCount))
            {
                return;
            }

            var nextCount = Math.Max(0, currentCount - 1);
            if (_perPeerCallCounts.TryUpdate(targetPeerId, nextCount, currentCount))
            {
                if (nextCount == 0)
                {
                    _perPeerCallCounts.TryRemove(targetPeerId, out _);
                }

                return;
            }
        }
    }

    public async Task<ServiceReply> CallServiceAsync(
        string serviceName,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        var normalizedServiceName = serviceName?.Trim() ?? string.Empty;
        var normalizedMethod = method?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedServiceName) || string.IsNullOrWhiteSpace(normalizedMethod))
        {
            return new ServiceReply
            {
                CorrelationId = Guid.NewGuid().ToString(),
                StatusCode = ServiceStatusCodes.InvalidPayload,
                ErrorMessage = "Service name and method are required"
            };
        }

        // 1. Discover service
        var descriptors = await _serviceDirectory.FindByNameAsync(normalizedServiceName, cancellationToken);

        if (descriptors.Count == 0)
        {
            _logger.LogWarning(
                "[ServiceClient] No providers found for service: {ServiceName}",
                normalizedServiceName);

            return new ServiceReply
            {
                CorrelationId = Guid.NewGuid().ToString(),
                StatusCode = ServiceStatusCodes.ServiceNotFound,
                ErrorMessage = "No providers available for requested service"
            };
        }

        // 2. Pick the freshest valid descriptor deterministically.
        var descriptor = descriptors
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.OwnerPeerId))
            .OrderByDescending(candidate => candidate.ExpiresAt)
            .ThenBy(candidate => candidate.OwnerPeerId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (descriptor == null)
        {
            return new ServiceReply
            {
                CorrelationId = Guid.NewGuid().ToString(),
                StatusCode = ServiceStatusCodes.ServiceUnavailable,
                ErrorMessage = "No valid providers available for requested service"
            };
        }

        _logger.LogDebug(
            "[ServiceClient] Selected provider for {ServiceName}: {PeerId}",
            normalizedServiceName, descriptor.OwnerPeerId);

        // 3. Create call
        var call = new ServiceCall
        {
            ServiceName = normalizedServiceName,
            Method = normalizedMethod,
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = payload.ToArray()
        };

        // 4. Make call
        return await CallAsync(descriptor.OwnerPeerId, call, cancellationToken);
    }

    /// <summary>
    /// Handle an incoming service reply (called by overlay dispatcher).
    /// </summary>
    public void HandleReply(ServiceReply reply)
    {
        if (reply == null)
        {
            _logger.LogWarning("[ServiceClient] Received null reply");
            return;
        }

        // Track message received
        _statsCollector?.RecordMessageReceived();

        // MEDIUM-3 FIX 4: Validate correlation ID to prevent injection
        if (string.IsNullOrWhiteSpace(reply.CorrelationId))
        {
            _logger.LogWarning("[ServiceClient] Received reply with empty correlation ID");
            return;
        }

        if (_pendingCalls.TryRemove(reply.CorrelationId, out var tcs))
        {
            tcs.TrySetResult(reply);
        }
        else
        {
            _logger.LogDebug(
                "[ServiceClient] Received reply for unknown correlation ID: {CorrelationId}",
                reply.CorrelationId);
        }
    }

    /// <summary>
    /// Get client metrics for monitoring.
    /// </summary>
    public ClientMetrics GetMetrics()
    {
        return new ClientMetrics
        {
            TotalPendingCalls = _pendingCalls.Count,
            PeersWithPendingCalls = _perPeerCallCounts.Count,
            MaxConcurrentCallsPerPeer = _maxConcurrentCallsPerPeer,
            MaxTotalPendingCalls = _maxTotalPendingCalls
        };
    }
}

/// <summary>
/// Client metrics for monitoring.
/// </summary>
public sealed record ClientMetrics
{
    public int TotalPendingCalls { get; init; }
    public int PeersWithPendingCalls { get; init; }
    public int MaxConcurrentCallsPerPeer { get; init; }
    public int MaxTotalPendingCalls { get; init; }
}
