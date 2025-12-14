// <copyright file="HttpTunnelOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    /// <summary>
    /// Options for HTTP tunnel transport.
    /// </summary>
    public class HttpTunnelOptions
    {
        /// <summary>
        /// Gets or sets the proxy endpoint.
        /// </summary>
        public string? ProxyEndpoint { get; set; }

        /// <summary>
        /// Gets or sets whether to use HTTPS.
        /// </summary>
        public bool UseHttps { get; set; } = true;
    }
}
