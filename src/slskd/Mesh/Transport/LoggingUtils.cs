// <copyright file="LoggingUtils.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Transport;

/// <summary>
/// Utilities for privacy-safe logging that prevents sensitive data leakage.
/// </summary>
public static class LoggingUtils
{
    private static readonly HashSet<string> SensitiveKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "privatekey", "private_key", "secret", "password", "token", "apikey", "api_key",
        "certificate", "cert", "key", "pin", "signature", "hash", "digest"
    };

    /// <summary>
    /// Logs a message with sensitive data redaction.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="level">The log level.</param>
    /// <param name="message">The log message.</param>
    /// <param name="args">The message arguments.</param>
    public static void LogSafe<T>(this ILogger<T> logger, LogLevel level, string? message, params object?[] args)
    {
        if (!logger.IsEnabled(level))
        {
            return;
        }

        var safeArgs = RedactSensitiveData(args);
        logger.Log(level, message, safeArgs);
    }

    /// <summary>
    /// Logs a debug message with sensitive data redaction and debug gating.
    /// Only logs in debug builds or when debug logging is explicitly enabled.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The log message.</param>
    /// <param name="args">The message arguments.</param>
    public static void LogDebugSafe<T>(this ILogger<T> logger, string? message, params object?[] args)
    {
        // Only log debug messages if explicitly enabled (not just because logger.IsEnabled(LogLevel.Debug))
        // This prevents accidental leakage of sensitive debug information
#if DEBUG
        var safeArgs = RedactSensitiveData(args);
        logger.LogDebug(message, safeArgs);
#endif
    }

    /// <summary>
    /// Logs a trace message with sensitive data redaction and trace gating.
    /// Only logs in trace builds or when trace logging is explicitly enabled.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="message">The log message.</param>
    /// <param name="args">The message arguments.</param>
    public static void LogTraceSafe<T>(this ILogger<T> logger, string? message, params object?[] args)
    {
        // Only log trace messages if explicitly enabled
        // Trace often contains the most sensitive debugging information
#if DEBUG
        var safeArgs = RedactSensitiveData(args);
        logger.LogTrace(message, safeArgs);
#endif
    }

    /// <summary>
    /// Safely logs a peer ID, showing only a truncated version for privacy.
    /// </summary>
    /// <param name="peerId">The peer ID to log.</param>
    /// <returns>A privacy-safe representation of the peer ID.</returns>
    public static string SafePeerId(string? peerId)
    {
        if (string.IsNullOrEmpty(peerId))
        {
            return "[null]";
        }

        if (peerId.Length <= 8)
        {
            return peerId; // Too short to be sensitive
        }

        // Show first 4 and last 4 characters for debugging, hide middle
        return $"{peerId[..4]}...{peerId[^4..]}";
    }

    /// <summary>
    /// Safely logs an IP address or hostname, redacting sensitive information.
    /// </summary>
    /// <param name="endpoint">The endpoint to log safely.</param>
    /// <returns>A privacy-safe representation of the endpoint.</returns>
    public static string SafeEndpoint(string? endpoint)
    {
        if (string.IsNullOrEmpty(endpoint))
        {
            return "[null]";
        }

        if (TryExtractHostAndPort(endpoint, out var host, out var port))
        {
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return endpoint;
            }

            if (System.Net.IPAddress.TryParse(host, out var ipAddress))
            {
                if (IsSafeLocalAddress(ipAddress))
                {
                    return endpoint;
                }

                if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    var parts = ipAddress.ToString().Split('.');
                    return $"xxx.xxx.xxx.{parts[3]}:{port}";
                }

                var ipv6Segments = ipAddress.ToString().Split(':', StringSplitOptions.RemoveEmptyEntries);
                var suffix = ipv6Segments.Length > 0 ? ipv6Segments[^1] : ipAddress.ToString();
                return $"xxxx:xxxx:xxxx:{suffix}:{port}";
            }

            return $"{RedactHostname(host)}:{port}";
        }

        if (System.Net.IPAddress.TryParse(endpoint, out var rawIpAddress))
        {
            if (IsSafeLocalAddress(rawIpAddress))
            {
                return endpoint;
            }

            if (rawIpAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                var parts = rawIpAddress.ToString().Split('.');
                return $"xxx.xxx.xxx.{parts[3]}";
            }

            var ipv6Segments = rawIpAddress.ToString().Split(':', StringSplitOptions.RemoveEmptyEntries);
            var suffix = ipv6Segments.Length > 0 ? ipv6Segments[^1] : rawIpAddress.ToString();
            return $"xxxx:xxxx:xxxx:{suffix}";
        }

        if (string.Equals(endpoint, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return endpoint;
        }

        return endpoint.Contains('.') ? RedactHostname(endpoint) : "[redacted]";
    }

    private static string RedactHostname(string hostname)
    {
        var parts = hostname.Split('.');
        if (parts.Length >= 2)
        {
            var tld = parts[^1];
            var domain = parts.Length >= 3 ? parts[^2] : parts[0];
            return $"{domain[..Math.Min(3, domain.Length)]}...{tld}";
        }

        return "[redacted]";
    }

    private static bool TryExtractHostAndPort(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        string portPart;
        if (endpoint.StartsWith("[", StringComparison.Ordinal))
        {
            var closingBracketIndex = endpoint.IndexOf(']');
            if (closingBracketIndex <= 1 || closingBracketIndex >= endpoint.Length - 2 || endpoint[closingBracketIndex + 1] != ':')
            {
                return false;
            }

            host = endpoint[1..closingBracketIndex];
            portPart = endpoint[(closingBracketIndex + 2)..];
        }
        else
        {
            var separatorIndex = endpoint.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == endpoint.Length - 1)
            {
                return false;
            }

            host = endpoint[..separatorIndex];
            portPart = endpoint[(separatorIndex + 1)..];
        }

        return int.TryParse(portPart, out port) && port is > 0 and <= ushort.MaxValue;
    }

    private static bool IsSafeLocalAddress(System.Net.IPAddress address)
    {
        if (System.Net.IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var ipv6Bytes = address.GetAddressBytes();
            var isUniqueLocal = ipv6Bytes.Length > 0 && (ipv6Bytes[0] & 0xFE) == 0xFC;
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || isUniqueLocal;
        }

        var ipv4Bytes = address.GetAddressBytes();
        return ipv4Bytes.Length == 4 &&
            (ipv4Bytes[0] == 10 ||
             (ipv4Bytes[0] == 172 && ipv4Bytes[1] >= 16 && ipv4Bytes[1] <= 31) ||
             (ipv4Bytes[0] == 192 && ipv4Bytes[1] == 168));
    }

    /// <summary>
    /// Safely logs certificate information without revealing private details.
    /// </summary>
    /// <param name="certificate">The certificate to log safely.</param>
    /// <returns>A privacy-safe representation of the certificate.</returns>
    public static string SafeCertificate(System.Security.Cryptography.X509Certificates.X509Certificate2? certificate)
    {
        if (certificate == null)
        {
            return "[null]";
        }

        var thumbprint = certificate.Thumbprint;

        // Show thumbprint (safe for correlation) but redact full subject
        if (string.IsNullOrEmpty(thumbprint))
        {
            return "[cert:unknown]";
        }

        return $"[cert:{thumbprint[..Math.Min(8, thumbprint.Length)]}...]";
    }

    /// <summary>
    /// Safely logs transport endpoint information.
    /// </summary>
    /// <param name="endpoint">The transport endpoint.</param>
    /// <returns>A privacy-safe representation of the endpoint.</returns>
    public static string SafeTransportEndpoint(TransportEndpoint? endpoint)
    {
        if (endpoint == null)
        {
            return "[null]";
        }

        var safeHost = SafeEndpoint(endpoint.Host);
        return $"{endpoint.TransportType}:{safeHost}:{endpoint.Port}";
    }

    /// <summary>
    /// Redacts sensitive data from logging arguments.
    /// </summary>
    /// <param name="args">The arguments to redact.</param>
    /// <returns>The redacted arguments.</returns>
    private static object?[] RedactSensitiveData(object?[] args)
    {
        if (args == null || args.Length == 0)
        {
            return Array.Empty<object?>();
        }

        var redacted = new object?[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            redacted[i] = RedactValue(args[i]);
        }

        return redacted;
    }

    /// <summary>
    /// Redacts a single value if it contains sensitive information.
    /// </summary>
    /// <param name="value">The value to redact.</param>
    /// <returns>The redacted value.</returns>
    private static object? RedactValue(object? value)
    {
        if (value == null)
        {
            return value;
        }

        var stringValue = value.ToString();
        if (string.IsNullOrEmpty(stringValue))
        {
            return value;
        }

        // Check if this looks like sensitive data
        var lowerValue = stringValue.ToLowerInvariant();

        // Redact if it contains sensitive keywords
        foreach (var keyword in SensitiveKeywords)
        {
            if (lowerValue.Contains(keyword))
            {
                return "[redacted]";
            }
        }

        // Redact if it looks like a private key (long hex/base64)
        if (stringValue.Length > 32 && IsHexOrBase64(stringValue))
        {
            return $"[redacted:{stringValue.Length}chars]";
        }

        // Redact if it looks like a full peer ID (long alphanumeric)
        if (stringValue.Length > 16 && stringValue.All(c => char.IsLetterOrDigit(c) || c == '-'))
        {
            return SafePeerId(stringValue);
        }

        return value;
    }

    /// <summary>
    /// Checks if a string appears to be hex or base64 encoded.
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>True if it appears to be encoded data.</returns>
    private static bool IsHexOrBase64(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        // Check for hex
        if (value.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
        {
            return true;
        }

        // Check for base64 (basic check)
        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a privacy-safe exception message.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>A safe exception message.</returns>
    public static string SafeException(Exception? exception)
    {
        if (exception == null)
        {
            return "[null]";
        }

        // Redact sensitive information from exception messages
        var message = RedactValue(exception.Message)?.ToString() ?? "Unknown error";

        // Include exception type but not full stack trace (too verbose)
        return $"{exception.GetType().Name}: {message}";
    }

    /// <summary>
    /// Logs connection establishment with privacy-safe information.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="transportType">The transport type.</param>
    public static void LogConnectionEstablished<T>(this ILogger<T> logger, string peerId, string endpoint, TransportType transportType)
    {
        logger.LogInformation("Connection established to peer {PeerId} via {Transport} at {Endpoint}",
            SafePeerId(peerId), transportType, SafeEndpoint(endpoint));
    }

    /// <summary>
    /// Logs connection failure with privacy-safe information.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="error">The error message.</param>
    public static void LogConnectionFailed<T>(this ILogger<T> logger, string peerId, string endpoint, string error)
    {
        logger.LogWarning("Connection failed to peer {PeerId} at {Endpoint}: {Error}",
            SafePeerId(peerId), SafeEndpoint(endpoint), SafeException(new Exception(error)));
    }

    /// <summary>
    /// Logs certificate validation with privacy-safe information.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="isValid">Whether the certificate is valid.</param>
    public static void LogCertificateValidation<T>(this ILogger<T> logger, string peerId, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate, bool isValid)
    {
        var level = isValid ? LogLevel.Debug : LogLevel.Warning;
        logger.Log(level, "Certificate validation for peer {PeerId}: {Certificate} - {Result}",
            SafePeerId(peerId), SafeCertificate(certificate), isValid ? "valid" : "invalid");
    }
}
