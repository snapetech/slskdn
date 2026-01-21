// <copyright file="BridgeFlowTypes.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Realm.Bridge
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Defines the types of flows that can be controlled across realm bridges.
    /// </summary>
    /// <remarks>
    ///     T-REALM-04: Bridge Flow Policies - controlled cross-realm communication.
    ///     Each flow type defines a specific category of cross-realm interaction.
    /// </remarks>
    public static class BridgeFlowTypes
    {
        // ActivityPub federation flows
        public const string ActivityPubRead = "activitypub:read";
        public const string ActivityPubWrite = "activitypub:write";

        // Metadata and discovery flows
        public const string MetadataRead = "metadata:read";
        public const string SearchRead = "search:read";

        // Content sharing flows (future)
        public const string ContentRead = "content:read";
        public const string ContentShare = "content:share";

        // Social interaction flows (future)
        public const string SocialRead = "social:read";
        public const string SocialInteract = "social:interact";

        /// <summary>
        ///     Gets all defined flow types.
        /// </summary>
        public static readonly IReadOnlySet<string> AllFlows = new HashSet<string>
        {
            ActivityPubRead,
            ActivityPubWrite,
            MetadataRead,
            SearchRead,
            ContentRead,
            ContentShare,
            SocialRead,
            SocialInteract
        };

        /// <summary>
        ///     Gets the flow types that are allowed by default (safe flows).
        /// </summary>
        /// <remarks>
        ///     These flows are considered low-risk and can be enabled without extensive review.
        /// </remarks>
        public static readonly IReadOnlySet<string> SafeFlows = new HashSet<string>
        {
            ActivityPubRead,
            MetadataRead,
            SearchRead
        };

        /// <summary>
        ///     Gets the flow types that are dangerous and should be carefully controlled.
        /// </summary>
        /// <remarks>
        ///     These flows could potentially allow remote realms to influence local behavior.
        /// </remarks>
        public static readonly IReadOnlySet<string> DangerousFlows = new HashSet<string>
        {
            ActivityPubWrite,
            ContentShare,
            SocialInteract
        };

        /// <summary>
        ///     Gets the flow types that are always forbidden across realms.
        /// </summary>
        /// <remarks>
        ///     These flows are never allowed and cannot be enabled even explicitly.
        /// </remarks>
        public static readonly IReadOnlySet<string> AlwaysForbiddenFlows = new HashSet<string>
        {
            // Governance flows are always forbidden - realms must remain sovereign
            "governance:read",
            "governance:write",
            "governance:root",

            // Configuration flows are always forbidden - no remote config changes
            "config:read",
            "config:write",

            // Moderation flows are always forbidden - local MCP control only
            "mcp:read",
            "mcp:write",
            "mcp:control",

            // Replication flows are always forbidden - no automatic cross-realm replication
            "replication:read",
            "replication:write",
            "replication:fullcopy"
        };

        /// <summary>
        ///     Validates that a flow type is well-formed.
        /// </summary>
        /// <param name="flow">The flow type to validate.</param>
        /// <returns>True if the flow type is valid.</returns>
        public static bool IsValidFlow(string flow)
        {
            if (string.IsNullOrWhiteSpace(flow))
            {
                return false;
            }

            // Must contain exactly one colon
            var parts = flow.Split(':');
            if (parts.Length != 2)
            {
                return false;
            }

            // Both parts must be non-empty
            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            // Category and action should be reasonable length
            if (parts[0].Length > 32 || parts[1].Length > 32)
            {
                return false;
            }

            // Only allow safe characters
            return flow.All(c => char.IsLetterOrDigit(c) || c == ':' || c == '-' || c == '_');
        }

        /// <summary>
        ///     Gets the category of a flow type.
        /// </summary>
        /// <param name="flow">The flow type.</param>
        /// <returns>The category, or null if invalid.</returns>
        public static string? GetFlowCategory(string flow)
        {
            if (!IsValidFlow(flow))
            {
                return null;
            }

            return flow.Split(':')[0];
        }

        /// <summary>
        ///     Gets the action of a flow type.
        /// </summary>
        /// <param name="flow">The flow type.</param>
        /// <returns>The action, or null if invalid.</returns>
        public static string? GetFlowAction(string flow)
        {
            if (!IsValidFlow(flow))
            {
                return null;
            }

            return flow.Split(':')[1];
        }
    }
}


