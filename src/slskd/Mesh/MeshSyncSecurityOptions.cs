// <copyright file="MeshSyncSecurityOptions.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
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
