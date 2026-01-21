// <copyright file="MeshServiceRouter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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
    private readonly SecurityEventLogger? _securityLogger;
    private readonly PeerWorkBudgetTracker _workBudgetTracker;
    private readonly MeshServiceFabricOptions _options;
    private readonly ConcurrentDictionary<string, IMeshService> _services = new();
    
    // Per-peer rate limiting: peerId -> (callCount, windowStart)
    private readonly ConcurrentDictionary<string, (int count, DateTimeOffset windowStart)> _perPeerCallCounts = new();
    
    // Global per-peer rate limiting across all services: peerId -> (callCount, windowStart)
    private readonly ConcurrentDictionary<string, (int count, DateTimeOffset windowStart)> _globalPeerCallCounts = new();
    
    // Circuit breaker per service: serviceName -> health tracker
    private readonly ConcurrentDictionary<string, ServiceHealthTracker> _serviceHealth = new();
    
    public MeshServiceRouter(
        ILogger<MeshServiceRouter> logger,
        Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions> options,
        ViolationTracker? violationTracker = null,
        SecurityEventLogger? securityLogger = null,
        PeerWorkBudgetTracker? workBudgetTracker = null)
    {
        _logger = logger;
        _violationTracker = violationTracker;
        _securityLogger = securityLogger;
        _options = options.Value;
        
        // Create work budget tracker with embedded options
        _workBudgetTracker = workBudgetTracker ?? new PeerWorkBudgetTracker(
            new WorkBudgetOptions
            {
                MaxWorkUnitsPerCall = _options.MaxWorkUnitsPerCall,
                MaxWorkUnitsPerPeerPerMinute = _options.MaxWorkUnitsPerPeerPerMinute,
                Enabled = true
            });
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

            // 3. Check global per-peer rate limit (across all services)
            if (!CheckGlobalRateLimit(remotePeerId))
            {
                _logger.LogWarning(
                    "[ServiceRouter] Global rate limit exceeded for peer: {PeerId}",
                    remotePeerId);
                
                RecordViolation(remotePeerId, ViolationType.RateLimitExceeded, "Global service call rate limit");
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.RateLimited,
                    "Too many requests (global limit)");
            }

            // 4. Check per-service rate limit
            if (!CheckServiceRateLimit(remotePeerId, call.ServiceName))
            {
                var key = $"{remotePeerId}:{call.ServiceName}";
                var (count, _) = _perPeerCallCounts.GetValueOrDefault(key);
                var serviceLimit = _options.PerServiceRateLimits.GetValueOrDefault(
                    call.ServiceName, 
                    _options.DefaultMaxCallsPerMinute);
                
                _securityLogger?.LogRateLimitViolation(
                    remotePeerId,
                    call.ServiceName,
                    "PerService",
                    count,
                    serviceLimit);
                
                _logger.LogWarning(
                    "[ServiceRouter] Service rate limit exceeded for peer: {PeerId}, service: {ServiceName}",
                    remotePeerId, call.ServiceName);
                
                RecordViolation(remotePeerId, ViolationType.RateLimitExceeded, $"Service call rate limit for {call.ServiceName}");
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.RateLimited,
                    "Too many requests for this service");
            }

            // 5. Find service
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

            // 5a. Check circuit breaker
            var health = _serviceHealth.GetOrAdd(call.ServiceName, _ => new ServiceHealthTracker());
            if (health.IsCircuitOpen())
            {
                _securityLogger?.LogCircuitBreakerStateChange(
                    call.ServiceName,
                    "Open",
                    health.ConsecutiveFailures,
                    health.CircuitOpenedAt);
                
                _logger.LogWarning(
                    "[ServiceRouter] Circuit breaker open for service: {ServiceName} (failures: {Failures}, opened: {OpenedAt})",
                    call.ServiceName, health.ConsecutiveFailures, health.CircuitOpenedAt);
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.ServiceUnavailable,
                    "Service temporarily unavailable (circuit breaker open)");
            }

            // 6. Create context with work budget
            var workBudget = _workBudgetTracker.CreateBudgetForPeer(remotePeerId);
            
            var context = new MeshServiceContext
            {
                RemotePeerId = remotePeerId,
                RemotePublicKey = remotePublicKey,
                ReceivedAt = DateTimeOffset.UtcNow,
                TraceId = call.CorrelationId,
                ViolationTracker = _violationTracker,
                WorkBudget = workBudget,
                Logger = _logger
            };

            // 7. Invoke service handler with timeout (per-service or default)
            var timeoutSeconds = _options.PerServiceTimeoutSeconds.GetValueOrDefault(
                call.ServiceName, 
                30); // Default 30 seconds
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            ServiceReply reply;
            try
            {
                reply = await service.HandleCallAsync(call, context, cts.Token);
                
                // Success: reset circuit breaker
                health.RecordSuccess();
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                _securityLogger?.LogServiceTimeout(
                    remotePeerId,
                    call.ServiceName,
                    call.Method,
                    TimeSpan.FromSeconds(timeoutSeconds));
                
                _logger.LogWarning(
                    "[ServiceRouter] Service call timed out: {ServiceName}.{Method}",
                    call.ServiceName, call.Method);
                
                // Timeout: record failure for circuit breaker
                health.RecordFailure();
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.Timeout,
                    "Service call timed out");
            }
            catch (Exception ex)
            {
                _securityLogger?.LogServiceException(
                    remotePeerId,
                    call.ServiceName,
                    call.Method,
                    ex);
                
                _logger.LogError(
                    ex,
                    "[ServiceRouter] Service handler threw exception: {ServiceName}.{Method}",
                    call.ServiceName, call.Method);
                
                // Exception: record failure for circuit breaker
                health.RecordFailure();
                
                return CreateErrorReply(
                    call.CorrelationId,
                    ServiceStatusCodes.UnknownError,
                    "Internal service error");
            }

            // 8. Log and return
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
    /// Check global per-peer rate limit (across all services).
    /// </summary>
    private bool CheckGlobalRateLimit(string peerId)
    {
        var now = DateTimeOffset.UtcNow;
        var windowDuration = TimeSpan.FromMinutes(1);
        var maxCallsPerWindow = _options.GlobalMaxCallsPerPeer;

        var (count, windowStart) = _globalPeerCallCounts.GetOrAdd(peerId, _ => (0, now));

        // Reset window if expired
        if (now - windowStart > windowDuration)
        {
            _globalPeerCallCounts[peerId] = (1, now);
            return true;
        }

        // Check limit
        if (count >= maxCallsPerWindow)
        {
            return false;
        }

        // Increment count
        _globalPeerCallCounts[peerId] = (count + 1, windowStart);
        return true;
    }

    /// <summary>
    /// Check per-service rate limit for a peer using sliding window.
    /// Uses per-service limit if configured, otherwise default.
    /// </summary>
    private bool CheckServiceRateLimit(string peerId, string serviceName)
    {
        var now = DateTimeOffset.UtcNow;
        var windowDuration = TimeSpan.FromMinutes(1);
        
        // Get per-service limit or fall back to default
        var maxCallsPerWindow = _options.PerServiceRateLimits.GetValueOrDefault(
            serviceName, 
            _options.DefaultMaxCallsPerMinute);

        // Use composite key for per-service tracking
        var key = $"{peerId}:{serviceName}";
        var (count, windowStart) = _perPeerCallCounts.GetOrAdd(key, _ => (0, now));

        // Reset window if expired
        if (now - windowStart > windowDuration)
        {
            _perPeerCallCounts[key] = (1, now);
            return true;
        }

        // Check limit
        if (count >= maxCallsPerWindow)
        {
            return false;
        }

        // Increment count
        _perPeerCallCounts[key] = (count + 1, windowStart);
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
    /// Get comprehensive statistics about the router, services, and security state.
    /// </summary>
    public RouterStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);

        // Circuit breaker stats
        var circuitBreakerInfo = _serviceHealth
            .Select(kvp => new CircuitBreakerInfo
            {
                ServiceName = kvp.Key,
                ConsecutiveFailures = kvp.Value.ConsecutiveFailures,
                IsOpen = kvp.Value.IsCircuitOpen(),
                OpenedAt = kvp.Value.CircuitOpenedAt
            })
            .ToList();

        // Rate limit stats (active peers in last minute)
        var activeGlobalPeers = _globalPeerCallCounts
            .Where(kvp => now - kvp.Value.windowStart <= TimeSpan.FromMinutes(1))
            .Count();

        var activeServicePeers = _perPeerCallCounts
            .Where(kvp => now - kvp.Value.windowStart <= TimeSpan.FromMinutes(1))
            .Count();

        // Work budget stats
        var workBudgetMetrics = _workBudgetTracker.GetMetrics();

        return new RouterStats
        {
            RegisteredServiceCount = _services.Count,
            TrackedPeerCount = _perPeerCallCounts.Count,
            
            // Rate limiting
            ActivePeersLastMinute = activeGlobalPeers,
            PerServiceTrackedPeers = activeServicePeers,
            
            // Circuit breakers
            CircuitBreakers = circuitBreakerInfo,
            OpenCircuitCount = circuitBreakerInfo.Count(cb => cb.IsOpen),
            
            // Work budget
            WorkBudgetEnabled = true,
            WorkBudgetMetrics = workBudgetMetrics
        };
    }
}

