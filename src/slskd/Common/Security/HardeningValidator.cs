// <copyright file="HardeningValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System;
    using System.Linq;
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
        ///     Rule name for CORS AllowCredentials with wildcard/any origin.
        /// </summary>
        public const string RuleCorsCredentialsWithWildcard = "CorsCredentialsWithWildcard";

        /// <summary>
        ///     Rule name for memory dump allowed while authentication is disabled.
        /// </summary>
        public const string RuleMemoryDumpWithAuthDisabled = "MemoryDumpWithAuthDisabled";

        /// <summary>
        ///     Rule name for HashFromAudioFileEnabled when the feature is not implemented.
        ///     §11: Enabling an incomplete feature must fail at startup when EnforceSecurity.
        /// </summary>
        public const string RuleHashFromAudioFileEnabled = "HashFromAudioFileEnabled";

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
            if (options?.Web == null || !options.Web.EnforceSecurity)
                return;

            // 1. Auth disabled + non-loopback + !AllowRemoteNoAuth
            if (options.Web.Authentication.Disabled && isBindingNonLoopback && !options.Web.AllowRemoteNoAuth)
            {
                throw new HardeningValidationException(
                    RuleAuthDisabledNonLoopback,
                    "Authentication is disabled and the application binds to a non-loopback address. Set Web.AllowRemoteNoAuth=true to allow, or bind to loopback only.");
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
                    throw new HardeningValidationException(
                        RuleCorsCredentialsWithWildcard,
                        "CORS is configured with AllowCredentials and wildcard/any origin, which is unsafe. Use an explicit AllowedOrigins list and no wildcard.");
                }
            }

            // 3. Memory dump allowed while auth disabled
            if ((options.Diagnostics?.AllowMemoryDump ?? false) && options.Web.Authentication.Disabled)
            {
                throw new HardeningValidationException(
                    RuleMemoryDumpWithAuthDisabled,
                    "Diagnostics.AllowMemoryDump is true while authentication is disabled. Enable authentication or set AllowMemoryDump=false.");
            }

            // 4. §11: HashFromAudioFileEnabled — feature not implemented (PCM extraction requires FFmpeg/NAudio)
            if (options.Flags?.HashFromAudioFileEnabled == true)
            {
                throw new HardeningValidationException(
                    RuleHashFromAudioFileEnabled,
                    "Flags.HashFromAudioFileEnabled is true but audio hash from file is not implemented. PCM extraction requires FFmpeg/NAudio integration. Set to false or implement the feature.");
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
