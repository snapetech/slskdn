// <copyright file="ProofOfStorage.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements proof-of-storage challenges to verify file possession.
/// SECURITY: Prevents peers from advertising files they don't actually have.
/// </summary>
public sealed class ProofOfStorage
{
    private readonly ConcurrentDictionary<string, Challenge> _pendingChallenges = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Default challenge size in bytes.
    /// </summary>
    public const int DefaultChallengeSize = 4096;

    /// <summary>
    /// How long challenges remain valid.
    /// </summary>
    public TimeSpan ChallengeTtl { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum pending challenges.
    /// </summary>
    public const int MaxPendingChallenges = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProofOfStorage"/> class.
    /// </summary>
    public ProofOfStorage()
    {
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <summary>
    /// Create a challenge for a peer to prove they have a file.
    /// Requests the hash of bytes at a random offset.
    /// </summary>
    /// <param name="filename">The file to challenge.</param>
    /// <param name="fileSize">Known file size.</param>
    /// <param name="username">Username being challenged.</param>
    /// <param name="challengeSize">Size of chunk to hash (default 4KB).</param>
    /// <returns>Challenge details to send to peer.</returns>
    public ChallengeRequest CreateChallenge(string filename, long fileSize, string username, int challengeSize = DefaultChallengeSize)
    {
        if (fileSize < challengeSize)
        {
            // For small files, challenge the whole file
            challengeSize = (int)fileSize;
        }

        // Pick random offset
        var maxOffset = fileSize - challengeSize;
        var offset = maxOffset > 0 ? RandomNumberGenerator.GetInt32(0, (int)Math.Min(maxOffset, int.MaxValue)) : 0;

        // Generate challenge nonce
        var nonceBytes = RandomNumberGenerator.GetBytes(16);
        var nonce = Convert.ToHexString(nonceBytes).ToLowerInvariant();

        var challengeId = Guid.NewGuid().ToString("N")[..16];

        var challenge = new Challenge
        {
            Id = challengeId,
            Filename = filename,
            FileSize = fileSize,
            Offset = offset,
            Length = challengeSize,
            Nonce = nonce,
            Username = username,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(ChallengeTtl),
            State = ChallengeState.Pending,
        };

        // Enforce max size
        if (_pendingChallenges.Count >= MaxPendingChallenges)
        {
            var oldest = _pendingChallenges.Values
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefault();
            if (oldest != null)
            {
                _pendingChallenges.TryRemove(oldest.Id, out _);
            }
        }

        _pendingChallenges[challengeId] = challenge;

        return new ChallengeRequest
        {
            ChallengeId = challengeId,
            Offset = offset,
            Length = challengeSize,
            Nonce = nonce,
            ExpiresAt = challenge.ExpiresAt,
        };
    }

    /// <summary>
    /// Respond to a challenge by providing the hash of the requested chunk.
    /// Call this locally to generate the response.
    /// </summary>
    /// <param name="filePath">Path to the local file.</param>
    /// <param name="offset">Offset requested.</param>
    /// <param name="length">Length requested.</param>
    /// <param name="nonce">Challenge nonce.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Challenge response hash.</returns>
    public async Task<string> GenerateResponseAsync(
        string filePath,
        long offset,
        int length,
        string nonce,
        CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        if (stream.Length < offset + length)
        {
            throw new InvalidOperationException($"File too small: {stream.Length} < {offset + length}");
        }

        stream.Seek(offset, SeekOrigin.Begin);

        var buffer = new byte[length];
        await stream.ReadExactlyAsync(buffer, cancellationToken);

        // Hash: SHA256(nonce || chunk_data)
        var nonceBytes = Encoding.UTF8.GetBytes(nonce);
        var combined = new byte[nonceBytes.Length + buffer.Length];
        Buffer.BlockCopy(nonceBytes, 0, combined, 0, nonceBytes.Length);
        Buffer.BlockCopy(buffer, 0, combined, nonceBytes.Length, buffer.Length);

        var hash = SHA256.HashData(combined);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Verify a challenge response from a peer.
    /// Requires knowing what the correct response should be.
    /// </summary>
    /// <param name="challengeId">The challenge ID.</param>
    /// <param name="response">The response hash from peer.</param>
    /// <param name="expectedResponse">The expected response (computed from local file).</param>
    /// <returns>Verification result.</returns>
    public ChallengeVerification VerifyResponse(string challengeId, string response, string expectedResponse)
    {
        if (!_pendingChallenges.TryGetValue(challengeId, out var challenge))
        {
            return ChallengeVerification.Failed("Challenge not found");
        }

        if (challenge.State != ChallengeState.Pending)
        {
            return ChallengeVerification.Failed($"Challenge already {challenge.State}");
        }

        if (challenge.ExpiresAt < DateTimeOffset.UtcNow)
        {
            challenge.State = ChallengeState.Expired;
            return ChallengeVerification.Failed("Challenge expired");
        }

        // Use constant-time comparison
        var responseValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(response.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedResponse.ToLowerInvariant()));

        if (!responseValid)
        {
            challenge.State = ChallengeState.Failed;
            return ChallengeVerification.Failed("Invalid proof - peer may not have the file");
        }

        challenge.State = ChallengeState.Verified;
        challenge.VerifiedAt = DateTimeOffset.UtcNow;

        return ChallengeVerification.Succeeded();
    }

    /// <summary>
    /// Get a pending challenge.
    /// </summary>
    public Challenge? GetChallenge(string challengeId)
    {
        return _pendingChallenges.TryGetValue(challengeId, out var c) ? c : null;
    }

    /// <summary>
    /// Cancel a challenge.
    /// </summary>
    public bool CancelChallenge(string challengeId)
    {
        if (_pendingChallenges.TryGetValue(challengeId, out var challenge))
        {
            challenge.State = ChallengeState.Cancelled;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get statistics about challenges.
    /// </summary>
    public ChallengeStats GetStats()
    {
        var challenges = _pendingChallenges.Values.ToList();
        return new ChallengeStats
        {
            TotalChallenges = challenges.Count,
            PendingChallenges = challenges.Count(c => c.State == ChallengeState.Pending),
            VerifiedChallenges = challenges.Count(c => c.State == ChallengeState.Verified),
            FailedChallenges = challenges.Count(c => c.State == ChallengeState.Failed),
            ExpiredChallenges = challenges.Count(c => c.State == ChallengeState.Expired),
        };
    }

    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var toRemove = _pendingChallenges
            .Where(kvp => kvp.Value.ExpiresAt < now.AddMinutes(-10))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _pendingChallenges.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Dispose resources.
    /// </summary>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// A proof-of-storage challenge.
/// </summary>
public sealed class Challenge
{
    /// <summary>Gets the challenge ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the filename being challenged.</summary>
    public required string Filename { get; init; }

    /// <summary>Gets the known file size.</summary>
    public required long FileSize { get; init; }

    /// <summary>Gets the offset to read from.</summary>
    public required long Offset { get; init; }

    /// <summary>Gets the length to read.</summary>
    public required int Length { get; init; }

    /// <summary>Gets the challenge nonce.</summary>
    public required string Nonce { get; init; }

    /// <summary>Gets the username being challenged.</summary>
    public required string Username { get; init; }

    /// <summary>Gets when the challenge was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the challenge expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Gets or sets the challenge state.</summary>
    public ChallengeState State { get; set; }

    /// <summary>Gets or sets when the challenge was verified.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }
}

/// <summary>
/// State of a challenge.
/// </summary>
public enum ChallengeState
{
    /// <summary>Waiting for response.</summary>
    Pending,

    /// <summary>Successfully verified.</summary>
    Verified,

    /// <summary>Verification failed.</summary>
    Failed,

    /// <summary>Challenge expired.</summary>
    Expired,

    /// <summary>Challenge cancelled.</summary>
    Cancelled,
}

/// <summary>
/// Challenge request to send to peer.
/// </summary>
public sealed class ChallengeRequest
{
    /// <summary>Gets the challenge ID.</summary>
    public required string ChallengeId { get; init; }

    /// <summary>Gets the offset to read from.</summary>
    public required long Offset { get; init; }

    /// <summary>Gets the length to read.</summary>
    public required int Length { get; init; }

    /// <summary>Gets the challenge nonce.</summary>
    public required string Nonce { get; init; }

    /// <summary>Gets when the challenge expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Result of verifying a challenge.
/// </summary>
public sealed class ChallengeVerification
{
    /// <summary>Gets whether verification succeeded.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets the error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Create a successful verification.</summary>
    public static ChallengeVerification Succeeded() => new() { IsValid = true };

    /// <summary>Create a failed verification.</summary>
    public static ChallengeVerification Failed(string error) => new()
    {
        IsValid = false,
        Error = error,
    };
}

/// <summary>
/// Statistics about challenges.
/// </summary>
public sealed class ChallengeStats
{
    /// <summary>Gets total challenges.</summary>
    public int TotalChallenges { get; init; }

    /// <summary>Gets pending challenges.</summary>
    public int PendingChallenges { get; init; }

    /// <summary>Gets verified challenges.</summary>
    public int VerifiedChallenges { get; init; }

    /// <summary>Gets failed challenges.</summary>
    public int FailedChallenges { get; init; }

    /// <summary>Gets expired challenges.</summary>
    public int ExpiredChallenges { get; init; }
}

