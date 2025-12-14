// <copyright file="Obfs4Options.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    using System.Collections.Generic;

    /// <summary>
    /// Options for Obfs4 transport (obfuscation protocol).
    /// </summary>
    public class Obfs4Options
    {

        /// <summary>
        /// Gets or sets a value indicating whether this transport is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the path to the obfs4proxy executable.
        /// </summary>
        public string? Obfs4ProxyPath { get; set; }

        /// <summary>
        /// Gets or sets the bridge lines (bridge connection strings).
        /// </summary>
        public List<string>? BridgeLines { get; set; }

        /// <summary>
        /// Gets or sets the bridge address.
        /// </summary>
        public string? BridgeAddress { get; set; }

        /// <summary>
        /// Gets or sets the bridge port.
        /// </summary>
        public int? BridgePort { get; set; }

        /// <summary>
        /// Gets or sets the bridge fingerprint.
        /// </summary>
        public string? BridgeFingerprint { get; set; }

        /// <summary>
        /// Gets or sets bridge parameters.
        /// </summary>
        public Dictionary<string, string>? BridgeParameters { get; set; }

        /// <summary>
        /// Gets or sets the certificate.
        /// </summary>
        public string? Certificate { get; set; }

        /// <summary>
        /// Gets or sets the connection timeout in seconds.
        /// </summary>
        public int ConnectTimeoutSeconds { get; set; } = 30;
    }
}
