using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Middleware to enforce authentication and CSRF protection for the mesh gateway.
/// </summary>
public class MeshGatewayAuthMiddleware
{
    private const string ApiKeyHeader = "X-Slskdn-ApiKey";
    private const string CsrfHeader = "X-Slskdn-Csrf";
    
    private readonly RequestDelegate _next;
    private readonly ILogger<MeshGatewayAuthMiddleware> _logger;
    private readonly MeshGatewayOptions _options;

    public MeshGatewayAuthMiddleware(
        RequestDelegate next,
        ILogger<MeshGatewayAuthMiddleware> _logger,
        IOptions<MeshGatewayOptions> options)
    {
        _next = next;
        this._logger = _logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to mesh gateway endpoints
        if (!context.Request.Path.StartsWithSegments("/mesh"))
        {
            await _next(context);
            return;
        }

        // Skip if gateway is disabled (endpoints shouldn't be registered, but double-check)
        if (!_options.Enabled)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "mesh_gateway_disabled" });
            return;
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var isLocalhost = IsLocalRequest(context);

        // Check API key requirement (required for non-localhost)
        if (!isLocalhost && !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            if (!context.Request.Headers.TryGetValue(ApiKeyHeader, out var apiKeyValue) ||
                !SecureCompare(apiKeyValue.ToString(), _options.ApiKey))
            {
                _logger.LogWarning(
                    "[GatewayAuth] Unauthorized access attempt from {RemoteIp} - invalid or missing API key",
                    remoteIp);
                
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "unauthorized",
                    message = $"Valid {ApiKeyHeader} header is required"
                });
                return;
            }
        }

        // Check CSRF token (required for localhost to prevent browser-based attacks)
        if (isLocalhost && !string.IsNullOrWhiteSpace(_options.CsrfToken))
        {
            if (!context.Request.Headers.TryGetValue(CsrfHeader, out var csrfValue) ||
                !SecureCompare(csrfValue.ToString(), _options.CsrfToken))
            {
                _logger.LogWarning(
                    "[GatewayAuth] CSRF validation failed from {RemoteIp}",
                    remoteIp);
                
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "csrf_required",
                    message = $"Valid {CsrfHeader} header is required for localhost access"
                });
                return;
            }
        }

        // Check Origin header if present (helps prevent CSRF)
        if (context.Request.Headers.TryGetValue("Origin", out var origin))
        {
            var originStr = origin.ToString();
            
            // If AllowedOrigins is configured, enforce it
            if (_options.AllowedOrigins.Count > 0)
            {
                if (!_options.AllowedOrigins.Contains(originStr))
                {
                    _logger.LogWarning(
                        "[GatewayAuth] Rejected request from unauthorized origin: {Origin}",
                        originStr);
                    
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "origin_not_allowed",
                        message = "Origin not in allowed list"
                    });
                    return;
                }
            }
            // If AllowedOrigins is empty and this is localhost, reject non-null origins as a safety measure
            else if (isLocalhost && !IsLocalhostOrigin(originStr))
            {
                _logger.LogWarning(
                    "[GatewayAuth] Rejected cross-origin request to localhost from: {Origin}",
                    originStr);
                
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "origin_not_allowed",
                    message = "Cross-origin requests to localhost are not allowed by default"
                });
                return;
            }
        }

        // Passed all checks
        _logger.LogDebug(
            "[GatewayAuth] Authorized request from {RemoteIp} to {Path}",
            remoteIp, context.Request.Path);
        
        await _next(context);
    }

    private static bool IsLocalRequest(HttpContext context)
    {
        var remoteIp = context.Connection.RemoteIpAddress;
        if (remoteIp == null)
            return false;

        // Check for localhost/loopback
        if (IPAddress.IsLoopback(remoteIp))
            return true;

        // Check if same as local IP (handles cases where request comes from same machine)
        var localIp = context.Connection.LocalIpAddress;
        if (localIp != null && remoteIp.Equals(localIp))
            return true;

        return false;
    }

    private static bool IsLocalhostOrigin(string origin)
    {
        return origin.Contains("localhost") ||
               origin.Contains("127.0.0.1") ||
               origin.Contains("[::1]");
    }

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks.
    /// </summary>
    private static bool SecureCompare(string a, string b)
    {
        if (a == null || b == null)
            return false;

        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);

        if (aBytes.Length != bBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}

