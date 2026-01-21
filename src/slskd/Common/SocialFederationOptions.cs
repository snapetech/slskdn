// <copyright file="SocialFederationOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.ComponentModel.DataAnnotations;
    using Utility.CommandLine;
    using Utility.EnvironmentVariables;

    /// <summary>
    ///     Social federation options.
    /// </summary>
    /// <remarks>
    ///     T-FED01: Social Federation Foundation (ActivityPub Server Skeleton).
    ///     Controls ActivityPub federation behavior and actor exposure.
    /// </remarks>
    public class SocialFederationOptions
    {
        /// <summary>
        ///     Gets a value indicating whether social federation is enabled.
        /// </summary>
        [Argument(default, "federation-enabled")]
        [EnvironmentVariable("FEDERATION_ENABLED")]
        [Description("enable social federation (ActivityPub)")]
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Gets the federation mode.
        /// </summary>
        /// <remarks>
        ///     T-FED01: Controls actor exposure and federation behavior.
        ///     - Hermit: No federation, actors not exposed
        ///     - FriendsOnly: Limited federation with approved peers
        ///     - Public: Full federation with public discovery
        /// </remarks>
        [Argument(default, "federation-mode")]
        [EnvironmentVariable("FEDERATION_MODE")]
        [Description("federation mode: Hermit, FriendsOnly, or Public")]
        public string Mode { get; init; } = "Hermit";

        /// <summary>
        ///     Gets the domain name for this instance.
        /// </summary>
        /// <remarks>
        ///     Used for ActivityPub actor IDs and WebFinger discovery.
        ///     Should match the external domain users access the instance on.
        /// </remarks>
        [Argument(default, "federation-domain")]
        [EnvironmentVariable("FEDERATION_DOMAIN")]
        [Description("domain name for ActivityPub federation")]
        public string? Domain { get; init; }

        /// <summary>
        ///     Gets the base URL for federation endpoints.
        /// </summary>
        /// <remarks>
        ///     Full base URL including protocol and domain.
        ///     Used to construct actor and endpoint URLs.
        /// </remarks>
        [Argument(default, "federation-base-url")]
        [EnvironmentVariable("FEDERATION_BASE_URL")]
        [Description("base URL for federation endpoints")]
        public string? BaseUrl { get; init; }

        /// <summary>
        ///     Gets the list of approved federation peers (for FriendsOnly mode).
        /// </summary>
        /// <remarks>
        ///     Domains of approved federation partners.
        ///     Only used in FriendsOnly mode for selective federation.
        /// </remarks>
        public string[] ApprovedPeers { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Gets the maximum number of activities to keep in outbox.
        /// </summary>
        [Argument(default, "federation-outbox-max-activities")]
        [EnvironmentVariable("FEDERATION_OUTBOX_MAX_ACTIVITIES")]
        [Description("maximum activities to keep in outbox")]
        [Range(10, 1000)]
        public int OutboxMaxActivities { get; init; } = 100;

        /// <summary>
        ///     Gets the maximum number of activities to return in a single page.
        /// </summary>
        [Argument(default, "federation-page-size")]
        [EnvironmentVariable("FEDERATION_PAGE_SIZE")]
        [Description("maximum activities per page")]
        [Range(10, 100)]
        public int PageSize { get; init; } = 20;

        /// <summary>
        ///     Gets a value indicating whether to enable ActivityPub signature verification.
        /// </summary>
        /// <remarks>
        ///     When enabled, validates HTTP signatures on incoming ActivityPub requests.
        ///     Strongly recommended for security but can be disabled for debugging.
        /// </remarks>
        [Argument(default, "federation-verify-signatures")]
        [EnvironmentVariable("FEDERATION_VERIFY_SIGNATURES")]
        [Description("enable HTTP signature verification for ActivityPub")]
        public bool VerifySignatures { get; init; } = true;

        /// <summary>
        ///     Gets the timeout for federation HTTP requests in seconds.
        /// </summary>
        [Argument(default, "federation-http-timeout-seconds")]
        [EnvironmentVariable("FEDERATION_HTTP_TIMEOUT_SECONDS")]
        [Description("timeout for federation HTTP requests (seconds)")]
        [Range(5, 120)]
        public int HttpTimeoutSeconds { get; init; } = 30;

        /// <summary>
        ///     Gets a value indicating whether federation is in hermit mode (no exposure).
        /// </summary>
        public bool IsHermit => string.Equals(Mode, "Hermit", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Gets a value indicating whether federation is in friends-only mode.
        /// </summary>
        public bool IsFriendsOnly => string.Equals(Mode, "FriendsOnly", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Gets a value indicating whether federation is in public mode.
        /// </summary>
        public bool IsPublic => string.Equals(Mode, "Public", StringComparison.OrdinalIgnoreCase);
    }
}
