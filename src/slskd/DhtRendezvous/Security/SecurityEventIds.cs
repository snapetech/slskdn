// <copyright file="SecurityEventIds.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using Microsoft.Extensions.Logging;

/// <summary>
/// Event IDs for security-related log entries to enable filtering, alerting, and metrics.
/// P2-3: Structured event IDs for security monitoring.
/// </summary>
public static class SecurityEventIds
{
    // Certificate & Identity Events (1000-1099)
    public static readonly EventId CertificatePinViolation = new(1001, "CertificatePinViolation");
    public static readonly EventId CertificateExpired = new(1002, "CertificateExpired");
    public static readonly EventId CertificateExpiringSoon = new(1003, "CertificateExpiringSoon");
    public static readonly EventId InvalidCertificate = new(1004, "InvalidCertificate");
    public static readonly EventId TofuFirstSeen = new(1010, "TofuFirstSeen");
    public static readonly EventId IdentityKeyGenerated = new(1020, "IdentityKeyGenerated");
    public static readonly EventId IdentityKeyLoaded = new(1021, "IdentityKeyLoaded");
    
    // Handshake & Authentication Events (1100-1199)
    public static readonly EventId HandshakeSignatureInvalid = new(1101, "HandshakeSignatureInvalid");
    public static readonly EventId HandshakeTimestampInvalid = new(1102, "HandshakeTimestampInvalid");
    public static readonly EventId HandshakePeerIdMismatch = new(1103, "HandshakePeerIdMismatch");
    public static readonly EventId HandshakeSuccess = new(1110, "HandshakeSuccess");
    public static readonly EventId HandshakeFailed = new(1111, "HandshakeFailed");
    
    // Replay Attack & Anti-Rollback Events (1200-1299)
    public static readonly EventId ReplayAttackDetected = new(1201, "ReplayAttackDetected");
    public static readonly EventId DescriptorRollbackDetected = new(1202, "DescriptorRollbackDetected");
    public static readonly EventId NonceReused = new(1203, "NonceReused");
    
    // Rate Limiting & DoS Events (1300-1399)
    public static readonly EventId RateLimitExceeded = new(1301, "RateLimitExceeded");
    public static readonly EventId ConnectionLimitExceeded = new(1302, "ConnectionLimitExceeded");
    public static readonly EventId MessageSizeLimitExceeded = new(1303, "MessageSizeLimitExceeded");
    public static readonly EventId IpBlocked = new(1310, "IpBlocked");
    public static readonly EventId UsernameBlocked = new(1311, "UsernameBlocked");
    
    // Protocol Violation Events (1400-1499)
    public static readonly EventId ProtocolViolation = new(1401, "ProtocolViolation");
    public static readonly EventId InvalidMessageFormat = new(1402, "InvalidMessageFormat");
    public static readonly EventId UnexpectedMessage = new(1403, "UnexpectedMessage");
    
    // File Integrity Events (1500-1599)
    public static readonly EventId FileIntegrityViolation = new(1501, "FileIntegrityViolation");
    public static readonly EventId HmacVerificationFailed = new(1502, "HmacVerificationFailed");
}

