// <copyright file="FederationPublishingOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Federation publishing options.
    /// </summary>
    /// <remarks>
    ///     T-FED03: Controls which VirtualSoulfind content gets published to federation.
    ///     Per-domain and per-list visibility policies with moderation integration.
    /// </remarks>
    public class FederationPublishingOptions
    {
        /// <summary>
        ///     Gets a value indicating whether federation publishing is enabled.
        /// </summary>
        [Argument(default, "federation-publishing-enabled")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_ENABLED")]
        [Description("enable publishing VirtualSoulfind content to federation")]
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Gets the domains that can be published to federation.
        /// </summary>
        /// <remarks>
        ///     Only content from these domains will be published.
        ///     Example: ["music", "books", "movies"]
        /// </remarks>
        public string[] PublishableDomains { get; init; } = new[] { "music" };

        /// <summary>
        ///     Gets the maximum number of activities to publish per hour.
        /// </summary>
        /// <remarks>
        ///     Rate limiting to prevent overwhelming federation servers.
        /// </remarks>
        [Argument(default, "federation-publishing-max-activities-per-hour")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_MAX_ACTIVITIES_PER_HOUR")]
        [Description("maximum activities to publish per hour")]
        [Range(1, 1000)]
        public int MaxActivitiesPerHour { get; init; } = 100;

        /// <summary>
        ///     Gets the timeout for federation delivery attempts.
        /// </summary>
        [Argument(default, "federation-publishing-delivery-timeout-seconds")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_DELIVERY_TIMEOUT_SECONDS")]
        [Description("timeout for federation delivery attempts (seconds)")]
        [Range(5, 300)]
        public int DeliveryTimeoutSeconds { get; init; } = 30;

        /// <summary>
        ///     Gets the maximum number of delivery retry attempts.
        /// </summary>
        [Argument(default, "federation-publishing-max-delivery-retries")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_MAX_DELIVERY_RETRIES")]
        [Description("maximum delivery retry attempts")]
        [Range(0, 10)]
        public int MaxDeliveryRetries { get; init; } = 3;

        /// <summary>
        ///     Gets a value indicating whether to require moderation approval before publishing.
        /// </summary>
        /// <remarks>
        ///     When enabled, content must pass through the moderation pipeline
        ///     before being published to federation.
        /// </remarks>
        [Argument(default, "federation-publishing-require-moderation")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_REQUIRE_MODERATION")]
        [Description("require moderation approval before publishing")]
        public bool RequireModerationApproval { get; init; } = true;

        /// <summary>
        ///     Gets the default visibility for published content.
        /// </summary>
        /// <remarks>
        ///     Default visibility when none is specified: "public", "private", or "circle:name".
        /// </remarks>
        [Argument(default, "federation-publishing-default-visibility")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_DEFAULT_VISIBILITY")]
        [Description("default visibility for published content")]
        public string DefaultVisibility { get; init; } = "public";

        /// <summary>
        ///     Gets the list of approved federation circles.
        /// </summary>
        /// <remarks>
        ///     Named circles that content can be published to with restricted visibility.
        /// </remarks>
        public string[] ApprovedCircles { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets a value indicating whether to include external service links in publications.
        /// </summary>
        /// <remarks>
        ///     When enabled, includes MusicBrainz, Discogs, etc. links in WorkRefs.
        ///     Provides additional discovery paths but increases metadata size.
        /// </remarks>
        [Argument(default, "federation-publishing-include-external-links")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_INCLUDE_EXTERNAL_LINKS")]
        [Description("include external service links in publications")]
        public bool IncludeExternalLinks { get; init; } = true;

        /// <summary>
        ///     Gets the maximum content metadata size in kilobytes.
        /// </summary>
        /// <remarks>
        ///     Limits the size of metadata included in federation publications.
        ///     Prevents abuse and reduces bandwidth usage.
        /// </remarks>
        [Argument(default, "federation-publishing-max-metadata-size-kb")]
        [EnvironmentVariable("FEDERATION_PUBLISHING_MAX_METADATA_SIZE_KB")]
        [Description("maximum content metadata size (KB)")]
        [Range(1, 100)]
        public int MaxMetadataSizeKb { get; init; } = 10;
    }
}
