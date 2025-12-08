// <copyright file="OverlayTimeouts.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;

/// <summary>
/// Timeout constants for overlay connections.
/// All timeouts are chosen to prevent resource exhaustion attacks.
/// </summary>
public static class OverlayTimeouts
{
    /// <summary>
    /// Maximum time to establish TCP connection.
    /// </summary>
    public static readonly TimeSpan Connect = TimeSpan.FromSeconds(10);
    
    /// <summary>
    /// Maximum time for TLS handshake after TCP connection.
    /// </summary>
    public static readonly TimeSpan TlsHandshake = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Maximum time to complete mesh_hello/mesh_hello_ack exchange.
    /// </summary>
    public static readonly TimeSpan ProtocolHandshake = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Maximum time to wait for any single message read.
    /// </summary>
    public static readonly TimeSpan MessageRead = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Maximum time to wait for any single message write.
    /// SECURITY: Shorter than read timeout to prevent slow clients from holding connections.
    /// </summary>
    public static readonly TimeSpan MessageWrite = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Maximum time connection can be idle before disconnect.
    /// </summary>
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(5);
    
    /// <summary>
    /// Interval for sending keepalive pings.
    /// </summary>
    public static readonly TimeSpan KeepaliveInterval = TimeSpan.FromMinutes(2);
    
    /// <summary>
    /// Maximum time to wait for pong after ping.
    /// </summary>
    public static readonly TimeSpan PongTimeout = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Grace period after disconnect message before forceful close.
    /// </summary>
    public static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(2);
}

