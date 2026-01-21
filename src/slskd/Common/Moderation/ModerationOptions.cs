// <copyright file="ModerationOptions.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Options for the Moderation / Control Plane (MCP) system.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         🔒 MANDATORY: See `MCP-HARDENING.md` before configuring.
    ///     </para>
    ///     <para>
    ///         MCP provides content and peer moderation through:
    ///         - Hash blocklists
    ///         - External moderation services (opt-in)
    ///         - Peer reputation tracking
    ///     </para>
    /// </remarks>
    public class ModerationOptions : IValidatableObject
    {
        /// <summary>
        ///     Gets a value indicating whether MCP is enabled.
        /// </summary>
        [Argument(default, "moderation-enabled")]
        [EnvironmentVariable("MODERATION_ENABLED")]
        [Description("enable Moderation / Control Plane (MCP)")]
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Gets the failsafe mode when providers fail.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Allowed values:
        ///         - "block" (default): Block content on provider error (conservative)
        ///         - "allow": Continue to next provider on error (permissive)
        ///     </para>
        ///     <para>
        ///         🔒 SECURITY: "block" mode is safer but may cause false positives
        ///         during provider outages. Choose based on risk tolerance.
        ///     </para>
        /// </remarks>
        [Argument(default, "moderation-failsafe-mode")]
        [EnvironmentVariable("MODERATION_FAILSAFE_MODE")]
        [Description("failsafe mode when providers fail: block or allow")]
        public string FailsafeMode { get; init; } = "block";

        /// <summary>
        ///     Gets hash blocklist options.
        /// </summary>
        public HashBlocklistOptions HashBlocklist { get; init; } = new HashBlocklistOptions();

        /// <summary>
        ///     Gets external moderation client options.
        /// </summary>
        public ExternalModerationOptions ExternalModeration { get; init; } = new ExternalModerationOptions();

        /// <summary>
        ///     Gets peer reputation options.
        /// </summary>
        public ReputationOptions Reputation { get; init; } = new ReputationOptions();

        /// <summary>
        ///     Gets LLM moderation options.
        /// </summary>
        /// <remarks>
        ///     T-MCP-LM01: Configuration for AI-powered content moderation.
        /// </remarks>
        public LlmModerationOptions LlmModeration { get; init; } = new LlmModerationOptions();

        /// <inheritdoc />
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (FailsafeMode != "block" && FailsafeMode != "allow")
            {
                results.Add(new ValidationResult(
                    $"FailsafeMode must be 'block' or 'allow', got: {FailsafeMode}",
                    new[] { nameof(FailsafeMode) }));
            }

            return results;
        }

        /// <summary>
        ///     Hash blocklist options.
        /// </summary>
        public class HashBlocklistOptions
        {
            /// <summary>
            ///     Gets a value indicating whether hash blocklist checking is enabled.
            /// </summary>
            [Argument(default, "moderation-hash-blocklist-enabled")]
            [EnvironmentVariable("MODERATION_HASH_BLOCKLIST_ENABLED")]
            [Description("enable hash blocklist checking")]
            public bool Enabled { get; init; } = false;

            /// <summary>
            ///     Gets the path to the hash blocklist file.
            /// </summary>
            /// <remarks>
            ///     Format: One SHA256 hash per line (hex-encoded).
            ///     🔒 SECURITY: This file must be stored securely (restricted permissions).
            /// </remarks>
            [Argument(default, "moderation-hash-blocklist-path")]
            [EnvironmentVariable("MODERATION_HASH_BLOCKLIST_PATH")]
            [Description("path to hash blocklist file")]
            public string? BlocklistPath { get; init; }
        }

        /// <summary>
        ///     External moderation service options.
        /// </summary>
        public class ExternalModerationOptions : IValidatableObject
        {
            /// <summary>
            ///     Gets the external moderation mode.
            /// </summary>
            /// <remarks>
            ///     T-MCP-LM03: Mode selection for LLM client types.
            ///     - Off: No external moderation
            ///     - Local: Use local LLM service (HTTP to localhost/local network)
            ///     - Remote: Use remote LLM API (HTTPS with domain allowlist)
            /// </remarks>
            [Argument(default, "moderation-external-mode")]
            [EnvironmentVariable("MODERATION_EXTERNAL_MODE")]
            [Description("external moderation mode: Off, Local, or Remote")]
            public string Mode { get; init; } = "Off";

            /// <summary>
            ///     Gets a value indicating whether external moderation is enabled.
            /// </summary>
            /// <remarks>
            ///     Computed from Mode - enabled when Mode is not "Off".
            /// </remarks>
            public bool Enabled => !string.Equals(Mode, "Off", StringComparison.OrdinalIgnoreCase);

            /// <summary>
            ///     Gets the external service endpoint.
            /// </summary>
            [Argument(default, "moderation-external-endpoint")]
            [EnvironmentVariable("MODERATION_EXTERNAL_ENDPOINT")]
            [Description("external moderation service HTTPS endpoint")]
            public string? Endpoint { get; init; }

            /// <summary>
            ///     Gets the allowed domains for external moderation.
            /// </summary>
            /// <remarks>
            ///     🔒 CRITICAL (MCP-HARDENING.md Section 2.1.1):
            ///     - Only HTTPS endpoints in this list are allowed
            ///     - No wildcards
            ///     - No IP addresses
            ///     Example: ["trusted-moderator.com", "moderation-api.example.net"]
            /// </remarks>
            public string[] AllowedDomains { get; init; } = Array.Empty<string>();

            /// <summary>
            ///     Gets the request timeout in seconds.
            /// </summary>
            [Argument(default, "moderation-external-timeout-seconds")]
            [EnvironmentVariable("MODERATION_EXTERNAL_TIMEOUT_SECONDS")]
            [Description("external moderation request timeout (seconds)")]
            [Range(1, 60)]
            public int TimeoutSeconds { get; init; } = 5;

            /// <inheritdoc />
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var results = new List<ValidationResult>();

                // Validate mode
                var validModes = new[] { "Off", "Local", "Remote" };
                if (!validModes.Contains(Mode, StringComparer.OrdinalIgnoreCase))
                {
                    results.Add(new ValidationResult(
                        $"Mode must be one of: {string.Join(", ", validModes)}, got: {Mode}",
                        new[] { nameof(Mode) }));
                }

                if (Enabled)
                {
                    if (string.IsNullOrWhiteSpace(Endpoint))
                    {
                        results.Add(new ValidationResult(
                            $"External moderation mode '{Mode}' requires an Endpoint.",
                            new[] { nameof(Endpoint) }));
                    }
                    else if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri))
                    {
                        results.Add(new ValidationResult(
                            $"Endpoint must be an absolute URI, got: {Endpoint}",
                            new[] { nameof(Endpoint) }));
                    }
                    else if (string.Equals(Mode, "Remote", StringComparison.OrdinalIgnoreCase) && uri.Scheme != "https")
                    {
                        results.Add(new ValidationResult(
                            $"Remote mode requires HTTPS endpoint, got: {Endpoint}",
                            new[] { nameof(Endpoint) }));
                    }

                    // Domain validation based on mode
                    if (string.Equals(Mode, "Remote", StringComparison.OrdinalIgnoreCase))
                    {
                        if (AllowedDomains.Length == 0)
                        {
                            results.Add(new ValidationResult(
                                "Remote mode requires AllowedDomains for SSRF protection.",
                                new[] { nameof(AllowedDomains) }));
                        }
                    }
                    else if (string.Equals(Mode, "Local", StringComparison.OrdinalIgnoreCase))
                    {
                        // For local mode, AllowedDomains is optional (defaults to localhost)
                        // but if specified, validate them
                        foreach (var domain in AllowedDomains)
                        {
                            if (string.IsNullOrWhiteSpace(domain) || domain.Contains("*") || IPAddress.TryParse(domain, out _))
                            {
                                results.Add(new ValidationResult(
                                    $"Local mode AllowedDomains should not contain wildcards or IP addresses: {domain}",
                                    new[] { nameof(AllowedDomains) }));
                            }
                        }
                    }
                }

                return results;
            }
        }

        /// <summary>
        ///     Peer reputation options.
        /// </summary>
        public class ReputationOptions
        {
            /// <summary>
            ///     Gets a value indicating whether peer reputation tracking is enabled.
            /// </summary>
            [Argument(default, "moderation-reputation-enabled")]
            [EnvironmentVariable("MODERATION_REPUTATION_ENABLED")]
            [Description("enable peer reputation tracking")]
            public bool Enabled { get; init; } = false;

            /// <summary>
            ///     Gets the ban threshold (negative events before ban).
            /// </summary>
            [Argument(default, "moderation-reputation-ban-threshold")]
            [EnvironmentVariable("MODERATION_REPUTATION_BAN_THRESHOLD")]
            [Description("negative events threshold for peer ban")]
            [Range(1, 1000)]
            public int BanThreshold { get; init; } = 10;

            /// <summary>
            ///     Gets the reputation decay period in days.
            /// </summary>
            /// <remarks>
            ///     🔒 MANDATORY (MCP-HARDENING.md Section 3.3):
            ///     Reputation must decay over time to prevent permanent bans.
            ///     After this period, reputation resets to neutral.
            /// </remarks>
            [Argument(default, "moderation-reputation-decay-days")]
            [EnvironmentVariable("MODERATION_REPUTATION_DECAY_DAYS")]
            [Description("days before reputation decays to neutral")]
            [Range(1, 365)]
            public int DecayDays { get; init; } = 30;
        }
    }
}

