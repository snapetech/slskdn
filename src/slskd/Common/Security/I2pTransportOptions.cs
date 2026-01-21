// <copyright file="I2pTransportOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    /// <summary>
    /// Options for I2P transport.
    /// </summary>
    public class I2pTransportOptions
    {

        /// <summary>
        /// Gets or sets a value indicating whether this transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the SAM bridge address.
        /// </summary>
        public string SamBridgeAddress { get; set; } = "127.0.0.1";

        /// <summary>
        /// Gets or sets the SAM bridge port.
        /// </summary>
        public int SamBridgePort { get; set; } = 7656;

        /// <summary>
        /// Gets or sets the SOCKS proxy host.
        /// </summary>
        public string? SocksHost { get; set; }

        /// <summary>
        /// Gets or sets the SOCKS proxy port.
        /// </summary>
        public int? SocksPort { get; set; }

        /// <summary>
        /// Gets or sets the I2P destination keys.
        /// </summary>
        public string? DestinationKeys { get; set; }

        /// <summary>
        /// Gets or sets the I2P SAM bridge endpoint (legacy).
        /// </summary>
        public string? SamBridgeEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the I2P destination private key (legacy).
        /// </summary>
        public string? PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets whether to use inbound tunnels.
        /// </summary>
        public bool UseInboundTunnels { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to use outbound tunnels.
        /// </summary>
        public bool UseOutboundTunnels { get; set; } = true;

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        public int ConnectTimeoutSeconds { get; set; } = 30;
    }
}
