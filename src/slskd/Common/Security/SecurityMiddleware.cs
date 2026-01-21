// <copyright file="SecurityMiddleware.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// ASP.NET Core middleware that applies security checks to all requests.
/// </summary>
public sealed class SecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityMiddleware> _logger;
    private readonly NetworkGuard? _networkGuard;
    private readonly ViolationTracker? _violationTracker;
    private readonly FingerprintDetection? _fingerprintDetection;
    private readonly ParanoidMode? _paranoidMode;
    private readonly ISecurityEventSink? _eventSink;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityMiddleware"/> class.
    /// </summary>
    public SecurityMiddleware(
        RequestDelegate next,
        ILogger<SecurityMiddleware> logger,
        NetworkGuard? networkGuard = null,
        ViolationTracker? violationTracker = null,
        FingerprintDetection? fingerprintDetection = null,
        ParanoidMode? paranoidMode = null,
        ISecurityEventSink? eventSink = null)
    {
        _next = next;
        _logger = logger;
        _networkGuard = networkGuard;
        _violationTracker = violationTracker;
        _fingerprintDetection = fingerprintDetection;
        _paranoidMode = paranoidMode;
        _eventSink = eventSink;
        
        // Don't log constructor - it's called once per request and creates too much noise
        // Only log at TRACE level if needed for debugging
    }

    /// <summary>
    /// Process the HTTP request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // CRITICAL: Always log when middleware is invoked to verify it's running
        var remoteIp = context.Connection.RemoteIpAddress;
        var pathBase = context.Request.PathBase.Value ?? string.Empty;
        var path = context.Request.Path.Value ?? string.Empty;
        var normalizedPath = pathBase + path;
        
        // Get raw target from HTTP feature
        var httpRequestFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpRequestFeature>();
        var rawTarget = httpRequestFeature?.RawTarget ?? string.Empty;
        
        // Only log at DEBUG level for non-private IPs to reduce noise from localhost
        var isPrivateIp = remoteIp != null && IsPrivateOrLocalIp(remoteIp);
        if (!isPrivateIp)
        {
            _logger.LogDebug(
                "[SecurityMiddleware] Request from {Ip}: Path='{Path}', RawTarget='{RawTarget}'",
                remoteIp, path, rawTarget);
        }
        
        // CRITICAL: Path traversal protection should ALWAYS be enabled, even when security is disabled
        // This is a basic security requirement that should never be bypassed
        // Check for path traversal FIRST, before any other checks
        
        // Check raw target first (before normalization) - this catches plain traversal
        // PathGuard.ContainsTraversal checks for both plain and URL-encoded traversal
        if (!string.IsNullOrEmpty(rawTarget) && PathGuard.ContainsTraversal(rawTarget))
        {
            _logger.LogWarning("Path traversal attempt from {SanitizedIp}: {SanitizedPath}", 
                LoggingSanitizer.SanitizeIpAddress(remoteIp), 
                LoggingSanitizer.SanitizeSensitiveData(rawTarget));
            
            // Record violation if ViolationTracker is available
            if (remoteIp != null)
            {
                _violationTracker?.RecordIpViolation(remoteIp, ViolationType.PathTraversal, rawTarget);
            }

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.PathTraversal,
                SecuritySeverity.High,
                $"Path traversal attempt: {rawTarget}",
                remoteIp?.ToString() ?? "unknown"));

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Bad Request");
            return;
        }
        
        // Also check normalized path (after routing) as a secondary check
        // This catches cases where routing doesn't fully normalize or where the normalized path is still suspicious
        if (!string.IsNullOrEmpty(normalizedPath) && PathGuard.ContainsTraversal(normalizedPath))
        {
            _logger.LogWarning("Path traversal attempt from {SanitizedIp}: {SanitizedPath}", 
                LoggingSanitizer.SanitizeIpAddress(remoteIp), 
                LoggingSanitizer.SanitizeSensitiveData(normalizedPath));
            
            // Record violation if ViolationTracker is available
            if (remoteIp != null)
            {
                _violationTracker?.RecordIpViolation(remoteIp, ViolationType.PathTraversal, normalizedPath);
            }

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.PathTraversal,
                SecuritySeverity.High,
                $"Path traversal attempt: {normalizedPath}",
                remoteIp?.ToString() ?? "unknown"));

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Bad Request");
            return;
        }
        
        // Check for suspicious paths even after normalization
        // These are paths that look like they're trying to access system files or directories
        // even though they don't contain explicit traversal sequences
        var isSuspicious = !string.IsNullOrEmpty(normalizedPath) && IsSuspiciousPath(normalizedPath);
        if (isSuspicious)
        {
            _logger.LogWarning("Suspicious path access attempt from {SanitizedIp}: {SanitizedPath}", 
                LoggingSanitizer.SanitizeIpAddress(remoteIp), 
                LoggingSanitizer.SanitizeSensitiveData(normalizedPath));
            
            // Record violation if ViolationTracker is available
            if (remoteIp != null)
            {
                _violationTracker?.RecordIpViolation(remoteIp, ViolationType.PathTraversal, normalizedPath);
            }

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.PathTraversal,
                SecuritySeverity.High,
                $"Suspicious path access attempt: {normalizedPath}",
                remoteIp?.ToString() ?? "unknown"));

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("Bad Request");
            return;
        }

        // If all security services are null, security is disabled - pass through after path traversal check
        // Check NetworkGuard and ViolationTracker first as they're the most likely to block
        if (_networkGuard == null && _violationTracker == null)
        {
            // Security is disabled - pass through without other checks (path traversal already checked above)
            _logger.LogDebug("[SecurityMiddleware] Security disabled (both NetworkGuard and ViolationTracker are null), passing through after path traversal check");
            await _next(context);
            return;
        }
        
        if (remoteIp == null)
        {
            await _next(context);
            return;
        }

        // Skip aggressive rate limiting for private/local IPs (e.g., web UI from LAN)
        // Note: isPrivateIp was already calculated above for logging
        if (!isPrivateIp)
        {
            _logger.LogDebug("[SecurityMiddleware] Security enabled, proceeding with additional checks for {Ip}", remoteIp);
        }

        // Check if IP is banned (still applies to private IPs if explicitly banned)
        // IMPORTANT: Only check if ViolationTracker is actually registered (security enabled)
        // Double-check that security is actually enabled by verifying NetworkGuard is also registered
        // This prevents false positives when only one service is registered
        if (_networkGuard != null && _violationTracker != null && _violationTracker.IsIpBanned(remoteIp))
        {
            _logger.LogWarning("Request blocked from banned IP: {Ip}", remoteIp);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("Forbidden");
            return;
        }

        // Check connection limits (relaxed for private IPs)
        if (!isPrivateIp && _networkGuard != null && !_networkGuard.AllowConnection(remoteIp))
        {
            _logger.LogWarning("Request blocked due to connection limits: {Ip}", remoteIp);
            _violationTracker?.RecordIpViolation(remoteIp, ViolationType.RateLimitExceeded, "Connection limit");

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.RateLimit,
                SecuritySeverity.Medium,
                "Connection limit exceeded",
                remoteIp.ToString()));

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsync("Too Many Requests");
            return;
        }

        // Register connection for tracking
        string? connectionId = null;
        if (_networkGuard != null)
        {
            connectionId = _networkGuard.RegisterConnection(remoteIp);
        }

        try
        {
            // Record connection for fingerprint detection (skip private IPs to reduce noise)
            if (!isPrivateIp)
            {
                _fingerprintDetection?.RecordConnection(
                    remoteIp,
                    context.Connection.RemotePort,
                    context.Request.Protocol,
                    context.Request.Headers.UserAgent.ToString());
            }

            // Check for reconnaissance patterns (skip for private IPs - high false positive rate)
            if (!isPrivateIp && _fingerprintDetection?.IsKnownScanner(remoteIp) == true)
            {
                _logger.LogWarning("Request from known scanner: {Ip}", remoteIp);

                _eventSink?.Report(SecurityEvent.Create(
                    SecurityEventType.Reconnaissance,
                    SecuritySeverity.High,
                    "Request from known scanner",
                    remoteIp.ToString()));

                // Don't block, but log - could be false positive
            }

            // Path traversal is already checked at the top of the method (always enabled)

            // Check message/body size limits
            if (context.Request.ContentLength > _networkGuard?.MaxMessageSize)
            {
                _logger.LogWarning("Request body too large from {Ip}: {Size}", remoteIp, context.Request.ContentLength);
                _violationTracker?.RecordIpViolation(remoteIp, ViolationType.InvalidMessage, "Body too large");

                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsync("Payload Too Large");
                return;
            }

            // Continue to next middleware
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing request from {Ip}", remoteIp);

            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.Other,
                SecuritySeverity.Medium,
                $"Request processing error: {ex.Message}",
                remoteIp.ToString()));

            throw;
        }
        finally
        {
            // Unregister connection
            if (_networkGuard != null && connectionId != null)
            {
                _networkGuard.UnregisterConnection(remoteIp, connectionId);
            }
        }
    }

    /// <summary>
    /// Check if an IP address is a private/local address.
    /// Private IPs (LAN, localhost) get relaxed rate limiting since they're typically the web UI.
    /// </summary>
    private static bool IsPrivateOrLocalIp(IPAddress ip)
    {
        // Handle IPv4-mapped IPv6 addresses
        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        // Loopback (127.x.x.x or ::1)
        if (IPAddress.IsLoopback(ip))
        {
            return true;
        }

        // IPv4 private ranges
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }
        }

        // IPv6 private ranges
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Link-local (fe80::/10)
            if (ip.IsIPv6LinkLocal)
            {
                return true;
            }

            // Site-local (deprecated but still used) (fec0::/10)
            if (ip.IsIPv6SiteLocal)
            {
                return true;
            }

            // Unique local addresses (fc00::/7)
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc)
            {
                return true;
            }
        }

        return false;
    }
    
    /// <summary>
    /// Check if a normalized path looks suspicious even without explicit traversal sequences.
    /// This catches cases where the path was normalized but still points to system directories.
    /// </summary>
    private bool IsSuspiciousPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        
        // Normalize the path for comparison (remove leading/trailing slashes, convert to lowercase)
        // Keep the leading slash for absolute path detection
        var normalized = path.TrimEnd('/').ToLowerInvariant();
        var normalizedWithoutSlash = normalized.TrimStart('/');
        
        // CRITICAL: Skip checking known safe routes entirely - they're handled by routing
        // This prevents unnecessary checks and logging for API endpoints, hubs, etc.
        if (normalized.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/hub/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/static/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/swagger/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("/mesh/gateway", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("/metrics", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("/", StringComparison.Ordinal))
        {
            // Known safe route - skip suspicious path check entirely
            return false;
        }
        
        // List of suspicious system paths that should never be accessible
        var suspiciousPaths = new[]
        {
            // System configuration files
            "etc/passwd",
            "etc/shadow",
            "etc/hosts",
            "etc/hostname",
            "etc/resolv.conf",
            "etc/fstab",
            "etc/group",
            "etc/sudoers",
            
            // System directories
            "etc/",
            "root/",
            "root/.ssh",
            "root/.bashrc",
            "root/.profile",
            "proc/",
            "sys/",
            "dev/",
            "boot/",
            "var/log/",
            "var/lib/",
            "var/spool/",
            "usr/bin/",
            "usr/sbin/",
            "usr/lib/",
            "usr/local/bin/",
            "sbin/",
            "bin/",
            
            // Windows system paths (for cross-platform protection)
            "windows/system32",
            "windows/syswow64",
            "windows/win.ini",
            "windows/system.ini",
            "program files",
            "programdata",
            "users/administrator",
            "users/admin",
            
            // Common sensitive files
            ".ssh/id_rsa",
            ".ssh/id_dsa",
            ".ssh/authorized_keys",
            ".bash_history",
            ".mysql_history",
            ".gitconfig",
            ".aws/credentials",
            ".docker/config.json",
        };
        
        // Check if path starts with any suspicious path (without leading slash)
        foreach (var suspicious in suspiciousPaths)
        {
            if (normalizedWithoutSlash.StartsWith(suspicious, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[IsSuspiciousPath] Suspicious path detected: '{Path}' matches pattern '{Suspicious}'", normalizedWithoutSlash, suspicious);
                return true;
            }
        }
        
        // Check for paths that are absolute (start with /) and look like system paths
        // This catches paths like /etc/passwd, /root/.ssh, etc. that weren't caught above
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            if (normalizedWithoutSlash.StartsWith("etc/") || 
                normalizedWithoutSlash.StartsWith("root/") || 
                normalizedWithoutSlash.StartsWith("proc/") ||
                normalizedWithoutSlash.StartsWith("sys/") ||
                normalizedWithoutSlash.StartsWith("dev/") ||
                normalizedWithoutSlash.StartsWith("var/") ||
                normalizedWithoutSlash.StartsWith("usr/") ||
                normalizedWithoutSlash.StartsWith("sbin/") ||
                normalizedWithoutSlash.StartsWith("bin/") ||
                normalizedWithoutSlash.StartsWith("windows/") ||
                normalizedWithoutSlash.StartsWith("program") ||
                normalizedWithoutSlash.StartsWith("users/"))
            {
                _logger.LogWarning("[IsSuspiciousPath] Suspicious system path detected: '{Path}'", normalizedWithoutSlash);
                return true;
            }
        }
        
        return false;
    }
}

