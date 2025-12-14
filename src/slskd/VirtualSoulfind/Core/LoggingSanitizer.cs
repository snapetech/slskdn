// <copyright file="LoggingSanitizer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core
{
    /// <summary>
    /// Utility for sanitizing sensitive data in logs.
    /// </summary>
    public static class LoggingSanitizer
    {
        /// <summary>
        /// Sanitizes a string for logging by redacting sensitive information.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>Sanitized string safe for logging.</returns>
        public static string? Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            // Redact sensitive patterns
            // Example: API keys, tokens, passwords
            if (value.Length > 8)
            {
                return value.Substring(0, 4) + "..." + value.Substring(value.Length - 4);
            }

            return "***";
        }
    }
}

