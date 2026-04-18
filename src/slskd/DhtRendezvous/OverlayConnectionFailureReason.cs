// <copyright file="OverlayConnectionFailureReason.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

/// <summary>
/// High-level reason buckets for failed outbound overlay connection attempts.
/// </summary>
public enum OverlayConnectionFailureReason
{
    ConnectTimeout,
    NoRoute,
    ConnectionRefused,
    ConnectionReset,
    TlsEof,
    TlsHandshake,
    ProtocolHandshake,
    RegistrationFailed,
    BlockedPeer,
    Unknown,
}
