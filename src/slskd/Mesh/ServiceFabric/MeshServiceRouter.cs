using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Common.Security;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Routes incoming service calls to registered IMeshService implementations.
/// Integrates with security/violation tracking and enforces limits.
/// </summary>
public class MeshServiceRouter
{
    private readonly ILogger<MeshServiceRouter> _logger;
    private readonly ViolationTracker? _violationTracker;
    private readonly MeshServiceFabricOptions _options;
    private readonly ConcurrentDictionary<string, IMeshService> _services = new();
    
    // Per-peer rate limiting: peerId -> (callCount, windowStart)
    private readonly ConcurrentDictionary<string, (int count, DateTimeOffset windowStart)> _perPeerCallCounts = new();
    
    public MeshServiceRouter(
        ILogger<MeshServiceRouter> logger,
        Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions> options,
        ViolationTracker? violationTracker = null)
    {
        _logger = logger;
        _violationTracker = violationTracker;
        _options = options.Value;
    }

    /// <summary>
    /// Register a service for routing.
    /// Thread-safe.
    /// </summary>
    public void RegisterService(IMeshService service)
    {
        if (service == null)
            throw new ArgumentNullException(nameof(service));

        if (string.IsNullOrWhiteSpace(service.ServiceName))
            throw new ArgumentException("Service name cannot be empty", nameof(service));

        if (_services.TryAdd(service.ServiceName, service))
        {
            _logger.LogInformation(
                "[ServiceRouter] Registered service: {ServiceName}",
                service.ServiceName);
        }
        else
        {
            _logger.LogWarning(
                "[ServiceRouter] Service already registered: {ServiceName}",
                service.ServiceName);
        }
    }

    /// <summary>
    /// Unregister a service.
    /// </summary>
    public bool UnregisterService(string serviceName)
    {
        if (_services.TryRemove(serviceName, out var service))
        {
            _logger.LogInformation(
                "[ServiceRouter] Unregistered service: {ServiceName}",
                serviceName);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Route an incoming service call to the appropriate handler.
    /// </summary>
    public async Task<ServiceReply> RouteAsync(
        ServiceCall call,
        string remotePeerId,
        string? remotePublicKey = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Basic validation
            if (call == null)
            {
                _logger.LogWarning("[ServiceRouter] Null service call received");
                return CreateErrorReply(string.Empty, ServiceStatusCodes.InvalidPayload, "Null call");
            }

            if (string.IsNullOrWhiteSpace(call.ServiceName))
            {
                _logger.LogWarning("[ServiceRouter] Empty service name in call");
                return CreateErrorReply(call.CorrelationId, ServiceStatusCodes.InvalidPayload, "Empty service name");
            }

            // 2. Check payload size limit
            if (call.Payload.Length > _options.MaxDescriptorBytes)
            {
                _logger.LogWarning(
                    "[ServiceRouter] Payload too large from {PeerId}: {Size} > {Max}",
                    remotePeerId, call.Payload.Length, _options.MaxDescriptorBytes);
                
                RecordViolation(remotePeerId, ViolationType.RateLimitExceeded, "Payload too large");
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.PayloadTooLarge,
                    $"Payload exceeds {_options.MaxDescriptorBytes} bytes");
            }

            // 3. Check per-peer rate limit
            if (!CheckRateLimit(remotePeerId))
            {
                _logger.LogWarning(
                    "[ServiceRouter] Rate limit exceeded for peer: {PeerId}",
                    remotePeerId);
                
                RecordViolation(remotePeerId, ViolationType.RateLimitExceeded, "Service call rate limit");
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.RateLimited,
                    "Too many requests");
            }

            // 4. Find service
            if (!_services.TryGetValue(call.ServiceName, out var service))
            {
                _logger.LogDebug(
                    "[ServiceRouter] Service not found: {ServiceName}",
                    call.ServiceName);
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.ServiceNotFound,
                    $"Service '{call.ServiceName}' not found");
            }

            // 5. Create context
            var context = new MeshServiceContext
            {
                RemotePeerId = remotePeerId,
                RemotePublicKey = remotePublicKey,
                ReceivedAt = DateTimeOffset.UtcNow,
                TraceId = call.CorrelationId,
                ViolationTracker = _violationTracker,
                Logger = _logger
            };

            // 6. Invoke service handler with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // TODO: Make configurable

            ServiceReply reply;
            try
            {
                reply = await service.HandleCallAsync(call, context, cts.Token);
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "[ServiceRouter] Service call timed out: {ServiceName}.{Method}",
                    call.ServiceName, call.Method);
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.Timeout,
                    "Service call timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[ServiceRouter] Service handler threw exception: {ServiceName}.{Method}",
                    call.ServiceName, call.Method);
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.UnknownError,
                    "Internal service error");
            }

            // 7. Log and return
            _logger.LogDebug(
                "[ServiceRouter] Call completed: {ServiceName}.{Method} -> {Status}",
                call.ServiceName, call.Method, reply.StatusCode);

            return reply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ServiceRouter] Unexpected error routing call");
            return CreateErrorReply(
                call?.CorrelationId ?? string.Empty,
                ServiceStatusCodes.UnknownError,
                "Router error");
        }
    }

    /// <summary>
    /// Check per-peer rate limit using sliding window.
    /// </summary>
    private bool CheckRateLimit(string peerId)
    {
        var now = DateTimeOffset.UtcNow;
        var windowDuration = TimeSpan.FromMinutes(1);
        var maxCallsPerWindow = 100; // TODO: Make configurable

        var (count, windowStart) = _perPeerCallCounts.GetOrAdd(peerId, _ => (0, now));

        // Reset window if expired
        if (now - windowStart > windowDuration)
        {
            _perPeerCallCounts[peerId] = (1, now);
            return true;
        }

        // Check limit
        if (count >= maxCallsPerWindow)
        {
            return false;
        }

        // Increment count
        _perPeerCallCounts[peerId] = (count + 1, windowStart);
        return true;
    }

    /// <summary>
    /// Record a violation via the violation tracker.
    /// </summary>
    private void RecordViolation(string peerId, ViolationType type, string details)
    {
        if (_violationTracker == null)
            return;

        try
        {
            // Use username violation tracking with peer ID
            _violationTracker.RecordUsernameViolation(peerId, type, details);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ServiceRouter] Failed to record violation for {PeerId}", peerId);
        }
    }

    /// <summary>
    /// Create a standard error reply.
    /// </summary>
    private static ServiceReply CreateErrorReply(string correlationId, int statusCode, string errorMessage)
    {
        return new ServiceReply
        {
            CorrelationId = correlationId,
            StatusCode = statusCode,
            Payload = Array.Empty<byte>(),
            ErrorMessage = errorMessage
        };
    }

    /// <summary>
    /// Get statistics about registered services and call counts.
    /// </summary>
    public RouterStats GetStats()
    {
        return new RouterStats
        {
            RegisteredServiceCount = _services.Count,
            TrackedPeerCount = _perPeerCallCounts.Count
        };
    }
}

/// <summary>
/// Statistics for MeshServiceRouter.
/// </summary>
public sealed record RouterStats
{
    public int RegisteredServiceCount { get; init; }
    public int TrackedPeerCount { get; init; }
}
