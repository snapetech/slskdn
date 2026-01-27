// <copyright file="UsernamePseudonymizer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Capture;

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

public interface IUsernamePseudonymizer
{
    Task<string> GetPeerIdAsync(string soulseekUsername, CancellationToken ct = default);
    Task<string?> GetUsernameAsync(string peerId, CancellationToken ct = default);
}

/// <summary>
/// Deterministic pseudonymization of Soulseek usernames to peer IDs.
/// Phase 6A: T-802 - Real implementation with deterministic hashing.
/// </summary>
public class UsernamePseudonymizer : IUsernamePseudonymizer
{
    private readonly ILogger<UsernamePseudonymizer> logger;
    private readonly ConcurrentDictionary<string, string> usernameToPeerId = new();
    private readonly ConcurrentDictionary<string, string> peerIdToUsername = new();

    // Salt for pseudonymization (prevents rainbow table attacks)
    // In production, this should be configurable per-instance
    private static readonly byte[] PseudonymizationSalt = Encoding.UTF8.GetBytes("slskdn-vsf-pseudonymization-salt-v1");

    public UsernamePseudonymizer(ILogger<UsernamePseudonymizer> logger)
    {
        this.logger = logger;
    }

    public Task<string> GetPeerIdAsync(string soulseekUsername, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(soulseekUsername))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(soulseekUsername));
        }

        // Check cache first
        if (usernameToPeerId.TryGetValue(soulseekUsername, out var cachedPeerId))
        {
            return Task.FromResult(cachedPeerId);
        }

        // Generate deterministic peer ID using SHA256 hash
        var peerId = ComputePeerId(soulseekUsername);

        // Cache both directions
        usernameToPeerId.TryAdd(soulseekUsername, peerId);
        peerIdToUsername.TryAdd(peerId, soulseekUsername);

        logger.LogTrace("[VSF-PSEUDO] Pseudonymized {Username} -> {PeerId}", soulseekUsername, peerId);

        return Task.FromResult(peerId);
    }

    public Task<string?> GetUsernameAsync(string peerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return Task.FromResult<string?>(null);
        }

        // Check cache
        if (peerIdToUsername.TryGetValue(peerId, out var username))
        {
            return Task.FromResult<string?>(username);
        }

        // Cannot reverse hash, so return null
        return Task.FromResult<string?>(null);
    }

    private static string ComputePeerId(string username)
    {
        // Use SHA256 to create deterministic but non-reversible peer ID
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(username.ToLowerInvariant());
        var saltedInput = new byte[inputBytes.Length + PseudonymizationSalt.Length];
        Buffer.BlockCopy(inputBytes, 0, saltedInput, 0, inputBytes.Length);
        Buffer.BlockCopy(PseudonymizationSalt, 0, saltedInput, inputBytes.Length, PseudonymizationSalt.Length);

        var hash = sha256.ComputeHash(saltedInput);

        // Take first 20 bytes (160 bits) and encode as base32
        var peerIdBytes = new byte[20];
        Buffer.BlockCopy(hash, 0, peerIdBytes, 0, 20);

        // Encode as hex for simplicity (could use base32 for shorter IDs)
        var peerId = Convert.ToHexString(peerIdBytes).ToLowerInvariant();
        return $"peer:vsf:{peerId}";
    }
}
