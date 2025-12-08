// <copyright file="PeerVerificationService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soulseek;

/// <summary>
/// Service for verifying that overlay peers are who they claim to be.
/// Uses Soulseek UserInfo to verify username ownership.
/// </summary>
public sealed class PeerVerificationService
{
    private readonly ILogger<PeerVerificationService> _logger;
    private readonly ISoulseekClient _soulseekClient;
    private readonly ConcurrentDictionary<string, VerificationRecord> _verificationCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);
    private readonly TimeSpan _verificationTimeout = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Challenge prefix for verification tokens.
    /// </summary>
    public const string ChallengePrefix = "slskdn-verify-v1:";
    
    public PeerVerificationService(
        ILogger<PeerVerificationService> logger,
        ISoulseekClient soulseekClient)
    {
        _logger = logger;
        _soulseekClient = soulseekClient;
    }
    
    /// <summary>
    /// Generate a challenge token for a peer to include in their UserInfo.
    /// </summary>
    /// <param name="username">The claimed username.</param>
    /// <param name="nonce">A random nonce for this verification session.</param>
    /// <returns>The challenge token the peer should include in their description.</returns>
    public static string GenerateChallenge(string username, string nonce)
    {
        // Challenge format: slskdn-verify-v1:<nonce>:<hash>
        // Hash = first 8 chars of SHA256(nonce + username)
        var hashInput = $"{nonce}:{username}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)))[..8];
        return $"{ChallengePrefix}{nonce}:{hash}";
    }
    
    /// <summary>
    /// Generate a random nonce for verification.
    /// </summary>
    public static string GenerateNonce()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
    }
    
    /// <summary>
    /// Verify that a peer controls the claimed Soulseek username.
    /// </summary>
    /// <param name="claimedUsername">The username claimed in the overlay handshake.</param>
    /// <param name="expectedChallenge">The challenge we sent them to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    public async Task<VerificationResult> VerifyPeerAsync(
        string claimedUsername,
        string expectedChallenge,
        CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_verificationCache.TryGetValue(claimedUsername, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.VerifiedAt < _cacheExpiry)
            {
                _logger.LogDebug("Using cached verification for {Username}", claimedUsername);
                return cached.Result;
            }
            else
            {
                _verificationCache.TryRemove(claimedUsername, out _);
            }
        }
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_verificationTimeout);
            
            // Get the user's info from Soulseek
            var userInfo = await _soulseekClient.GetUserInfoAsync(claimedUsername, cts.Token);
            
            if (userInfo is null)
            {
                _logger.LogWarning("Could not get UserInfo for {Username}", claimedUsername);
                return VerificationResult.Failed("User not found on Soulseek");
            }
            
            // Check if their description contains our challenge
            var description = userInfo.Description ?? string.Empty;
            
            if (description.Contains(expectedChallenge))
            {
                _logger.LogInformation("Verified username {Username} via Soulseek UserInfo challenge", claimedUsername);
                var result = VerificationResult.Success();
                CacheVerification(claimedUsername, result);
                return result;
            }
            
            // Check if they have any slskdn capability tag (weaker verification)
            if (description.Contains("slskdn_caps:") || description.Contains("slskdn/"))
            {
                _logger.LogDebug("Username {Username} has slskdn tags but not our specific challenge", claimedUsername);
                var result = VerificationResult.Partial("Has slskdn tags but challenge not found");
                return result;
            }
            
            _logger.LogWarning("Username verification failed for {Username}: challenge not found in description", claimedUsername);
            return VerificationResult.Failed("Challenge not found in user description");
        }
        catch (UserOfflineException)
        {
            _logger.LogDebug("User {Username} is offline, cannot verify", claimedUsername);
            return VerificationResult.Failed("User is offline");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Verification timed out for {Username}", claimedUsername);
            return VerificationResult.Failed("Verification timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying username {Username}", claimedUsername);
            return VerificationResult.Failed($"Error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Quick check if a user appears to be an slskdn client based on their UserInfo.
    /// Does not verify identity, just checks for slskdn markers.
    /// </summary>
    public async Task<bool> IsSlskdnClientAsync(string username, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            
            var userInfo = await _soulseekClient.GetUserInfoAsync(username, cts.Token);
            var description = userInfo?.Description ?? string.Empty;
            
            return description.Contains("slskdn_caps:") || 
                   description.Contains("slskdn/") ||
                   description.Contains(ChallengePrefix);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Get verification statistics.
    /// </summary>
    public VerificationStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        var validCached = 0;
        var expiredCached = 0;
        
        foreach (var kvp in _verificationCache)
        {
            if (now - kvp.Value.VerifiedAt < _cacheExpiry)
            {
                validCached++;
            }
            else
            {
                expiredCached++;
            }
        }
        
        return new VerificationStats
        {
            CachedVerifications = validCached,
            ExpiredCacheEntries = expiredCached,
            CacheExpiryMinutes = (int)_cacheExpiry.TotalMinutes,
        };
    }
    
    /// <summary>
    /// Clear verification cache for a specific user.
    /// </summary>
    public void ClearCache(string username)
    {
        _verificationCache.TryRemove(username, out _);
    }
    
    /// <summary>
    /// Clear all cached verifications.
    /// </summary>
    public void ClearAllCache()
    {
        _verificationCache.Clear();
    }
    
    private void CacheVerification(string username, VerificationResult result)
    {
        _verificationCache[username] = new VerificationRecord
        {
            Result = result,
            VerifiedAt = DateTimeOffset.UtcNow,
        };
    }
    
    private sealed class VerificationRecord
    {
        public required VerificationResult Result { get; init; }
        public required DateTimeOffset VerifiedAt { get; init; }
    }
}

/// <summary>
/// Result of username verification.
/// </summary>
public sealed class VerificationResult
{
    public bool IsVerified { get; init; }
    public bool IsPartial { get; init; }
    public string? FailureReason { get; init; }
    
    public static VerificationResult Success() => new() { IsVerified = true };
    public static VerificationResult Partial(string reason) => new() { IsPartial = true, FailureReason = reason };
    public static VerificationResult Failed(string reason) => new() { FailureReason = reason };
}

/// <summary>
/// Verification service statistics.
/// </summary>
public sealed class VerificationStats
{
    public int CachedVerifications { get; init; }
    public int ExpiredCacheEntries { get; init; }
    public int CacheExpiryMinutes { get; init; }
}

