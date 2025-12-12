using System.Collections.Generic;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Configuration options for the mesh service HTTP gateway.
/// </summary>
public class MeshGatewayOptions
{
    /// <summary>
    /// Enable or disable the HTTP gateway (default: false).
    /// IMPORTANT: This gateway exposes mesh service calls via HTTP.
    /// Only enable if you understand the security implications.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Bind address for the gateway (default: "127.0.0.1").
    /// SECURITY: Only bind to localhost unless you have implemented proper authentication
    /// and understand the risks of remote access.
    /// </summary>
    public string BindAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port for the gateway (default: 0 = reuse existing HTTP listener).
    /// </summary>
    public int Port { get; set; } = 0;

    /// <summary>
    /// API key required for gateway access (optional, but MANDATORY if not binding to localhost).
    /// Generate via: slskd generate-gateway-key
    /// Clients must provide this via X-Slskdn-ApiKey header.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// CSRF token for localhost protection (optional, auto-generated if empty).
    /// Clients must provide this via X-Slskdn-Csrf header.
    /// This prevents malicious websites from making requests to localhost:port.
    /// </summary>
    public string? CsrfToken { get; set; }

    /// <summary>
    /// List of service names that can be invoked via the gateway (default: empty = none).
    /// Only services explicitly listed here can be called.
    /// Example: ["pods", "shadow-index", "mesh-introspect"]
    /// </summary>
    public List<string> AllowedServices { get; set; } = new();

    /// <summary>
    /// Maximum request body size in bytes (default: 1MB).
    /// </summary>
    public int MaxRequestBodyBytes { get; set; } = 1048576;

    /// <summary>
    /// Request timeout in seconds (default: 30).
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable logging of request/response bodies (default: false).
    /// WARNING: This may log sensitive data. Only enable for debugging.
    /// </summary>
    public bool LogBodies { get; set; } = false;

    /// <summary>
    /// Require explicit acknowledgment of risks when binding to non-localhost (default: false).
    /// If true, IUnderstandTheRisk must be set to true to bind to non-localhost addresses.
    /// </summary>
    public bool RequireRiskAcknowledgment { get; set; } = true;

    /// <summary>
    /// Explicit acknowledgment that you understand the security risks of remote gateway access.
    /// Must be true if BindAddress is not 127.0.0.1 and RequireRiskAcknowledgment is true.
    /// </summary>
    public bool IUnderstandTheRisk { get; set; } = false;

    /// <summary>
    /// Allowed Origins for CORS (default: empty = no CORS).
    /// Only set this if you need browser-based access from specific origins.
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Enable rate limiting per IP address (default: true).
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Maximum requests per IP per minute (default: 60).
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Validates the configuration and returns true if valid, false otherwise.
    /// </summary>
    public (bool IsValid, string? Error) Validate()
    {
        if (!Enabled)
            return (true, null);

        // Check if binding to non-localhost without API key
        if (!IsLocalhost(BindAddress) && string.IsNullOrWhiteSpace(ApiKey))
        {
            return (false, "SECURITY: ApiKey is REQUIRED when binding to non-localhost addresses. " +
                          "Set MeshGateway:ApiKey in config or use 'slskd generate-gateway-key'.");
        }

        // Check if binding to non-localhost without risk acknowledgment
        if (!IsLocalhost(BindAddress) && RequireRiskAcknowledgment && !IUnderstandTheRisk)
        {
            return (false, "SECURITY: Binding to non-localhost requires IUnderstandTheRisk=true. " +
                          "Set MeshGateway:IUnderstandTheRisk=true if you understand the security implications.");
        }

        // Check if AllowedServices is empty
        if (AllowedServices.Count == 0)
        {
            return (false, "MeshGateway:AllowedServices cannot be empty. " +
                          "Specify at least one service name (e.g., 'pods', 'shadow-index', 'mesh-introspect').");
        }

        return (true, null);
    }

    private static bool IsLocalhost(string address)
    {
        return address == "127.0.0.1" ||
               address == "localhost" ||
               address == "::1" ||
               address == "0.0.0.0"; // 0.0.0.0 is treated as localhost for validation purposes
    }
}

