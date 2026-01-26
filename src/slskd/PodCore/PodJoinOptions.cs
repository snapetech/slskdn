// <copyright file="PodJoinOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

/// <summary>
/// Options for Pod join/leave. Binds to PodCore:Join.
/// 6.4: When SignatureMode is Enforce, join requests require a nonce and replay is rejected.
/// </summary>
public class PodJoinOptions
{
    /// <summary>
    /// Signature/nonce strictness for join. Enforce: require Nonce on PodJoinRequest and reject replays.
    /// </summary>
    public SignatureMode SignatureMode { get; set; } = SignatureMode.Off;
}
