// <copyright file="ExternalModerationOptions.cs" company="slskd Team">
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
    using System.Net;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

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
        ///     🔒 CRITICAL (docs/MCP-HARDENING.md Section 2.1.1):
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
}

