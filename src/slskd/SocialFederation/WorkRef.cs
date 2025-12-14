// <copyright file="WorkRef.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text.Json.Serialization;

    /// <summary>
    ///     WorkRef object type for ActivityPub federation.
    /// </summary>
    /// <remarks>
    ///     T-FED02: WorkRef represents creative works (music, books, movies, etc.)
    ///     in ActivityPub federation. Contains only safe, public metadata.
    ///     SECURITY: NO local paths, hashes, mesh peer IDs, or IP addresses.
    /// </remarks>
    public sealed class WorkRef
    {
        /// <summary>
        ///     Gets or sets the JSON-LD context.
        /// </summary>
        [JsonPropertyName("@context")]
        public object Context { get; set; } = new[]
        {
            "https://www.w3.org/ns/activitystreams",
            "https://w3id.org/federation/workref#"
        };

        /// <summary>
        ///     Gets or sets the object ID.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the object type (always "WorkRef").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "WorkRef";

        /// <summary>
        ///     Gets or sets the content domain.
        /// </summary>
        /// <remarks>
        ///     Valid domains: "music", "books", "movies", "tv", "software", "games"
        /// </remarks>
        [JsonPropertyName("domain")]
        [Required]
        [RegularExpression("^(music|books|movies|tv|software|games)$")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the external identifiers.
        /// </summary>
        /// <remarks>
        ///     Maps external service IDs to this work.
        ///     Example: { "musicbrainz": "12345-67890", "discogs": "67890" }
        ///     SECURITY: Only sanitized external service identifiers.
        /// </remarks>
        [JsonPropertyName("externalIds")]
        public Dictionary<string, string> ExternalIds { get; set; } = new();

        /// <summary>
        ///     Gets or sets the work title.
        /// </summary>
        [JsonPropertyName("title")]
        [Required]
        [StringLength(500, MinimumLength = 1)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        ///     Gets or sets the creator/artist/author.
        /// </summary>
        [JsonPropertyName("creator")]
        [StringLength(500)]
        public string? Creator { get; set; }

        /// <summary>
        ///     Gets or sets the release year.
        /// </summary>
        [JsonPropertyName("year")]
        [Range(1000, 2100)]
        public int? Year { get; set; }

        /// <summary>
        ///     Gets or sets additional metadata.
        /// </summary>
        /// <remarks>
        ///     Domain-specific metadata that is safe to share publicly.
        ///     SECURITY: Must be validated to exclude sensitive information.
        /// </remarks>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        ///     Gets or sets the attribution (source instance).
        /// </summary>
        /// <remarks>
        ///     Public attribution to the source instance for this work reference.
        /// </remarks>
        [JsonPropertyName("attributedTo")]
        public string? AttributedTo { get; set; }

        /// <summary>
        ///     Gets or sets the publication timestamp.
        /// </summary>
        [JsonPropertyName("published")]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        ///     Validates that the WorkRef contains no sensitive information.
        /// </summary>
        /// <returns>True if the WorkRef is safe to publish.</returns>
        public bool ValidateSecurity()
        {
            // Check external IDs for potentially sensitive patterns
            foreach (var kvp in ExternalIds)
            {
                var key = kvp.Key.ToLowerInvariant();
                var value = kvp.Value.ToLowerInvariant();

                // Block common sensitive patterns
                if (ContainsSensitivePattern(key) || ContainsSensitivePattern(value))
                {
                    return false;
                }
            }

            // Check metadata for sensitive information
            foreach (var kvp in Metadata)
            {
                var key = kvp.Key.ToLowerInvariant();
                var value = kvp.Value?.ToString()?.ToLowerInvariant() ?? string.Empty;

                if (ContainsSensitivePattern(key) || ContainsSensitivePattern(value))
                {
                    return false;
                }
            }

            // Check title and creator for basic safety
            if (ContainsSensitivePattern(Title) || (Creator != null && ContainsSensitivePattern(Creator)))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsSensitivePattern(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            var sensitivePatterns = new[]
            {
                // File paths
                @"^[a-zA-Z]:[\\/]",  // Windows paths
                @"^/",               // Unix absolute paths
                @"^\.\.",            // Relative paths with ..
                @"[\\/]",            // Any path separators

                // Hashes (hex patterns)
                @"^[a-fA-F0-9]{32,}$",  // MD5/SHA1/SHA256 etc.

                // IP addresses
                @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",

                // UUIDs (could be sensitive identifiers)
                @"^[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$",

                // Mesh peer IDs (pod: or bridge: prefixes)
                @"^(pod|bridge):",

                // URLs with localhost/private IPs
                @"(localhost|127\.0\.0\.1|192\.168\.|10\.|172\.(1[6-9]|2[0-9]|3[0-1])\.)",

                // Sensitive keywords
                @"\b(hash|path|file|local|private|internal)\b"
            };

            return sensitivePatterns.Any(pattern =>
                System.Text.RegularExpressions.Regex.IsMatch(input, pattern));
        }

        /// <summary>
        ///     Creates a WorkRef from music metadata.
        /// </summary>
        /// <param name="musicItem">The music content item.</param>
        /// <param name="instanceUrl">The instance base URL.</param>
        /// <returns>A WorkRef for the music item.</returns>
        public static WorkRef FromMusicItem(ContentDomain.MusicContentItem musicItem, string instanceUrl)
        {
            var workRef = new WorkRef
            {
                Id = $"{instanceUrl}/works/music/{SanitizeId(musicItem.Id)}",
                Domain = "music",
                Title = musicItem.Title ?? "Unknown Title",
                Creator = musicItem.Artist,
                Year = musicItem.Year,
                AttributedTo = $"{instanceUrl}/actors/music",
                Published = DateTimeOffset.UtcNow
            };

            // Add external IDs (sanitized)
            if (!string.IsNullOrEmpty(musicItem.MusicBrainzId))
            {
                workRef.ExternalIds["musicbrainz"] = musicItem.MusicBrainzId;
            }

            if (!string.IsNullOrEmpty(musicItem.DiscogsId))
            {
                workRef.ExternalIds["discogs"] = musicItem.DiscogsId;
            }

            // Add safe metadata
            if (musicItem.DurationSeconds.HasValue)
            {
                workRef.Metadata["durationSeconds"] = musicItem.DurationSeconds.Value;
            }

            if (!string.IsNullOrEmpty(musicItem.Genre))
            {
                workRef.Metadata["genre"] = musicItem.Genre;
            }

            return workRef;
        }

        /// <summary>
        ///     Sanitizes an ID for use in URLs.
        /// </summary>
        /// <param name="id">The ID to sanitize.</param>
        /// <returns>The sanitized ID.</returns>
        private static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "unknown";
            }

            // Replace unsafe characters with hyphens
            return System.Text.RegularExpressions.Regex.Replace(id, @"[^a-zA-Z0-9\-_]", "-")
                .Trim('-')
                .ToLowerInvariant();
        }
    }
}
