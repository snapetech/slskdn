// <copyright file="CryptographicCommitment.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements cryptographic commitment protocol for file transfers.
/// SECURITY: Prevents bait-and-switch attacks by requiring hash commitments before transfer.
/// </summary>
public sealed class CryptographicCommitment
{
    private readonly ConcurrentDictionary<string, Commitment> _commitments = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// How long commitments remain valid.
    /// </summary>
    public TimeSpan CommitmentTtl { get; init; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Maximum commitments to track.
    /// </summary>
    public const int MaxCommitments = 10000;

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptographicCommitment"/> class.
    /// </summary>
    public CryptographicCommitment()
    {
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Create a commitment for a file hash.
    /// The commitment is H(hash || nonce), revealing only the commitment.
    /// Later, the full hash and nonce are revealed to verify.
    /// </summary>
    /// <param name="fileHash">The actual file hash (SHA256 hex).</param>
    /// <param name="username">Username making the commitment.</param>
    /// <param name="filename">Filename being committed.</param>
    /// <returns>Commitment details including the commitment hash and nonce.</returns>
    public CommitmentResult CreateCommitment(string fileHash, string username, string filename)
    {
        // Generate random nonce
        var nonceBytes = RandomNumberGenerator.GetBytes(32);
        var nonce = Convert.ToHexString(nonceBytes).ToLowerInvariant();

        // Create commitment: H(hash || nonce)
        var dataToHash = fileHash.ToLowerInvariant() + nonce;
        var commitmentHash = ComputeSha256(dataToHash);

        // Generate commitment ID
        var commitmentId = Guid.NewGuid().ToString("N")[..16];

        var commitment = new Commitment
        {
            Id = commitmentId,
            CommitmentHash = commitmentHash,
            ActualHash = fileHash.ToLowerInvariant(),
            Nonce = nonce,
            Username = username,
            Filename = filename,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CommitmentTtl),
            State = CommitmentState.Pending,
        };

        // Enforce max size
        if (_commitments.Count >= MaxCommitments)
        {
            var oldest = _commitments.Values
                .OrderBy(c => c.CreatedAt)
                .FirstOrDefault();
            if (oldest != null)
            {
                _commitments.TryRemove(oldest.Id, out _);
            }
        }

        _commitments[commitmentId] = commitment;

        return new CommitmentResult
        {
            CommitmentId = commitmentId,
            CommitmentHash = commitmentHash,
            Nonce = nonce,
            ExpiresAt = commitment.ExpiresAt,
        };
    }

    /// <summary>
    /// Verify a commitment by revealing the actual hash.
    /// </summary>
    /// <param name="commitmentId">The commitment ID.</param>
    /// <param name="revealedHash">The revealed file hash.</param>
    /// <param name="nonce">The nonce used in commitment.</param>
    /// <returns>Verification result.</returns>
    public CommitmentVerification VerifyCommitment(string commitmentId, string revealedHash, string nonce)
    {
        if (!_commitments.TryGetValue(commitmentId, out var commitment))
        {
            return CommitmentVerification.Failed("Commitment not found");
        }

        if (commitment.State != CommitmentState.Pending)
        {
            return CommitmentVerification.Failed($"Commitment already {commitment.State}");
        }

        if (commitment.ExpiresAt < DateTimeOffset.UtcNow)
        {
            commitment.State = CommitmentState.Expired;
            return CommitmentVerification.Failed("Commitment expired");
        }

        // Recompute commitment hash
        var dataToHash = revealedHash.ToLowerInvariant() + nonce;
        var computedCommitment = ComputeSha256(dataToHash);

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedCommitment),
            Encoding.UTF8.GetBytes(commitment.CommitmentHash)))
        {
            commitment.State = CommitmentState.Failed;
            return CommitmentVerification.Failed("Commitment hash mismatch - possible bait-and-switch!");
        }

        // Verify the revealed hash matches what was originally committed
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(revealedHash.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(commitment.ActualHash)))
        {
            commitment.State = CommitmentState.Failed;
            return CommitmentVerification.Failed("Revealed hash doesn't match original commitment");
        }

        commitment.State = CommitmentState.Verified;
        commitment.VerifiedAt = DateTimeOffset.UtcNow;

        return CommitmentVerification.Succeeded(commitment.ActualHash);
    }

    /// <summary>
    /// Verify that received content matches a commitment.
    /// </summary>
    /// <param name="commitmentId">The commitment ID.</param>
    /// <param name="contentHash">Hash of received content.</param>
    /// <returns>True if content matches commitment.</returns>
    public bool VerifyContent(string commitmentId, string contentHash)
    {
        if (!_commitments.TryGetValue(commitmentId, out var commitment))
        {
            return false;
        }

        if (commitment.State != CommitmentState.Verified)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(contentHash.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(commitment.ActualHash));
    }

    /// <summary>
    /// Get a commitment by ID.
    /// </summary>
    public Commitment? GetCommitment(string commitmentId)
    {
        return _commitments.TryGetValue(commitmentId, out var c) ? c : null;
    }

    /// <summary>
    /// Cancel a commitment.
    /// </summary>
    public bool CancelCommitment(string commitmentId)
    {
        if (_commitments.TryGetValue(commitmentId, out var commitment))
        {
            commitment.State = CommitmentState.Cancelled;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get statistics about commitments.
    /// </summary>
    public CommitmentStats GetStats()
    {
        var commitments = _commitments.Values.ToList();
        return new CommitmentStats
        {
            TotalCommitments = commitments.Count,
            PendingCommitments = commitments.Count(c => c.State == CommitmentState.Pending),
            VerifiedCommitments = commitments.Count(c => c.State == CommitmentState.Verified),
            FailedCommitments = commitments.Count(c => c.State == CommitmentState.Failed),
            ExpiredCommitments = commitments.Count(c => c.State == CommitmentState.Expired),
        };
    }

    private void CleanupExpired(object? state)
    {
        var now = DateTimeOffset.UtcNow;
        var toRemove = _commitments
            .Where(kvp => kvp.Value.ExpiresAt < now.AddHours(-1)) // Keep for 1 hour after expiry for debugging
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _commitments.TryRemove(id, out _);
        }
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
/// A cryptographic commitment.
/// </summary>
public sealed class Commitment
{
    /// <summary>Gets the commitment ID.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the commitment hash H(hash || nonce).</summary>
    public required string CommitmentHash { get; init; }

    /// <summary>Gets the actual file hash.</summary>
    public required string ActualHash { get; init; }

    /// <summary>Gets the nonce used.</summary>
    public required string Nonce { get; init; }

    /// <summary>Gets the username who made the commitment.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the filename.</summary>
    public required string Filename { get; init; }

    /// <summary>Gets when the commitment was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets when the commitment expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Gets or sets the commitment state.</summary>
    public CommitmentState State { get; set; }

    /// <summary>Gets or sets when the commitment was verified.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }
}

/// <summary>
/// State of a commitment.
/// </summary>
public enum CommitmentState
{
    /// <summary>Waiting for verification.</summary>
    Pending,

    /// <summary>Successfully verified.</summary>
    Verified,

    /// <summary>Verification failed.</summary>
    Failed,

    /// <summary>Commitment expired.</summary>
    Expired,

    /// <summary>Commitment cancelled.</summary>
    Cancelled,
}

/// <summary>
/// Result of creating a commitment.
/// </summary>
public sealed class CommitmentResult
{
    /// <summary>Gets the commitment ID.</summary>
    public required string CommitmentId { get; init; }

    /// <summary>Gets the commitment hash to share with peer.</summary>
    public required string CommitmentHash { get; init; }

    /// <summary>Gets the nonce (keep secret until reveal).</summary>
    public required string Nonce { get; init; }

    /// <summary>Gets when the commitment expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Result of verifying a commitment.
/// </summary>
public sealed class CommitmentVerification
{
    /// <summary>Gets whether verification succeeded.</summary>
    public bool IsValid { get; init; }

    /// <summary>Gets the error message if failed.</summary>
    public string? Error { get; init; }

    /// <summary>Gets the verified hash if successful.</summary>
    public string? VerifiedHash { get; init; }

    /// <summary>Create a successful verification.</summary>
    public static CommitmentVerification Succeeded(string hash) => new()
    {
        IsValid = true,
        VerifiedHash = hash,
    };

    /// <summary>Create a failed verification.</summary>
    public static CommitmentVerification Failed(string error) => new()
    {
        IsValid = false,
        Error = error,
    };
}

/// <summary>
/// Statistics about commitments.
/// </summary>
public sealed class CommitmentStats
{
    /// <summary>Gets total commitments tracked.</summary>
    public int TotalCommitments { get; init; }

    /// <summary>Gets pending commitments.</summary>
    public int PendingCommitments { get; init; }

    /// <summary>Gets verified commitments.</summary>
    public int VerifiedCommitments { get; init; }

    /// <summary>Gets failed commitments.</summary>
    public int FailedCommitments { get; init; }

    /// <summary>Gets expired commitments.</summary>
    public int ExpiredCommitments { get; init; }
}

