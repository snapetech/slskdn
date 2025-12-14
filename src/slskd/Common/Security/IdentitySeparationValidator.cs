// <copyright file="IdentitySeparationValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Extensions.Logging;

    /// <summary>
    ///     Validates and audits identity separation throughout the system.
    /// </summary>
    /// <remarks>
    ///     H-ID01: Identity Separation Enforcement.
    ///     Provides validation and auditing capabilities to ensure different
    ///     identity types remain properly separated.
    /// </remarks>
    public static class IdentitySeparationValidator
    {
        /// <summary>
        ///     Validates a collection of identities for cross-contamination.
        /// </summary>
        /// <param name="identities">Dictionary of identity contexts to their values.</param>
        /// <param name="logger">Optional logger for audit messages.</param>
        /// <returns>Validation result with any violations found.</returns>
        public static IdentityValidationResult ValidateIdentities(
            IReadOnlyDictionary<string, string> identities,
            ILogger? logger = null)
        {
            var violations = new List<IdentityViolation>();

            // Check each identity against all others for cross-contamination
            foreach (var (context1, identity1) in identities)
            {
                foreach (var (context2, identity2) in identities)
                {
                    if (context1 == context2)
                    {
                        continue;
                    }

                    var forbiddenTypes = GetForbiddenTypesForContext(context1);
                    if (IdentitySeparationEnforcer.HasCrossContamination(identity1, forbiddenTypes))
                    {
                        var violation = new IdentityViolation
                        {
                            Context = context1,
                            Identity = identity1,
                            ViolatedContexts = new[] { context2 },
                            DetectedTypes = new[] { IdentitySeparationEnforcer.DetectIdentityType(identity1) ?? IdentitySeparationEnforcer.IdentityType.Mesh }
                        };

                        violations.Add(violation);

                        logger?.LogWarning(
                            "[IdentityAudit] Cross-contamination detected: {Context} identity '{Identity}' matches forbidden type from {ViolatedContext}",
                            context1, LoggingSanitizer.SanitizeSensitiveData(identity1), context2);
                    }
                }
            }

            return new IdentityValidationResult
            {
                IsValid = violations.Count == 0,
                Violations = violations
            };
        }

        /// <summary>
        ///     Audits pod peer IDs for identity leakage.
        /// </summary>
        /// <param name="podPeerIds">Collection of pod peer IDs to audit.</param>
        /// <param name="logger">Optional logger for audit messages.</param>
        /// <returns>Audit result with any unsafe peer IDs.</returns>
        public static PodPeerIdAuditResult AuditPodPeerIds(
            IEnumerable<string> podPeerIds,
            ILogger? logger = null)
        {
            var unsafeIds = new List<string>();
            var stats = new Dictionary<string, int>();

            foreach (var peerId in podPeerIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            {
                if (!IdentitySeparationEnforcer.IsSafePodPeerId(peerId))
                {
                    unsafeIds.Add(peerId);

                    var detectedType = IdentitySeparationEnforcer.DetectIdentityType(peerId);
                    var typeName = detectedType?.ToString() ?? "Unknown";
                    stats[typeName] = stats.GetValueOrDefault(typeName, 0) + 1;

                    logger?.LogWarning(
                        "[IdentityAudit] Unsafe pod peer ID detected: {PeerId} (type: {DetectedType})",
                        LoggingSanitizer.SanitizeSensitiveData(peerId), typeName);
                }
            }

            return new PodPeerIdAuditResult
            {
                TotalAudited = podPeerIds.Count(),
                UnsafeCount = unsafeIds.Count,
                UnsafeIds = unsafeIds,
                ViolationStats = stats
            };
        }

        /// <summary>
        ///     Gets the identity types that are forbidden for a given context.
        /// </summary>
        private static IdentitySeparationEnforcer.IdentityType[] GetForbiddenTypesForContext(string context)
        {
            return context.ToLowerInvariant() switch
            {
                "mesh" => new[] { IdentitySeparationEnforcer.IdentityType.Soulseek, IdentitySeparationEnforcer.IdentityType.LocalUser },
                "soulseek" => new[] { IdentitySeparationEnforcer.IdentityType.Mesh, IdentitySeparationEnforcer.IdentityType.Pod },
                "pod" => new[] { IdentitySeparationEnforcer.IdentityType.Soulseek, IdentitySeparationEnforcer.IdentityType.LocalUser },
                "localuser" => new[] { IdentitySeparationEnforcer.IdentityType.Mesh, IdentitySeparationEnforcer.IdentityType.Soulseek },
                _ => Array.Empty<IdentitySeparationEnforcer.IdentityType>()
            };
        }
    }

    /// <summary>
    ///     Result of identity validation.
    /// </summary>
    public sealed class IdentityValidationResult
    {
        /// <summary>
        ///     Gets a value indicating whether all identities are properly separated.
        /// </summary>
        public bool IsValid { get; init; }

        /// <summary>
        ///     Gets the list of identity violations found.
        /// </summary>
        public IReadOnlyList<IdentityViolation> Violations { get; init; } = Array.Empty<IdentityViolation>();
    }

    /// <summary>
    ///     Represents an identity separation violation.
    /// </summary>
    public sealed class IdentityViolation
    {
        /// <summary>
        ///     Gets the context where the violation occurred.
        /// </summary>
        public string? Context { get; init; }

        /// <summary>
        ///     Gets the identity value that caused the violation.
        /// </summary>
        public string? Identity { get; init; }

        /// <summary>
        ///     Gets the contexts that were violated.
        /// </summary>
        public string[]? ViolatedContexts { get; init; }

        /// <summary>
        ///     Gets the detected identity types.
        /// </summary>
        public IdentitySeparationEnforcer.IdentityType[]? DetectedTypes { get; init; }
    }

    /// <summary>
    ///     Result of pod peer ID audit.
    /// </summary>
    public sealed class PodPeerIdAuditResult
    {
        /// <summary>
        ///     Gets the total number of peer IDs audited.
        /// </summary>
        public int TotalAudited { get; init; }

        /// <summary>
        ///     Gets the number of unsafe peer IDs found.
        /// </summary>
        public int UnsafeCount { get; init; }

        /// <summary>
        ///     Gets the list of unsafe peer IDs.
        /// </summary>
        public IReadOnlyList<string> UnsafeIds { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets statistics about violation types.
        /// </summary>
        public IReadOnlyDictionary<string, int> ViolationStats { get; init; } = new Dictionary<string, int>();
    }
}


