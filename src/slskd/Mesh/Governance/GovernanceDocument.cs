// <copyright file="GovernanceDocument.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Governance
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    ///     Represents a governance document in the mesh.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Governance documents contain policies, rules, and configurations
    ///     that govern mesh behavior. Extended with realm awareness.
    /// </remarks>
    public class GovernanceDocument
    {
        /// <summary>
        ///     Gets or sets the unique document identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the document type.
        /// </summary>
        /// <remarks>
        ///     Examples: "policy", "rule", "configuration", "profile".
        /// </remarks>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the document version.
        /// </summary>
        [JsonPropertyName("version")]
        public int Version { get; set; }

        /// <summary>
        ///     Gets or sets the creation timestamp.
        /// </summary>
        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Gets or sets the last modification timestamp.
        /// </summary>
        [JsonPropertyName("modified")]
        public DateTimeOffset Modified { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Gets or sets the realm ID this document belongs to.
        /// </summary>
        /// <remarks>
        ///     T-REALM-03: Associates document with specific realm for scoping.
        /// </remarks>
        [JsonPropertyName("realmId")]
        public string? RealmId { get; set; }

        /// <summary>
        ///     Gets or sets the public key fingerprint of the signer.
        /// </summary>
        [JsonPropertyName("signer")]
        public string Signer { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the document signature.
        /// </summary>
        [JsonPropertyName("signature")]
        public string Signature { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the document content/metadata.
        /// </summary>
        [JsonPropertyName("content")]
        public object Content { get; set; } = new object();

        /// <summary>
        ///     Gets or sets additional metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public GovernanceMetadata Metadata { get; set; } = new GovernanceMetadata();

        /// <summary>
        ///     Validates the governance document structure.
        /// </summary>
        /// <returns>True if the document is structurally valid.</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Id) &&
                   !string.IsNullOrWhiteSpace(Type) &&
                   !string.IsNullOrWhiteSpace(Signer) &&
                   !string.IsNullOrWhiteSpace(Signature) &&
                   Created <= Modified &&
                   Version >= 0;
        }
    }

    /// <summary>
    ///     Metadata for governance documents.
    /// </summary>
    public class GovernanceMetadata
    {
        /// <summary>
        ///     Gets or sets the document description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        ///     Gets or sets the document priority.
        /// </summary>
        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        /// <summary>
        ///     Gets or sets the expiration date (if applicable).
        /// </summary>
        [JsonPropertyName("expires")]
        public DateTimeOffset? Expires { get; set; }

        /// <summary>
        ///     Gets or sets custom metadata properties.
        /// </summary>
        [JsonPropertyName("properties")]
        public System.Collections.Generic.Dictionary<string, object> Properties { get; set; }
            = new System.Collections.Generic.Dictionary<string, object>();
    }
}