/// <summary>
/// Comprehensive statistics for MeshServiceRouter.
/// Includes service, rate limiting, circuit breaker, and work budget metrics.
/// </summary>
public sealed record RouterStats
{
    // Basic stats
    public int RegisteredServiceCount { get; init; }
    public int TrackedPeerCount { get; init; }
    
    // Rate limiting stats
    public int ActivePeersLastMinute { get; init; }
    public int PerServiceTrackedPeers { get; init; }
    
    // Circuit breaker stats
    public List<CircuitBreakerInfo> CircuitBreakers { get; init; } = new();
    public int OpenCircuitCount { get; init; }
    
    // Work budget stats
    public bool WorkBudgetEnabled { get; init; }
    public WorkBudgetMetrics? WorkBudgetMetrics { get; init; }
}

/// <summary>
/// Circuit breaker information for a service.
/// </summary>
public sealed record CircuitBreakerInfo
{
    public string ServiceName { get; init; } = string.Empty;
    public int ConsecutiveFailures { get; init; }
    public bool IsOpen { get; init; }
    public DateTimeOffset? OpenedAt { get; init; }
}

/// <summary>
/// Service health tracker for circuit breaker pattern.
/// </summary>
internal class ServiceHealthTracker
{
    private const int FailureThreshold = 5;
    private static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromMinutes(5);
    
