// <copyright file="HardeningValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.Security
{
    using System;
    using System.Linq;
    using Serilog;
    using slskd;

    /// <summary>
    ///     Validates startup configuration for dangerous combinations when EnforceSecurity is enabled.
    ///     Fails fast before the host is built.
    /// </summary>
    /// <remarks>
    ///     PR-01: Enforce switch and startup HardeningValidator. When Web.EnforceSecurity is true,
    ///     refuses to start when: auth disabled + non-loopback + !AllowRemoteNoAuth; CORS credentials + wildcard;
    ///     memory dump allowed while auth disabled.
    /// </remarks>
    public static class HardeningValidator
    {
        /// <summary>
        ///     Rule name for auth disabled with non-loopback bind and AllowRemoteNoAuth false.
        /// </summary>
        public const string RuleAuthDisabledNonLoopback = "AuthDisabledNonLoopback";

        /// <summary>
        ///     Rule name for auth disabled with remote no-auth enabled but no CIDR restrictions.
        /// </summary>
        public const string RuleRemoteNoAuthWithoutCidrs = "RemoteNoAuthWithoutCidrs";

        /// <summary>
        ///     Rule name for CORS AllowCredentials with wildcard/any origin.
        /// </summary>
        public const string RuleCorsCredentialsWithWildcard = "CorsCredentialsWithWildcard";

        /// <summary>
        ///     Rule name for memory dump allowed while authentication is disabled.
        /// </summary>
        public const string RuleMemoryDumpWithAuthDisabled = "MemoryDumpWithAuthDisabled";

        /// <summary>
        ///     Rule name for HashFromAudioFileEnabled when the gated feature is unavailable.
        ///     §11: Enabling an incomplete feature must fail at startup when EnforceSecurity.
        /// </summary>
        public const string RuleHashFromAudioFileEnabled = "HashFromAudioFileEnabled";

        /// <summary>
        ///     Rule name for weak/empty Prometheus metrics password.
        /// </summary>
        public const string RuleWeakMetricsPassword = "WeakMetricsPassword";

        /// <summary>
        ///     Validates options and throws <see cref="HardeningValidationException"/> when EnforceSecurity is on
        ///     and a dangerous configuration is detected.
        /// </summary>
        /// <param name="options">Startup options (must be validated already).</param>
        /// <param name="environment">Environment name (reserved for future use).</param>
        /// <param name="isBindingNonLoopback">True when the app binds to non-loopback (e.g. IPAddress.Any).</param>
        /// <exception cref="HardeningValidationException">Thrown when EnforceSecurity is on and a rule fails.</exception>
        public static void Validate(OptionsAtStartup options, string environment, bool isBindingNonLoopback)
        {
            if (options?.Web == null)
                return;

            var enforce = options.Web.EnforceSecurity;

            // 1. Auth disabled + non-loopback + !AllowRemoteNoAuth
            if (options.Web.Authentication.Disabled && isBindingNonLoopback && !options.Web.AllowRemoteNoAuth)
            {
                const string msg = "Authentication is disabled and the application binds to a non-loopback address. Set Web.AllowRemoteNoAuth=true to allow, or bind to loopback only.";
                if (enforce)
                    throw new HardeningValidationException(RuleAuthDisabledNonLoopback, msg);
                else
                    Log.Warning("[{Rule}] {Message}", RuleAuthDisabledNonLoopback, msg);
            }

            if (options.Web.Authentication.Disabled && options.Web.AllowRemoteNoAuth &&
                string.IsNullOrWhiteSpace(options.Web.Authentication.Passthrough?.AllowedCidrs))
            {
                const string msg = "Web.AllowRemoteNoAuth is enabled without Web.Authentication.Passthrough.AllowedCidrs. Remote no-auth access must be constrained to explicit CIDRs.";
                if (enforce)
                    throw new HardeningValidationException(RuleRemoteNoAuthWithoutCidrs, msg);
                else
                    Log.Warning("[{Rule}] {Message}", RuleRemoteNoAuthWithoutCidrs, msg);
            }

            // 2. CORS: when Enabled, forbid AllowCredentials with wildcard/any origin. When !Enabled, no CORS middleware (PR-04) so skip.
            if (options.Web.Cors?.Enabled == true)
            {
                var cred = options.Web.Cors.AllowCredentials;
                var origins = options.Web.Cors.AllowedOrigins;
                var anyOrigin = origins == null || origins.Length == 0 ||
                    origins.Any(o => string.Equals(o, "*", StringComparison.OrdinalIgnoreCase));
                if (cred && anyOrigin)
                {
                    const string msg = "CORS is configured with AllowCredentials and wildcard/any origin, which is unsafe. Use an explicit AllowedOrigins list and no wildcard.";
                    if (enforce)
                        throw new HardeningValidationException(RuleCorsCredentialsWithWildcard, msg);
                    else
                        Log.Warning("[{Rule}] {Message}", RuleCorsCredentialsWithWildcard, msg);
                }
            }

            // 3. Memory dump allowed while auth disabled
            if ((options.Diagnostics?.AllowMemoryDump ?? false) && options.Web.Authentication.Disabled)
            {
                const string msg = "Diagnostics.AllowMemoryDump is true while authentication is disabled. Enable authentication or set AllowMemoryDump=false.";
                if (enforce)
                    throw new HardeningValidationException(RuleMemoryDumpWithAuthDisabled, msg);
                else
                    Log.Warning("[{Rule}] {Message}", RuleMemoryDumpWithAuthDisabled, msg);
            }

            // 4. LOW-05: Prometheus/metrics endpoint has a weak or empty password
            var metrics = options.Metrics;
            var metricsAuth = metrics?.Authentication;
            if (metrics?.Enabled == true && metricsAuth != null && !metricsAuth.Disabled &&
                string.IsNullOrWhiteSpace(metricsAuth.Password))
            {
                const string msg = "Web.Authentication.Metrics.Password is empty. " +
                    "The Prometheus metrics endpoint will be protected with no password. " +
                    "Set a strong password via web.authentication.metrics.password or disable the metrics endpoint.";
                if (enforce)
                    throw new HardeningValidationException(RuleWeakMetricsPassword, msg);
                else
                    Log.Warning("[{Rule}] {Message}", RuleWeakMetricsPassword, msg);
            }

            // 5. §11: HashFromAudioFileEnabled requires PCM extraction support from FFmpeg/NAudio.
            if (options.Flags?.HashFromAudioFileEnabled == true)
            {
                const string msg = "Flags.HashFromAudioFileEnabled is true but audio hash from file requires unavailable PCM extraction support. Set to false unless FFmpeg/NAudio integration is available.";
                if (enforce)
                    throw new HardeningValidationException(RuleHashFromAudioFileEnabled, msg);
                else
                    Log.Warning("[{Rule}] {Message}", RuleHashFromAudioFileEnabled, msg);
            }
        }
    }

    /// <summary>
    ///     Thrown when <see cref="HardeningValidator.Validate"/> detects a dangerous configuration and EnforceSecurity is on.
    /// </summary>
    public sealed class HardeningValidationException : Exception
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="HardeningValidationException"/> class.
        /// </summary>
        /// <param name="ruleName">Identifies the hardening rule that failed.</param>
        /// <param name="message">Human-readable description.</param>
        public HardeningValidationException(string ruleName, string message)
            : base($"[{ruleName}] {message}")
        {
            RuleName = ruleName;
        }

        /// <summary>
        ///     Gets the rule that failed (e.g. AuthDisabledNonLoopback, CorsCredentialsWithWildcard, MemoryDumpWithAuthDisabled).
        /// </summary>
        public string RuleName { get; }
    }
}
