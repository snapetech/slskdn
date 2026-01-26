// <copyright file="IProfileService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Service for managing peer profiles (own and others).</summary>
public interface IProfileService
{
    /// <summary>Get this peer's own profile (generates if missing).</summary>
    Task<PeerProfile> GetMyProfileAsync(CancellationToken ct = default);

    /// <summary>Update this peer's profile and re-sign.</summary>
    Task<PeerProfile> UpdateMyProfileAsync(string displayName, string? avatar, int capabilities, List<PeerEndpoint> endpoints, CancellationToken ct = default);

    /// <summary>Get a peer's profile by PeerId (from cache or fetch from endpoint).</summary>
    Task<PeerProfile?> GetProfileAsync(string peerId, CancellationToken ct = default);

    /// <summary>Verify a profile's signature.</summary>
    bool VerifyProfile(PeerProfile profile);

    /// <summary>Sign a profile with this peer's private key.</summary>
    PeerProfile SignProfile(PeerProfile profile);

    /// <summary>Generate a friend code from PeerId.</summary>
    string GetFriendCode(string peerId);

    /// <summary>Decode a friend code back to PeerId (fuzzy match if needed).</summary>
    string? DecodeFriendCode(string code);
}
