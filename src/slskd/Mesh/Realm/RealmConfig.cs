// <copyright file="RealmConfig.cs" company="slskdN Team">
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
    ///     Realm configuration for single-realm pods.
    /// </summary>
    /// <remarks>
    ///     T-REALM-01: RealmConfig & RealmID Plumbing.
    ///     Defines the realm identity and policies for mesh isolation and governance.
    /// </remarks>
    public class RealmConfig
    {
        /// <summary>
        ///     Gets or sets the realm identifier.
        /// </summary>
        /// <remarks>
        ///     T-REALM-01: Stable identifier for this realm (e.g., "slskdn-main-v1").
        ///     Used as namespace salt for mesh/DHT overlay isolation.
        ///     Must be non-empty and unique across all realms.
        /// </remarks>
        [Required]
        [NotNullOrWhiteSpace]
        [RegularExpression(@"^[a-zA-Z0-9\-_\.]{3,64}$",
            ErrorMessage = "Realm ID must be 3-64 characters, containing only letters, numbers, hyphens, underscores, and periods.")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the display name for this realm.
        /// </summary>
        [StringLength(100)]
        public string? DisplayName { get; set; }

        /// <summary>
        ///     Gets or sets the description of this realm.
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }

        /// <summary>
        ///     Gets or sets the governance root identities trusted for this realm.
        /// </summary>
        /// <remarks>
        ///     T-REALM-01: Governance identities trusted for signing realm policies.
        ///     Only governance documents signed by these roots are accepted.
        ///     Format: Array of public key fingerprints or identity URIs.
        /// </remarks>
        public string[] GovernanceRoots { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     Gets or sets the bootstrap nodes for joining the mesh overlay.
        /// </summary>
        /// <remarks>
        ///     T-REALM-01: Initial peer endpoints for joining the realm's mesh.
        ///     Used when pod has no existing mesh connections.
        ///     Format: Array of "host:port" or full peer URIs.
        /// </remarks>
        public string[] BootstrapNodes { get; set; } = Array.Empty<string>();

        /// <summary>
        ///     Gets or sets the realm policies.
        /// </summary>
        /// <remarks>
        ///     T-REALM-01: Configuration for gossip, replication, and other realm behaviors.
        /// </remarks>
        public RealmPolicies Policies { get; set; } = new RealmPolicies();

        /// <summary>
        ///     Validates the realm configuration.
        /// </summary>
        /// <returns>A list of validation errors, or empty if valid.</returns>
        public IEnumerable<ValidationResult> Validate()
        {
            var results = new List<ValidationResult>();

            // Validate realm ID
            if (string.IsNullOrWhiteSpace(Id))
            {
                results.Add(new ValidationResult("Realm ID is required.", new[] { nameof(Id) }));
            }
            else
            {
                // Warn about generic/default IDs
                var genericIds = new[] { "default", "realm", "main", "test", "dev", "prod" };
                if (genericIds.Contains(Id.ToLowerInvariant()))
                {
                    // This is a warning, not an error - realms can use generic IDs if they want
                    // But we log it for awareness
                }

                // Check for potentially problematic patterns
                if (Id.Contains("..") || Id.StartsWith(".") || Id.EndsWith("."))
                {
                    results.Add(new ValidationResult(
                        "Realm ID should not start or end with periods, or contain consecutive periods.",
                        new[] { nameof(Id) }));
                }
            }

            // Validate governance roots
            if (GovernanceRoots.Length == 0)
            {
                results.Add(new ValidationResult(
                    "At least one governance root is required for realm security.",
                    new[] { nameof(GovernanceRoots) }));
            }

            foreach (var root in GovernanceRoots)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    results.Add(new ValidationResult(
                        "Governance root entries cannot be null or empty.",
                        new[] { nameof(GovernanceRoots) }));
                }
            }

            // Validate bootstrap nodes
            if (BootstrapNodes.Length == 0)
            {
                // This is a warning, not an error - realms can operate without bootstrap nodes
                // if they have existing mesh connections
            }

            foreach (var node in BootstrapNodes)
            {
                if (string.IsNullOrWhiteSpace(node))
                {
                    results.Add(new ValidationResult(
                        "Bootstrap node entries cannot be null or empty.",
                        new[] { nameof(BootstrapNodes) }));
                }
            }

            // Validate policies
            if (Policies == null)
            {
                results.Add(new ValidationResult("Realm policies are required.", new[] { nameof(Policies) }));
            }
            else
            {
                results.AddRange(Policies.Validate().Select(r =>
                    new ValidationResult(r.ErrorMessage, r.MemberNames.Select(n => $"{nameof(Policies)}.{n}"))));
            }

            return results;
        }

        /// <summary>
        ///     Gets a value indicating whether this realm configuration is valid.
        /// </summary>
        public bool IsValid => !Validate().Any();

        /// <summary>
        ///     Creates a realm namespace salt from the realm ID.
        /// </summary>
        /// <returns>A stable salt value for namespacing operations.</returns>
        /// <remarks>
        ///     T-REALM-01: Used for mesh/DHT overlay namespace isolation.
        ///     Different realms get different namespace salts, ensuring isolation.
        /// </remarks>
        public byte[] GetNamespaceSalt()
        {
            if (string.IsNullOrEmpty(Id))
            {
                throw new InvalidOperationException("Cannot generate namespace salt for realm with empty ID.");
            }

            // Use SHA256 hash of realm ID as salt
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Id));
        }

        /// <summary>
        ///     Checks if the given governance root is trusted for this realm.
        /// </summary>
        /// <param name="governanceRoot">The governance root to check.</param>
        /// <returns>True if the root is trusted.</returns>
        public bool IsTrustedGovernanceRoot(string governanceRoot)
        {
            if (string.IsNullOrWhiteSpace(governanceRoot))
            {
                return false;
            }

            return GovernanceRoots.Contains(governanceRoot, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Realm policies configuration.
    /// </summary>
    public class RealmPolicies
    {
        /// <summary>
        ///     Gets or sets a value indicating whether gossip is enabled.
        /// </summary>
        /// <remarks>
        ///     Controls whether this pod participates in realm-wide gossip.
        /// </remarks>
        public bool GossipEnabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets a value indicating whether replication is enabled.
        /// </summary>
        /// <remarks>
        ///     Controls whether this pod participates in content replication.
        /// </remarks>
        public bool ReplicationEnabled { get; set; } = true;

        /// <summary>
        ///     Gets or sets the maximum number of gossip hops.
        /// </summary>
        [Range(1, 10)]
        public int MaxGossipHops { get; set; } = 3;

        /// <summary>
        ///     Gets or sets the gossip interval in seconds.
        /// </summary>
        [Range(30, 3600)]
        public int GossipIntervalSeconds { get; set; } = 300; // 5 minutes

        /// <summary>
        ///     Gets or sets a value indicating whether federation is allowed.
        /// </summary>
        /// <remarks>
        ///     Controls whether this realm allows ActivityPub federation.
        /// </remarks>
        public bool FederationAllowed { get; set; } = true;

        /// <summary>
        ///     Validates the realm policies.
        /// </summary>
        /// <returns>A list of validation errors.</returns>
        public IEnumerable<ValidationResult> Validate()
        {
            var results = new List<ValidationResult>();

            if (MaxGossipHops < 1 || MaxGossipHops > 10)
            {
                results.Add(new ValidationResult(
                    "Max gossip hops must be between 1 and 10.",
                    new[] { nameof(MaxGossipHops) }));
            }

            if (GossipIntervalSeconds < 30 || GossipIntervalSeconds > 3600)
            {
                results.Add(new ValidationResult(
                    "Gossip interval must be between 30 and 3600 seconds.",
                    new[] { nameof(GossipIntervalSeconds) }));
            }

            return results;
        }
    }
}
