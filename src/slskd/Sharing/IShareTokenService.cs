// <copyright file="IShareTokenService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

/// <summary>
/// Creates and validates share tokens used for capability-based access to streams and manifest.
/// Tokens are HMAC-SHA256–signed JWTs. No client paths; constant-time validation where applicable.
/// </summary>
public interface IShareTokenService
{
    /// <summary>
    /// Creates a share token with the given claims. Expiry is enforced at validation.
    /// </summary>
    /// <param name="shareId">Share (grant) id.</param>
    /// <param name="collectionId">Collection id.</param>
    /// <param name="audienceId">Optional audience (user or group) id.</param>
    /// <param name="allowStream">Whether streaming is allowed.</param>
    /// <param name="allowDownload">Whether download is allowed.</param>
    /// <param name="maxConcurrentStreams">Max concurrent streams for this token.</param>
    /// <param name="expiresIn">Token lifetime from now.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Encoded token string.</returns>
    /// <exception cref="InvalidOperationException">TokenSigningKey not configured or too short.</exception>
    Task<string> CreateAsync(
        string shareId,
        string collectionId,
        string? audienceId,
        bool allowStream,
        bool allowDownload,
        int maxConcurrentStreams,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a share token and returns claims if valid. Uses constant-time signature verification.
    /// Returns null if malformed, signature invalid, or expired.
    /// </summary>
    /// <param name="token">The encoded token (e.g. from Bearer or ?token=).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Decoded claims or null.</returns>
    Task<ShareTokenClaims?> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Decoded, validated share token claims.
/// </summary>
/// <param name="ShareId">Share (grant) id.</param>
/// <param name="CollectionId">Collection id.</param>
/// <param name="AudienceId">Optional audience id.</param>
/// <param name="AllowStream">Whether streaming is allowed.</param>
/// <param name="AllowDownload">Whether download is allowed.</param>
/// <param name="MaxConcurrentStreams">Max concurrent streams.</param>
/// <param name="ExpiresAtUtc">Expiry time (UTC).</param>
public sealed record ShareTokenClaims(
    string ShareId,
    string CollectionId,
    string? AudienceId,
    bool AllowStream,
    bool AllowDownload,
    int MaxConcurrentStreams,
    DateTimeOffset ExpiresAtUtc);
