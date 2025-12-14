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
    }

    /// <summary>
    /// Process the HTTP request.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;

        if (remoteIp == null)
        {
            await _next(context);
            return;
        }

        // Skip aggressive rate limiting for private/local IPs (e.g., web UI from LAN)
        var isPrivateIp = IsPrivateOrLocalIp(remoteIp);

        // Check if IP is banned (still applies to private IPs if explicitly banned)
        if (_violationTracker?.IsIpBanned(remoteIp) == true)
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

            // Validate request path for traversal attempts
            var path = context.Request.Path.Value;
            if (!string.IsNullOrEmpty(path) && PathGuard.ContainsTraversal(path))
            {
                _logger.LogWarning("Path traversal attempt from {SanitizedIp}: {SanitizedPath}", LoggingSanitizer.SanitizeIpAddress(remoteIp), LoggingSanitizer.SanitizeSensitiveData(path));
                _violationTracker?.RecordIpViolation(remoteIp, ViolationType.PathTraversal, path);

                _eventSink?.Report(SecurityEvent.Create(
                    SecurityEventType.PathTraversal,
                    SecuritySeverity.High,
                    $"Path traversal attempt: {path}",
                    remoteIp.ToString()));

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Bad Request");
                return;
            }

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
        return app.UseMiddleware<SecurityMiddleware>();
    }
}

