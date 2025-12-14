// <copyright file="MeekOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    /// <summary>
    /// Options for Meek transport.
    /// </summary>
    public class MeekOptions
    {
        /// <summary>
        /// Gets or sets the bridge endpoint.
        /// </summary>
        public string? BridgeEndpoint { get; set; }

        /// <summary>
        /// Gets or sets whether to use HTTPS.
        /// </summary>
        public bool UseHttps { get; set; } = true;

        /// <summary>
        /// Gets or sets the fronting domain.
        /// </summary>
        public string? FrontingDomain { get; set; }
    }
}
