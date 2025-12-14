// <copyright file="MultiRealmConfig.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using slskd.Validation;

    /// <summary>
    ///     Multi-realm configuration for pods participating in multiple realms.
    /// </summary>
    /// <remarks>
    ///     T-REALM-02: MultiRealmConfig & Bridge Skeleton.
    ///     Enables controlled cross-realm communication through explicit bridging.
    /// </remarks>
    public class MultiRealmConfig
    {
        /// <summary>
        ///     Gets or sets the list of realm configurations.
        /// </summary>
        /// <remarks>
        ///     T-REALM-02: Each realm config defines a separate overlay network.
        ///     The pod will establish connections to each realm's overlay.
        /// </remarks>
        [MinLength(1, ErrorMessage = "At least one realm configuration is required.")]
        public RealmConfig[] Realms { get; set; } = new[]
        {
            new RealmConfig
            {
                Id = "default-realm-v1",
                GovernanceRoots = new[] { "default-governance-root" },
                BootstrapNodes = Array.Empty<string>(),
                Policies = new RealmPolicies()
            }
        };

        /// <summary>
        ///     Gets or sets the bridge configuration for cross-realm communication.
        /// </summary>
        /// <remarks>
        ///     T-REALM-02: Controls what can flow between realms.
        ///     When disabled, realms are completely isolated at application layer.
        /// </remarks>
        public BridgeConfig Bridge { get; set; } = new BridgeConfig();

        /// <summary>
        ///     Validates the multi-realm configuration.
        /// </summary>
        /// <returns>A list of validation errors, or empty if valid.</returns>
        public IEnumerable<ValidationResult> Validate()
        {
            var results = new List<ValidationResult>();

            // Validate realms array
            if (Realms == null || Realms.Length == 0)
            {
                results.Add(new ValidationResult("At least one realm configuration is required.", new[] { nameof(Realms) }));
                return results; // Can't validate further without realms
            }

            // Validate each realm config
            var realmIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < Realms.Length; i++)
            {
                var realm = Realms[i];
                if (realm == null)
                {
                    results.Add(new ValidationResult($"Realm configuration at index {i} is null.", new[] { $"{nameof(Realms)}[{i}]" }));
                    continue;
                }

                // Check for duplicate realm IDs
                if (!realmIds.Add(realm.Id))
                {
                    results.Add(new ValidationResult($"Duplicate realm ID '{realm.Id}' found.", new[] { $"{nameof(Realms)}[{i}].{nameof(RealmConfig.Id)}" }));
                }

                // Validate individual realm config
                var realmErrors = realm.Validate().ToList();
                foreach (var error in realmErrors)
                {
                    var memberNames = error.MemberNames.Select(name => $"{nameof(Realms)}[{i}].{name}").ToArray();
                    results.Add(new ValidationResult(error.ErrorMessage, memberNames));
                }
            }

            // Validate bridge configuration
            if (Bridge == null)
            {
                results.Add(new ValidationResult("Bridge configuration is required.", new[] { nameof(Bridge) }));
            }
            else
            {
                var bridgeErrors = Bridge.Validate().ToList();
                foreach (var error in bridgeErrors)
                {
                    var memberNames = error.MemberNames.Select(name => $"{nameof(Bridge)}.{name}").ToArray();
                    results.Add(new ValidationResult(error.ErrorMessage, memberNames));
                }
            }

            return results;
        }

        /// <summary>
        ///     Gets a value indicating whether this multi-realm configuration is valid.
        /// </summary>
        public bool IsValid => !Validate().Any();

        /// <summary>
        ///     Gets all realm IDs configured.
        /// </summary>
        public IReadOnlySet<string> RealmIds => Realms?.Select(r => r?.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        /// <summary>
        ///     Gets a realm configuration by ID.
        /// </summary>
        /// <param name="realmId">The realm ID to find.</param>
        /// <returns>The realm configuration, or null if not found.</returns>
        public RealmConfig? GetRealm(string realmId)
        {
            return Realms?.FirstOrDefault(r => r != null && string.Equals(r.Id, realmId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///     Checks if bridging is enabled between realms.
        /// </summary>
        public bool IsBridgingEnabled => Bridge?.Enabled == true;

        /// <summary>
        ///     Checks if a specific flow is allowed between realms.
        /// </summary>
        /// <param name="flow">The flow to check (e.g., "governance:read", "replication:write").</param>
        /// <returns>True if the flow is allowed.</returns>
        public bool IsFlowAllowed(string flow)
        {
            if (Bridge == null)
            {
                return false;
            }

            // If bridging is disabled, no flows are allowed
            if (!Bridge.Enabled)
            {
                return false;
            }

            // Check if flow is explicitly disallowed
            if (Bridge.DisallowedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // If allowed flows are specified, flow must be in the list
            if (Bridge.AllowedFlows.Length > 0)
            {
                return Bridge.AllowedFlows.Contains(flow, StringComparer.OrdinalIgnoreCase);
            }

            // If no allowed flows specified, all flows are allowed (except disallowed ones)
            return true;
        }
    }

    /// <summary>
    ///     Bridge configuration for cross-realm communication.
    /// </summary>
    /// <remarks>
    ///     T-REALM-02: Controls what can flow between realms.
    ///     Implements fail-closed security by default.
    /// </remarks>
    public class BridgeConfig
    {
        /// <summary>
        ///     Gets or sets a value indicating whether bridging is enabled.
        /// </summary>
        /// <remarks>
        ///     T-REALM-02: When false, realms are completely isolated at application layer.
        ///     No cross-realm flows are permitted.
        /// </remarks>
        public bool Enabled { get; set; } = false;

        /// <summary>
        ///     Gets or sets the flows that are explicitly allowed between realms.
        /// </summary>
        /// <remarks>
        ///     T-REALM-02: Whitelist of allowed flows. If empty, all flows are allowed
        ///     (except those in DisallowedFlows). Examples:
        ///     - "governance:read" - Read governance documents across realms
        ///     - "replication:write" - Write replication data across realms
        ///     - "federation:activitypub" - Allow ActivityPub federation flows
        ///     - "metadata:read" - Read metadata across realms
        /// </remarks>
        public string[] AllowedFlows { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     Gets or sets the flows that are explicitly disallowed between realms.
        /// </summary>
        /// <remarks>
        ///     T-REALM-02: Blacklist of forbidden flows. Always denied, even if in AllowedFlows.
        ///     Examples: "governance:root", "replication:fullcopy", "mcp:control"
        /// </remarks>
        public string[] DisallowedFlows { get; set; } = new[]
        {
            "governance:root",    // Never allow root governance changes across realms
            "replication:fullcopy", // Never allow full database copies
            "mcp:control"         // Never allow moderation control across realms
        };

        /// <summary>
        ///     Validates the bridge configuration.
        /// </summary>
        /// <returns>A list of validation errors.</returns>
        public IEnumerable<ValidationResult> Validate()
        {
            var results = new List<ValidationResult>();

            // Validate flow names
            var allFlows = AllowedFlows.Concat(DisallowedFlows).ToArray();
            foreach (var flow in allFlows)
            {
                if (string.IsNullOrWhiteSpace(flow))
                {
                    results.Add(new ValidationResult("Flow names cannot be null or empty."));
                    continue;
                }

                // Validate flow format (should be category:action)
                if (!flow.Contains(':'))
                {
                    results.Add(new ValidationResult($"Invalid flow format '{flow}'. Expected 'category:action'."));
                }
            }

            // Check for conflicting flows (same flow in both allowed and disallowed)
            var conflicts = AllowedFlows.Intersect(DisallowedFlows, StringComparer.OrdinalIgnoreCase).ToArray();
            if (conflicts.Length > 0)
            {
                results.Add(new ValidationResult(
                    $"Conflicting flows found in both allowed and disallowed lists: {string.Join(", ", conflicts)}."));
            }

            return results;
        }
    }
}

