// <copyright file="Obfs4Options.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    /// <summary>
    /// Options for Obfs4 transport.
    /// </summary>
    public class Obfs4Options
    {
        /// <summary>
        /// Gets or sets the bridge endpoint.
        /// </summary>
        public string? BridgeEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the bridge fingerprint.
        /// </summary>
        public string? BridgeFingerprint { get; set; }

        /// <summary>
        /// Gets or sets the certificate.
        /// </summary>
        public string? Certificate { get; set; }
    }
}
