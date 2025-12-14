// <copyright file="GossipMessage.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Gossip
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    ///     Represents a gossip message in the mesh.
    /// </summary>
    /// <remarks>
    ///     T-REALM-03: Gossip messages contain information that needs to be disseminated
    ///     throughout the mesh. Extended with realm awareness for proper scoping.
    /// </remarks>
    public class GossipMessage
    {
        /// <summary>
        ///     Gets or sets the unique message identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        ///     Gets or sets the message type.
        /// </summary>
        /// <remarks>
        ///     Examples: "health", "abuse", "peer-status", "content-update".
        /// </remarks>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the realm ID this message belongs to.
        /// </summary>
        /// <remarks>
        ///     T-REALM-03: Associates message with specific realm for scoping.
        ///     Messages from different realms are kept separate.
        /// </remarks>
        [JsonPropertyName("realmId")]
        public string? RealmId { get; set; }

        /// <summary>
        ///     Gets or sets the message timestamp.
        /// </summary>
        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        ///     Gets or sets the hop count (how many times this message has been forwarded).
        /// </summary>
        [JsonPropertyName("hops")]
        public int Hops { get; set; }

        /// <summary>
        ///     Gets or sets the maximum hops this message can travel.
        /// </summary>
        [JsonPropertyName("maxHops")]
        public int MaxHops { get; set; } = 3;

        /// <summary>
        ///     Gets or sets the message time-to-live.
        /// </summary>
        [JsonPropertyName("ttl")]
        public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        ///     Gets or sets the originator of the message.
        /// </summary>
        [JsonPropertyName("originator")]
        public string Originator { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the message payload.
        /// </summary>
        [JsonPropertyName("payload")]
        public object Payload { get; set; } = new object();

        /// <summary>
        ///     Gets or sets additional metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public GossipMetadata Metadata { get; set; } = new GossipMetadata();

        /// <summary>
        ///     Checks if this message can still be forwarded.
        /// </summary>
        /// <returns>True if the message can be forwarded.</returns>
        public bool CanForward()
        {
            return Hops < MaxHops &&
                   Timestamp + Ttl > DateTimeOffset.UtcNow &&
                   !string.IsNullOrWhiteSpace(Type) &&
                   !string.IsNullOrWhiteSpace(Originator);
        }

        /// <summary>
        ///     Creates a forwarded copy of this message with incremented hop count.
        /// </summary>
        /// <returns>A new message with incremented hops.</returns>
        public GossipMessage CreateForwardedCopy()
        {
            return new GossipMessage
            {
                Id = Id,
                Type = Type,
                RealmId = RealmId,
                Timestamp = Timestamp,
                Hops = Hops + 1,
                MaxHops = MaxHops,
                Ttl = Ttl,
                Originator = Originator,
                Payload = Payload,
                Metadata = Metadata
            };
        }

        /// <summary>
        ///     Checks if this message belongs to a specific realm.
        /// </summary>
        /// <param name="realmId">The realm ID to check.</param>
        /// <returns>True if the message belongs to the realm.</returns>
        public bool BelongsToRealm(string realmId)
        {
            return string.Equals(RealmId, realmId, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    ///     Metadata for gossip messages.
    /// </summary>
    public class GossipMetadata
    {
        /// <summary>
        ///     Gets or sets the message priority.
        /// </summary>
        [JsonPropertyName("priority")]
        public GossipPriority Priority { get; set; } = GossipPriority.Normal;

        /// <summary>
        ///     Gets or sets the message reliability requirement.
        /// </summary>
        [JsonPropertyName("reliability")]
        public GossipReliability Reliability { get; set; } = GossipReliability.BestEffort;

        /// <summary>
        ///     Gets or sets custom metadata properties.
        /// </summary>
        [JsonPropertyName("properties")]
        public System.Collections.Generic.Dictionary<string, object> Properties { get; set; }
            = new System.Collections.Generic.Dictionary<string, object>();
    }

    /// <summary>
    ///     Gossip message priority levels.
    /// </summary>
    public enum GossipPriority
    {
        /// <summary>Low priority message.</summary>
        Low = 0,

        /// <summary>Normal priority message.</summary>
        Normal = 1,

        /// <summary>High priority message.</summary>
        High = 2,

        /// <summary>Critical message requiring immediate attention.</summary>
        Critical = 3
    }

    /// <summary>
    ///     Gossip message reliability requirements.
    /// </summary>
    public enum GossipReliability
    {
        /// <summary>Best effort delivery.</summary>
        BestEffort = 0,

        /// <summary>At least once delivery.</summary>
        AtLeastOnce = 1,

        /// <summary>Exactly once delivery.</summary>
        ExactlyOnce = 2
    }
}
