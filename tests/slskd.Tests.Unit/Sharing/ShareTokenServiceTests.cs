// <copyright file="ShareTokenServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Sharing;

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using slskd;
using slskd.Sharing;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class ShareTokenServiceTests
{
    private const string ValidSigningKeyBase64 = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="; // 32 bytes

    private static IOptionsMonitor<slskd.Options> CreateOptions(string? tokenSigningKey = ValidSigningKeyBase64)
    {
        var opts = new slskd.Options
        {
            Sharing = new slskd.Options.SharingOptions { TokenSigningKey = tokenSigningKey ?? string.Empty }
        };
        return new TestOptionsMonitor(opts);
    }

    private static ShareTokenService CreateService(IOptionsMonitor<slskd.Options>? options = null, ILogger<ShareTokenService>? log = null)
    {
        return new ShareTokenService(
            options ?? CreateOptions(),
            log ?? Mock.Of<ILogger<ShareTokenService>>());
    }

    [Fact]
    public async Task CreateAsync_ReturnsNonEmptyToken()
    {
        var svc = CreateService();
        var t = await svc.CreateAsync("s1", "c1", null, true, true, 2, TimeSpan.FromHours(1));
        Assert.NotNull(t);
        Assert.True(t.Length > 20);
    }

    [Fact]
    public async Task ValidateAsync_ValidToken_ReturnsClaims()
    {
        var svc = CreateService();
        var token = await svc.CreateAsync("s1", "c1", "a1", true, false, 3, TimeSpan.FromHours(1));
        var claims = await svc.ValidateAsync(token);
        Assert.NotNull(claims);
        Assert.Equal("s1", claims.ShareId);
        Assert.Equal("c1", claims.CollectionId);
        Assert.Equal("a1", claims.AudienceId);
        Assert.True(claims.AllowStream);
        Assert.False(claims.AllowDownload);
        Assert.Equal(3, claims.MaxConcurrentStreams);
    }

    [Fact]
    public async Task ValidateAsync_MismatchedAudienceAndCollectionClaim_ReturnsNull()
    {
        var svc = CreateService();
        var now = DateTime.UtcNow;
        var claims = new[]
        {
            new Claim("share_id", "s1"),
            new Claim("collection_id", "c1"),
            new Claim("stream", "1"),
            new Claim("download", "1"),
            new Claim("max_concurrent_streams", "1"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var key = new SymmetricSecurityKey(Convert.FromBase64String(ValidSigningKeyBase64));
        var token = new JwtSecurityToken(
            issuer: slskd.Program.AppName,
            audience: "c2",
            claims: claims,
            notBefore: now,
            expires: now.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        Assert.Null(await svc.ValidateAsync(encoded));
    }

    [Fact]
    public async Task ValidateAsync_TamperedSignature_ReturnsNull()
    {
        var svc = CreateService();
        var token = await svc.CreateAsync("s1", "c1", null, true, true, 1, TimeSpan.FromHours(1));
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        var tampered = string.Join('.', parts[0], parts[1], parts[2][..^1] + (parts[2][^1] == 'A' ? "B" : "A"));

        Assert.Null(await svc.ValidateAsync(tampered));
    }

    [Fact]
    public async Task ValidateAsync_ExpiredToken_ReturnsNull()
    {
        var svc = CreateService();
        var token = await svc.CreateAsync("s1", "c1", null, true, true, 1, TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        var claims = await svc.ValidateAsync(token);
        Assert.Null(claims);
    }

    [Fact]
    public async Task ValidateAsync_EmptyOrNullToken_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(await svc.ValidateAsync(""));
        Assert.Null(await svc.ValidateAsync("   "));
        Assert.Null(await svc.ValidateAsync("invalid-jwt"));
    }

    [Fact]
    public void CreateAsync_NoTokenSigningKey_Throws()
    {
        var opts = CreateOptions(tokenSigningKey: null);
        var svc = CreateService(opts);
        Assert.Throws<InvalidOperationException>(() =>
            svc.CreateAsync("s1", "c1", null, true, true, 1, TimeSpan.FromHours(1)).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task ValidateAsync_NoTokenSigningKey_ReturnsNull()
    {
        var opts = CreateOptions(null);
        var svc = CreateService(opts);
        var claims = await svc.ValidateAsync("any");
        Assert.Null(claims);
    }
}
