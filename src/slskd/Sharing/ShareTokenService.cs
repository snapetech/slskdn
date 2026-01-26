// <copyright file="ShareTokenService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Sharing;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// HMAC-SHA256–signed JWT share token service. Uses Options.Sharing.TokenSigningKey.
/// </summary>
public sealed class ShareTokenService : IShareTokenService
{
    private const string ClaimShareId = "share_id";
    private const string ClaimCollectionId = "collection_id";
    private const string ClaimAudienceId = "audience_id";
    private const string ClaimStream = "stream";
    private const string ClaimDownload = "download";
    private const string ClaimMaxConcurrentStreams = "max_concurrent_streams";

    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<ShareTokenService> _log;

    public ShareTokenService(IOptionsMonitor<slskd.Options> options, ILogger<ShareTokenService> log)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public Task<string> CreateAsync(
        string shareId,
        string collectionId,
        string? audienceId,
        bool allowStream,
        bool allowDownload,
        int maxConcurrentStreams,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        var key = GetSigningKey();
        var now = DateTime.UtcNow;
        var expires = now.Add(expiresIn);

        var claims = new List<Claim>
        {
            new(ClaimShareId, shareId ?? string.Empty),
            new(ClaimCollectionId, collectionId ?? string.Empty),
            new(ClaimStream, allowStream ? "1" : "0"),
            new(ClaimDownload, allowDownload ? "1" : "0"),
            new(ClaimMaxConcurrentStreams, maxConcurrentStreams.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (!string.IsNullOrEmpty(audienceId))
            claims.Add(new Claim(ClaimAudienceId, audienceId));

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: slskd.Program.AppName,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult(encoded);
    }

    /// <inheritdoc />
    public Task<ShareTokenClaims?> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return Task.FromResult<ShareTokenClaims?>(null);

        SymmetricSecurityKey key;
        try
        {
            key = GetSigningKey();
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult<ShareTokenClaims?>(null);
        }
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = slskd.Program.AppName,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero,
            }, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt)
                return Task.FromResult<ShareTokenClaims?>(null);

            var shareId = principal.FindFirst(ClaimShareId)?.Value;
            var collectionId = principal.FindFirst(ClaimCollectionId)?.Value;
            var audienceId = principal.FindFirst(ClaimAudienceId)?.Value;
            var stream = principal.FindFirst(ClaimStream)?.Value == "1";
            var download = principal.FindFirst(ClaimDownload)?.Value == "1";
            var maxStr = principal.FindFirst(ClaimMaxConcurrentStreams)?.Value;
            _ = int.TryParse(maxStr, out var maxConcurrent);
            var exp = jwt.ValidTo;

            if (string.IsNullOrEmpty(shareId) || string.IsNullOrEmpty(collectionId))
            {
                _log.LogDebug("[ShareToken] Missing share_id or collection_id");
                return Task.FromResult<ShareTokenClaims?>(null);
            }

            return Task.FromResult<ShareTokenClaims?>(new ShareTokenClaims(
                shareId, collectionId, audienceId, stream, download, maxConcurrent,
                new DateTimeOffset(exp, TimeSpan.Zero)));
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "[ShareToken] Validation failed");
            return Task.FromResult<ShareTokenClaims?>(null);
        }
    }

    private SymmetricSecurityKey GetSigningKey()
    {
        var raw = _options.CurrentValue.Sharing.TokenSigningKey;
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Sharing:TokenSigningKey is not configured. Set slskd:Sharing:TokenSigningKey (base64, min 32 bytes decoded).");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(raw.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Sharing:TokenSigningKey must be valid base64.", ex);
        }

        if (bytes.Length < 32)
            throw new InvalidOperationException("Sharing:TokenSigningKey decoded length must be at least 32 bytes.");

        return new SymmetricSecurityKey(bytes);
    }
}
