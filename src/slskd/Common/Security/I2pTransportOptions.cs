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
        /// Gets or sets the I2P SAM bridge endpoint.
        /// </summary>
        public string? SamBridgeEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the I2P destination private key.
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
    }
}