/// <summary>
/// Extension methods for adding security middleware.
/// </summary>
public static class SecurityMiddlewareExtensions
{
    /// <summary>
    /// Add security middleware to the application pipeline.
    /// Should be added early in the pipeline, before authentication.
    /// </summary>
    public static IApplicationBuilder UseSecurityMiddleware(this IApplicationBuilder app)
    {
        // Resolve services once and reuse the middleware instance
        var serviceProvider = app.ApplicationServices;
        var logger = serviceProvider.GetRequiredService<ILogger<SecurityMiddleware>>();
        var networkGuard = serviceProvider.GetService<NetworkGuard>();
        var violationTracker = serviceProvider.GetService<ViolationTracker>();
        var fingerprintDetection = serviceProvider.GetService<FingerprintDetection>();
        var paranoidMode = serviceProvider.GetService<ParanoidMode>();
        var eventSink = serviceProvider.GetService<ISecurityEventSink>();
        
        // Create middleware instance directly - this ensures it's constructed and we can catch any exceptions
        // Use a factory pattern to create middleware per request
        return app.Use(async (context, next) =>
        {
            try
            {
                var middleware = new SecurityMiddleware(
                    next,
                    logger,
                    networkGuard,
                    violationTracker,
                    fingerprintDetection,
                    paranoidMode,
                    eventSink);
                
                await middleware.InvokeAsync(context);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[UseSecurityMiddleware] Exception in security middleware: {Message}", ex.Message);
                throw;
            }
        });
    }
}

