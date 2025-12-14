// <copyright file="IdentityConfigurationAuditor.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Extensions.Logging;
    using slskd.Core;

    /// <summary>
    ///     Audits identity configuration for proper separation.
    /// </summary>
    /// <remarks>
    ///     H-ID01: Identity Separation Enforcement.
    ///     Validates that identity configurations don't mix different identity types.
    /// </remarks>
    public static class IdentityConfigurationAuditor
    {
        /// <summary>
        ///     Audits the current system configuration for identity separation issues.
        /// </summary>
        /// <param name="options">The current system options.</param>
        /// <param name="logger">Logger for audit messages.</param>
        /// <returns>Audit result with any configuration violations.</returns>
        public static IdentityConfigurationAuditResult AuditConfiguration(Options options, ILogger logger)
        {
            var violations = new List<IdentityConfigurationViolation>();

            // Audit Soulseek configuration
            AuditSoulseekConfiguration(options.Soulseek, violations, logger);

            // Audit web authentication configuration
            AuditWebConfiguration(options.Web, violations, logger);

            // Audit metrics configuration
            AuditMetricsConfiguration(options.Metrics, violations, logger);

            // Audit proxy configurations
            AuditProxyConfigurations(options.Soulseek, violations, logger);

            return new IdentityConfigurationAuditResult
            {
                IsCompliant = violations.Count == 0,
                Violations = violations,
                AuditedAt = DateTimeOffset.UtcNow
            };
        }

        /// <summary>
        ///     Audits Soulseek identity configuration.
        /// </summary>
        private static void AuditSoulseekConfiguration(
            Options.SoulseekOptions soulseekOptions,
            List<IdentityConfigurationViolation> violations,
            ILogger logger)
        {
            // Check that Soulseek credentials don't match other identity types
            var soulseekUsername = soulseekOptions.Username;
            if (!string.IsNullOrWhiteSpace(soulseekUsername))
            {
                // Should not look like other identity types
                if (IdentitySeparationEnforcer.IsValidIdentityFormat(soulseekUsername, IdentitySeparationEnforcer.IdentityType.Mesh) ||
                    IdentitySeparationEnforcer.IsValidIdentityFormat(soulseekUsername, IdentitySeparationEnforcer.IdentityType.Pod) ||
                    IdentitySeparationEnforcer.IsValidIdentityFormat(soulseekUsername, IdentitySeparationEnforcer.IdentityType.LocalUser))
                {
                    violations.Add(new IdentityConfigurationViolation
                    {
                        Category = "Soulseek",
                        Issue = "Username resembles other identity type",
                        Value = LoggingSanitizer.SanitizeExternalIdentifier(soulseekUsername),
                        Recommendation = "Use Soulseek-specific username format"
                    });

                    logger.LogWarning(
                        "[IdentityAudit] Soulseek username '{SanitizedUsername}' resembles other identity type",
                        LoggingSanitizer.SanitizeExternalIdentifier(soulseekUsername));
                }
            }

            // Check proxy configuration for Soulseek
            if (soulseekOptions.Connection.Proxy.Enabled)
            {
                var proxyUsername = soulseekOptions.Connection.Proxy.Username;
                if (!string.IsNullOrWhiteSpace(proxyUsername))
                {
                    if (IdentitySeparationEnforcer.IsValidIdentityFormat(proxyUsername, IdentitySeparationEnforcer.IdentityType.Soulseek))
                    {
                        violations.Add(new IdentityConfigurationViolation
                        {
                            Category = "Proxy",
                            Issue = "Proxy username matches Soulseek identity",
                            Value = LoggingSanitizer.SanitizeExternalIdentifier(proxyUsername),
                            Recommendation = "Use distinct proxy credentials"
                        });

                        logger.LogWarning(
                            "[IdentityAudit] Proxy username matches Soulseek identity: {SanitizedUsername}",
                            LoggingSanitizer.SanitizeExternalIdentifier(proxyUsername));
                    }
                }
            }
        }

        /// <summary>
        ///     Audits web authentication configuration.
        /// </summary>
        private static void AuditWebConfiguration(
            Options.WebOptions webOptions,
            List<IdentityConfigurationViolation> violations,
            ILogger logger)
        {
            var webUsername = webOptions.Authentication.Username;
            if (!string.IsNullOrWhiteSpace(webUsername))
            {
                // Web username should not match Soulseek or look like other identity types
                if (IdentitySeparationEnforcer.IsValidIdentityFormat(webUsername, IdentitySeparationEnforcer.IdentityType.Soulseek) ||
                    IdentitySeparationEnforcer.IsValidIdentityFormat(webUsername, IdentitySeparationEnforcer.IdentityType.Mesh))
                {
                    violations.Add(new IdentityConfigurationViolation
                    {
                        Category = "WebAuth",
                        Issue = "Web username matches other identity type",
                        Value = LoggingSanitizer.SanitizeExternalIdentifier(webUsername),
                        Recommendation = "Use distinct web authentication credentials"
                    });

                    logger.LogWarning(
                        "[IdentityAudit] Web username matches other identity type: {SanitizedUsername}",
                        LoggingSanitizer.SanitizeExternalIdentifier(webUsername));
                }
            }
        }

        /// <summary>
        ///     Audits metrics authentication configuration.
        /// </summary>
        private static void AuditMetricsConfiguration(
            Options.MetricsOptions metricsOptions,
            List<IdentityConfigurationViolation> violations,
            ILogger logger)
        {
            var metricsUsername = metricsOptions.Authentication.Username;
            if (!string.IsNullOrWhiteSpace(metricsUsername))
            {
                // Metrics username should not match other identities
                if (IdentitySeparationEnforcer.IsValidIdentityFormat(metricsUsername, IdentitySeparationEnforcer.IdentityType.Soulseek) ||
                    IdentitySeparationEnforcer.IsValidIdentityFormat(metricsUsername, IdentitySeparationEnforcer.IdentityType.Mesh) ||
                    IdentitySeparationEnforcer.IsValidIdentityFormat(metricsUsername, IdentitySeparationEnforcer.IdentityType.LocalUser))
                {
                    violations.Add(new IdentityConfigurationViolation
                    {
                        Category = "Metrics",
                        Issue = "Metrics username matches other identity type",
                        Value = LoggingSanitizer.SanitizeExternalIdentifier(metricsUsername),
                        Recommendation = "Use distinct metrics authentication credentials"
                    });

                    logger.LogWarning(
                        "[IdentityAudit] Metrics username matches other identity type: {SanitizedUsername}",
                        LoggingSanitizer.SanitizeExternalIdentifier(metricsUsername));
                }
            }
        }

        /// <summary>
        ///     Audits proxy configurations for identity separation.
        /// </summary>
        private static void AuditProxyConfigurations(
            Options.SoulseekOptions soulseekOptions,
            List<IdentityConfigurationViolation> violations,
            ILogger logger)
        {
            // Check if proxy credentials are reused across different services
            var soulseekProxyUsername = soulseekOptions.Connection.Proxy.Username;
            var soulseekProxyPassword = soulseekOptions.Connection.Proxy.Password;

            // This is a simplified check - in practice, you'd compare against all proxy configs
            // For now, just check that proxy credentials don't match Soulseek login
            if (!string.IsNullOrWhiteSpace(soulseekProxyUsername) &&
                !string.IsNullOrWhiteSpace(soulseekOptions.Username))
            {
                if (soulseekProxyUsername == soulseekOptions.Username)
                {
                    violations.Add(new IdentityConfigurationViolation
                    {
                        Category = "Proxy",
                        Issue = "Proxy username matches Soulseek username",
                        Value = LoggingSanitizer.SanitizeExternalIdentifier(soulseekProxyUsername),
                        Recommendation = "Use separate credentials for proxy and Soulseek"
                    });

                    logger.LogWarning(
                        "[IdentityAudit] Proxy username matches Soulseek username: {SanitizedUsername}",
                        LoggingSanitizer.SanitizeExternalIdentifier(soulseekProxyUsername));
                }
            }
        }
    }

    /// <summary>
    ///     Result of identity configuration audit.
    /// </summary>
    public sealed class IdentityConfigurationAuditResult
    {
        /// <summary>
        ///     Gets a value indicating whether the configuration is compliant.
        /// </summary>
        public bool IsCompliant { get; init; }

        /// <summary>
        ///     Gets the list of configuration violations found.
        /// </summary>
        public IReadOnlyList<IdentityConfigurationViolation> Violations { get; init; } = Array.Empty<IdentityConfigurationViolation>();

        /// <summary>
        ///     Gets the timestamp when the audit was performed.
        /// </summary>
        public DateTimeOffset AuditedAt { get; init; }
    }

    /// <summary>
    ///     Represents an identity configuration violation.
    /// </summary>
    public sealed class IdentityConfigurationViolation
    {
        /// <summary>
        ///     Gets the configuration category (e.g., "Soulseek", "WebAuth").
        /// </summary>
        public string? Category { get; init; }

        /// <summary>
        ///     Gets the description of the issue.
        /// </summary>
        public string? Issue { get; init; }

        /// <summary>
        ///     Gets the problematic value (sanitized for logging).
        /// </summary>
        public string? Value { get; init; }

        /// <summary>
        ///     Gets the recommended fix.
        /// </summary>
        public string? Recommendation { get; init; }
    }
}
