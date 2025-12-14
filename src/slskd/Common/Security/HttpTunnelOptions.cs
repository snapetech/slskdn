// <copyright file="HttpTunnelOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System.Collections.Generic;

    /// <summary>
    /// Options for HTTP tunnel transport.
    /// </summary>
    public class HttpTunnelOptions
    {
        /// <summary>
        /// Gets or sets the HTTP tunnel endpoint URL.
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// Gets or sets the proxy URL for the tunnel.
        /// </summary>
        public string? ProxyUrl { get; set; }

        /// <summary>
        /// Gets or sets custom HTTP headers.
        /// </summary>
        public Dictionary<string, string>? CustomHeaders { get; set; }

        /// <summary>
        /// Gets or sets the User-Agent header.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// Gets or sets the HTTP method (GET, POST, etc.).
        /// </summary>
        public string Method { get; set; } = "POST";

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
