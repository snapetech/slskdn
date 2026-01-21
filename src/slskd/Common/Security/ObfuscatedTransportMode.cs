// <copyright file="ObfuscatedTransportMode.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security
{
    /// <summary>
    /// Obfuscated transport mode enumeration.
    /// </summary>
    public enum ObfuscatedTransportMode
    {
        /// <summary>
        /// Direct connection (no obfuscation).
        /// </summary>
        Direct,

        /// <summary>
        /// WebSocket tunnel transport.
        /// </summary>
        WebSocket,

        /// <summary>
        /// HTTP tunnel transport.
        /// </summary>
        HttpTunnel,

        /// <summary>
        /// Obfs4 obfuscation transport.
        /// </summary>
        Obfs4,

        /// <summary>
        /// Meek domain fronting transport.
        /// </summary>
        Meek,
    }
}



