// <copyright file="MeshSyncSecurityOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh;

/// <summary>
///     Options for mesh sync security (T-1432, T-1433, T-1434, T-1435): rate limiting,
///     quarantine, proof-of-possession, and consensus. Binds to <c>Mesh:SyncSecurity</c>.
/// </summary>
public class MeshSyncSecurityOptions
{
    /// <summary>Maximum invalid entries per 5‑minute window before rate limit (T-1432). Default: 50.</summary>
    public int MaxInvalidEntriesPerWindow { get; set; } = 50;

    /// <summary>Maximum invalid messages per 5‑minute window before rate limit (T-1432). Default: 10.</summary>
    public int MaxInvalidMessagesPerWindow { get; set; } = 10;

    /// <summary>Rate limit and violation window in minutes (T-1432, T-1433). Default: 5.</summary>
    public int RateLimitWindowMinutes { get; set; } = 5;

    /// <summary>Number of rate limit violations in the window that trigger quarantine (T-1433). Default: 3.</summary>
    public int QuarantineViolationThreshold { get; set; } = 3;

    /// <summary>Quarantine duration in minutes (T-1433). Default: 30.</summary>
    public int QuarantineDurationMinutes { get; set; } = 30;

    /// <summary>Require proof-of-possession before accepting new hash entries (T-1434). Default: false.</summary>
    public bool ProofOfPossessionEnabled { get; set; } = false;

    /// <summary>
    ///     HARDENING-2026-04-20 H7: require every inbound mesh hash entry to carry a valid Ed25519
    ///     signature from its sender. Default <c>false</c> for one release so the existing tester
    ///     mesh (which emits unsigned entries) isn't silently partitioned; flip to <c>true</c> once
    ///     all operators are on a build that signs on the outbound path.
    /// </summary>
    public bool RequireSignedEntries { get; set; } = false;

    /// <summary>Minimum number of mesh peers to query for consensus (T-1435). Default: 5.</summary>
    public int ConsensusMinPeers { get; set; } = 5;

    /// <summary>Minimum number of peers that must agree (same FlacKey+ByteHash+Size) to accept a hash (T-1435). Default: 3.</summary>
    public int ConsensusMinAgreements { get; set; } = 3;

    /// <summary>Alert when <c>SignatureVerificationFailures</c> exceeds this (lifetime). 0 = disabled. Default: 50.</summary>
    public int AlertThresholdSignatureFailures { get; set; } = 50;

    /// <summary>Alert when <c>RateLimitViolations</c> exceeds this (lifetime). 0 = disabled. Default: 20.</summary>
    public int AlertThresholdRateLimitViolations { get; set; } = 20;

    /// <summary>Alert when <c>QuarantineEvents</c> exceeds this (lifetime). 0 = disabled. Default: 10.</summary>
    public int AlertThresholdQuarantineEvents { get; set; } = 10;
}
