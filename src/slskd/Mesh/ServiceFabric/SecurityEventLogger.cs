using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Centralized security event logger for service fabric.
/// Provides structured, consistent logging for all security-relevant events.
/// </summary>
public class SecurityEventLogger
{
    private readonly ILogger<SecurityEventLogger> _logger;

    public SecurityEventLogger(ILogger<SecurityEventLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Log rate limit violation.
    /// </summary>
    public void LogRateLimitViolation(
        string peerId,
        string serviceName,
        string limitType,
        int currentCount,
        int maxAllowed)
    {
        _logger.LogWarning(
            "[SECURITY] Rate limit violation | " +
            "PeerId: {PeerId} | " +
            "Service: {ServiceName} | " +
            "LimitType: {LimitType} | " +
            "Current: {CurrentCount} | " +
            "Max: {MaxAllowed}",
            peerId, serviceName, limitType, currentCount, maxAllowed);
    }

    /// <summary>
    /// Log circuit breaker state change.
    /// </summary>
    public void LogCircuitBreakerStateChange(
        string serviceName,
        string newState,
        int consecutiveFailures,
        DateTimeOffset? openedAt)
    {
        _logger.LogWarning(
            "[SECURITY] Circuit breaker state change | " +
            "Service: {ServiceName} | " +
            "NewState: {NewState} | " +
            "Failures: {Failures} | " +
            "OpenedAt: {OpenedAt}",
            serviceName, newState, consecutiveFailures, openedAt);
    }

    /// <summary>
    /// Log discovery abuse detection.
    /// </summary>
    public void LogDiscoveryAbuse(
        string peerId,
        string pattern,
        int queryCount,
        int uniqueServiceCount)
    {
        _logger.LogWarning(
            "[SECURITY] Discovery abuse detected | " +
            "PeerId: {PeerId} | " +
            "Pattern: {Pattern} | " +
            "Queries: {QueryCount} | " +
            "UniqueServices: {UniqueServiceCount}",
            peerId, pattern, queryCount, uniqueServiceCount);
    }

    /// <summary>
    /// Log payload size violation.
    /// </summary>
    public void LogPayloadSizeViolation(
        string peerId,
        string serviceName,
        string method,
        int payloadSize,
        int maxAllowed)
    {
        _logger.LogWarning(
            "[SECURITY] Payload size violation | " +
            "PeerId: {PeerId} | " +
            "Service: {ServiceName} | " +
            "Method: {Method} | " +
            "Size: {PayloadSize} | " +
            "Max: {MaxAllowed}",
            peerId, serviceName, method, payloadSize, maxAllowed);
    }

    /// <summary>
    /// Log service descriptor validation failure.
    /// </summary>
    public void LogDescriptorValidationFailure(
        string serviceName,
        string reason,
        string? peerId = null)
    {
        _logger.LogWarning(
            "[SECURITY] Service descriptor validation failed | " +
            "Service: {ServiceName} | " +
            "Reason: {Reason} | " +
            "PeerId: {PeerId}",
            serviceName, reason, peerId ?? "unknown");
    }

    /// <summary>
    /// Log service call timeout.
    /// </summary>
    public void LogServiceTimeout(
        string peerId,
        string serviceName,
        string method,
        TimeSpan timeout)
    {
        _logger.LogWarning(
            "[SECURITY] Service call timeout | " +
            "PeerId: {PeerId} | " +
            "Service: {ServiceName} | " +
            "Method: {Method} | " +
            "Timeout: {Timeout}s",
            peerId, serviceName, method, timeout.TotalSeconds);
    }

    /// <summary>
    /// Log service call exception.
    /// </summary>
    public void LogServiceException(
        string peerId,
        string serviceName,
        string method,
        Exception exception)
    {
        _logger.LogError(
            exception,
            "[SECURITY] Service call exception | " +
            "PeerId: {PeerId} | " +
            "Service: {ServiceName} | " +
            "Method: {Method}",
            peerId, serviceName, method);
    }

    /// <summary>
    /// Log unauthorized access attempt.
    /// </summary>
    public void LogUnauthorizedAccess(
        string peerId,
        string serviceName,
        string method,
        string reason)
    {
        _logger.LogWarning(
            "[SECURITY] Unauthorized access attempt | " +
            "PeerId: {PeerId} | " +
            "Service: {ServiceName} | " +
            "Method: {Method} | " +
            "Reason: {Reason}",
            peerId, serviceName, method, reason);
    }

    /// <summary>
    /// Log malformed request.
    /// </summary>
    public void LogMalformedRequest(
        string peerId,
        string reason,
        string? additionalContext = null)
    {
        _logger.LogWarning(
            "[SECURITY] Malformed request | " +
            "PeerId: {PeerId} | " +
            "Reason: {Reason} | " +
            "Context: {Context}",
            peerId, reason, additionalContext ?? "none");
    }

    /// <summary>
    /// Log client-side quota violation.
    /// </summary>
    public void LogClientQuotaViolation(
        string targetPeerId,
        string quotaType,
        int currentCount,
        int maxAllowed)
    {
        _logger.LogWarning(
            "[SECURITY] Client quota violation | " +
            "TargetPeer: {TargetPeerId} | " +
            "QuotaType: {QuotaType} | " +
            "Current: {CurrentCount} | " +
            "Max: {MaxAllowed}",
            targetPeerId, quotaType, currentCount, maxAllowed);
    }

    /// <summary>
    /// Log successful security mitigation.
    /// </summary>
    public void LogSecurityMitigation(
        string peerId,
        string mitigationType,
        string reason)
    {
        _logger.LogInformation(
            "[SECURITY] Mitigation applied | " +
            "PeerId: {PeerId} | " +
            "Type: {MitigationType} | " +
            "Reason: {Reason}",
            peerId, mitigationType, reason);
    }

    /// <summary>
    /// Log suspicious pattern detected.
    /// </summary>
    public void LogSuspiciousPattern(
        string peerId,
        string pattern,
        Dictionary<string, object>? metrics = null)
    {
        var metricsStr = metrics != null 
            ? string.Join(", ", metrics.Select(kvp => $"{kvp.Key}={kvp.Value}"))
            : "none";

        _logger.LogWarning(
            "[SECURITY] Suspicious pattern | " +
            "PeerId: {PeerId} | " +
            "Pattern: {Pattern} | " +
            "Metrics: {Metrics}",
            peerId, pattern, metricsStr);
    }
}

