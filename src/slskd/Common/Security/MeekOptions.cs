// <copyright file="MeekOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System.Collections.Generic;

    /// <summary>
    /// Options for Meek transport (domain fronting).
    /// </summary>
    public class MeekOptions
    {

        /// <summary>
        /// Gets or sets a value indicating whether this transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the fronting domain (the domain that appears in SNI/Host header).
        /// </summary>
        public string? FrontingDomain { get; set; }

        /// <summary>
        /// Gets or sets the front domain (alternate name).
        /// </summary>
        public string? FrontDomain { get; set; }

        /// <summary>
        /// Gets or sets the actual bridge URL (hidden behind front domain).
        /// </summary>
        public string? BridgeUrl { get; set; }

        /// <summary>
        /// Gets or sets custom HTTP headers for domain fronting.
        /// </summary>
        public Dictionary<string, string>? CustomHeaders { get; set; }

        /// <summary>
        /// Gets or sets the proxy URL.
        /// </summary>
        public string? ProxyUrl { get; set; }

        /// <summary>
        /// Gets or sets whether to use HTTPS.
        /// </summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        public int ConnectTimeoutSeconds { get; set; } = 30;
    }
}