    public int ConsecutiveFailures { get; private set; }
    public DateTimeOffset? CircuitOpenedAt { get; private set; }

    /// <summary>
    /// Check if circuit breaker is currently open.
    /// </summary>
    public bool IsCircuitOpen()
    {
        if (ConsecutiveFailures < FailureThreshold)
            return false;

        // Circuit is open, check if it should reset (half-open state)
        if (CircuitOpenedAt.HasValue && 
            DateTimeOffset.UtcNow - CircuitOpenedAt.Value >= CircuitOpenDuration)
        {
            // Circuit has been open long enough, allow one test request (half-open)
            ConsecutiveFailures = FailureThreshold - 1; // One more failure will re-open
            CircuitOpenedAt = null;
            return false; // Allow test request
        }

        return true; // Circuit remains open
    }

    /// <summary>
    /// Record a successful call (resets circuit breaker).
    /// </summary>
    public void RecordSuccess()
    {
        ConsecutiveFailures = 0;
        CircuitOpenedAt = null;
    }

    /// <summary>
    /// Record a failed call (may open circuit breaker).
    /// </summary>
    public void RecordFailure()
    {
        ConsecutiveFailures++;
        
        if (ConsecutiveFailures >= FailureThreshold && !CircuitOpenedAt.HasValue)
        {
            CircuitOpenedAt = DateTimeOffset.UtcNow;
        }
    }
}
