// <copyright file="PodMessageSignerOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

/// <summary>
/// Options for PodMessageSigner. PR-12. Binds to PodCore:Security or PodCore:MessageSigner.
/// </summary>
public class PodMessageSignerOptions
{
    /// <summary>
    /// Signature verification mode. Off: missing sig accepted (legacy); Warn: log if missing; Enforce: reject unsigned.
    /// </summary>
    public SignatureMode SignatureMode { get; set; } = SignatureMode.Off;
}

/// <summary>
/// Message signature verification strictness.
/// </summary>
public enum SignatureMode
{
    /// <summary>Accept messages without signature; verify when ed25519: present.</summary>
    Off = 0,
    /// <summary>Log warning when signature missing; still accept. Reject invalid.</summary>
    Warn = 1,
    /// <summary>Reject messages without valid ed25519 signature.</summary>
    Enforce = 2,
}
