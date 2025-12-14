// <copyright file="LoggingSanitizer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System;
    using System.Net;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    ///     Provides safe logging utilities that sanitize sensitive information.
    /// </summary>
    /// <remarks>
    ///     H-GLOBAL01: Logging and Telemetry Hygiene Audit.
    ///     Ensures sensitive data like full paths, IP addresses, and external identifiers
    ///     are never logged in plain text. Provides sanitized alternatives.
    /// </remarks>
    public static class LoggingSanitizer
    {
        /// <summary>
        ///     Sanitizes a file path for safe logging by truncating directory components.
        /// </summary>
        /// <param name="path">The full file path.</param>
        /// <returns>A sanitized path showing only filename and extension.</returns>
        /// <example>
        ///     "/home/user/documents/secret.pdf" → "secret.pdf"
        ///     "C:\Users\user\Desktop\confidential.docx" → "confidential.docx"
        /// </example>
        public static string SanitizeFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "[empty]";
            }

            try
            {
                return System.IO.Path.GetFileName(path);
            }
            catch
            {
                // If path parsing fails, return a generic placeholder
                return "[invalid-path]";
            }
        }

        /// <summary>
        ///     Sanitizes an IP address for safe logging by hashing it.
        /// </summary>
        /// <param name="ipAddress">The IP address to sanitize.</param>
        /// <returns>A hashed representation of the IP address.</returns>
        /// <example>
        ///     "192.168.1.100" → "a1b2c3d4e5f6..."
        /// </example>
        public static string SanitizeIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return "[empty]";
            }

            try
            {
                // Hash the IP address to prevent correlation while maintaining uniqueness
                using var sha256 = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(ipAddress);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
            }
            catch
            {
                return "[invalid-ip]";
            }
        }

        /// <summary>
        ///     Sanitizes an IP address for safe logging by hashing it.
        /// </summary>
        /// <param name="ipAddress">The IP address to sanitize.</param>
        /// <returns>A hashed representation of the IP address.</returns>
        public static string SanitizeIpAddress(IPAddress? ipAddress)
        {
            if (ipAddress == null)
            {
                return "[null]";
            }

            return SanitizeIpAddress(ipAddress.ToString());
        }

        /// <summary>
        ///     Sanitizes a username or external identifier for safe logging.
        /// </summary>
        /// <param name="identifier">The username or external identifier.</param>
        /// <returns>A sanitized version showing length and first character only.</returns>
        /// <example>
        ///     "john_doe_12345" → "j**** (13 chars)"
        /// </example>
        public static string SanitizeExternalIdentifier(string? identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return "[empty]";
            }

            if (identifier.Length <= 2)
            {
                return $"{identifier[0]}* ({identifier.Length} chars)";
            }

            return $"{identifier[0]}***{identifier[^1]} ({identifier.Length} chars)";
        }

        /// <summary>
        ///     Sanitizes a content hash for safe logging by truncating it.
        /// </summary>
        /// <param name="hash">The full hash string.</param>
        /// <returns>A truncated hash showing first and last 8 characters.</returns>
        /// <example>
        ///     "a1b2c3d4e5f678901234567890abcdef1234567890abcdef" → "a1b2c3d4...bcdef123"
        /// </example>
        public static string SanitizeHash(string? hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
            {
                return "[empty]";
            }

            if (hash.Length <= 16)
            {
                return hash;
            }

            return $"{hash.Substring(0, 8)}...{hash.Substring(hash.Length - 8)}";
        }

        /// <summary>
        ///     Sanitizes a URL for safe logging by removing sensitive components.
        /// </summary>
        /// <param name="url">The full URL.</param>
        /// <returns>A sanitized URL showing only scheme and hostname.</returns>
        /// <example>
        ///     "https://api.example.com/users/12345/profile?token=secret" → "https://api.example.com"
        /// </example>
        public static string SanitizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return "[empty]";
            }

            try
            {
                var uri = new Uri(url);
                return $"{uri.Scheme}://{uri.Host}";
            }
            catch
            {
                return "[invalid-url]";
            }
        }

        /// <summary>
        ///     Sanitizes arbitrary sensitive data by replacing it with a placeholder.
        /// </summary>
        /// <param name="data">The sensitive data.</param>
        /// <returns>A placeholder indicating sensitive data was present.</returns>
        public static string SanitizeSensitiveData(string? data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return "[empty]";
            }

            return $"[redacted-{data.Length}-chars]";
        }

        /// <summary>
        ///     Creates a safe logging context that can be used with structured logging.
        /// </summary>
        /// <param name="contextName">Name of the context (e.g., "user", "file", "peer").</param>
        /// <param name="identifier">The identifier to sanitize.</param>
        /// <returns>A safe context object for logging.</returns>
        public static object SafeContext(string contextName, string identifier)
        {
            return new { Context = contextName, Id = SanitizeExternalIdentifier(identifier) };
        }
    }
}
