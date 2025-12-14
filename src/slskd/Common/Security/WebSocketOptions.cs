// <copyright file="WebSocketOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System.Collections.Generic;

    /// <summary>
    /// Options for WebSocket transport.
    /// </summary>
    public class WebSocketOptions
    {
        /// <summary>
        /// Gets or sets the WebSocket server URL.
        /// </summary>
        public string? ServerUrl { get; set; }

        /// <summary>
        /// Gets or sets the WebSocket sub-protocol.
        /// </summary>
        public string? SubProtocol { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use WSS (secure WebSocket).
        /// </summary>
        public bool UseWss { get; set; } = true;

        /// <summary>
        /// Gets or sets custom headers to include in WebSocket handshake.
        /// </summary>
        public Dictionary<string, string>? CustomHeaders { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of pooled connections.
        /// </summary>
        public int MaxPooledConnections { get; set; } = 10;

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        public int ConnectTimeoutSeconds { get; set; } = 30;
    }
}

