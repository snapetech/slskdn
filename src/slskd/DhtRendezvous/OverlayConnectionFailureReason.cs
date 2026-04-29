// <copyright file="OverlayConnectionFailureReason.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
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
