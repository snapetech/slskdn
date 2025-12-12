using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Validator and helper utilities for MeshGateway configuration.
/// </summary>
public class MeshGatewayConfigValidator
{
    private readonly ILogger<MeshGatewayConfigValidator> _logger;
    private readonly MeshGatewayOptions _options;

    public MeshGatewayConfigValidator(
        ILogger<MeshGatewayConfigValidator> logger,
        IOptions<MeshGatewayOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Validates configuration and logs warnings/errors on startup.
    /// Returns true if valid, false if startup should be aborted.
    /// </summary>
    public bool ValidateOnStartup()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[GatewayConfig] Mesh gateway is DISABLED");
            return true;
        }

        _logger.LogInformation("[GatewayConfig] Mesh gateway is ENABLED - validating configuration...");

        var (isValid, error) = _options.Validate();
        if (!isValid)
        {
            _logger.LogError("[GatewayConfig] ❌ CONFIGURATION ERROR: {Error}", error);
            return false;
        }

        // Check for security-relevant configurations
        var isLocalhost = IsLocalhost(_options.BindAddress);
        
        if (!isLocalhost)
        {
            _logger.LogWarning(
                "╔═══════════════════════════════════════════════════════════════════════╗");
            _logger.LogWarning(
                "║                    ⚠️  SECURITY WARNING ⚠️                             ║");
            _logger.LogWarning(
                "║  Mesh gateway is binding to NON-LOCALHOST address: {BindAddress,-15} ║",
                _options.BindAddress);
            _logger.LogWarning(
                "║  This allows REMOTE ACCESS to your mesh services via HTTP.           ║");
            _logger.LogWarning(
                "║  Ensure proper firewall rules and authentication are in place.       ║");
            _logger.LogWarning(
                "╚═══════════════════════════════════════════════════════════════════════╝");

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _logger.LogError(
                    "[GatewayConfig] ❌ CRITICAL: ApiKey is REQUIRED for non-localhost binding!");
                return false;
            }
        }

        if (isLocalhost && string.IsNullOrWhiteSpace(_options.CsrfToken))
        {
            _logger.LogWarning(
                "[GatewayConfig] ⚠️  CsrfToken is not set. Auto-generating a random token for this session.");
            _logger.LogWarning(
                "[GatewayConfig] ⚠️  Set MeshGateway:CsrfToken in config to persist across restarts.");
            
            // Auto-generate CSRF token for this session
            _options.CsrfToken = GenerateSecureToken();
            
            _logger.LogInformation(
                "[GatewayConfig] Generated CSRF token: {Token}", _options.CsrfToken);
            _logger.LogInformation(
                "[GatewayConfig] Clients must include header: X-Slskdn-Csrf: {Token}", _options.CsrfToken);
        }

        if (_options.AllowedServices.Count == 0)
        {
            _logger.LogError(
                "[GatewayConfig] ❌ AllowedServices is empty - no services can be called!");
            return false;
        }

        _logger.LogInformation(
            "[GatewayConfig] ✅ Bind address: {BindAddress}:{Port}",
            _options.BindAddress, _options.Port == 0 ? "shared" : _options.Port.ToString());
        _logger.LogInformation(
            "[GatewayConfig] ✅ Allowed services: {Services}",
            string.Join(", ", _options.AllowedServices));
        _logger.LogInformation(
            "[GatewayConfig] ✅ API key: {Status}",
            string.IsNullOrWhiteSpace(_options.ApiKey) ? "NOT SET (localhost only)" : "CONFIGURED");
        _logger.LogInformation(
            "[GatewayConfig] ✅ CSRF token: {Status}",
            string.IsNullOrWhiteSpace(_options.CsrfToken) ? "NOT SET" : "CONFIGURED");
        _logger.LogInformation(
            "[GatewayConfig] ✅ Max request body: {Size} bytes",
            _options.MaxRequestBodyBytes);
        _logger.LogInformation(
            "[GatewayConfig] ✅ Request timeout: {Timeout} seconds",
            _options.RequestTimeoutSeconds);

        if (_options.LogBodies)
        {
            _logger.LogWarning(
                "[GatewayConfig] ⚠️  LogBodies is ENABLED - sensitive data may be logged!");
        }

        return true;
    }

    /// <summary>
    /// Generates a cryptographically secure random token for API keys or CSRF tokens.
    /// </summary>
    public static string GenerateSecureToken(int byteLength = 32)
    {
        var bytes = new byte[byteLength];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsLocalhost(string address)
    {
        return address == "127.0.0.1" ||
               address == "localhost" ||
               address == "::1" ||
               address == "0.0.0.0";
    }
}
