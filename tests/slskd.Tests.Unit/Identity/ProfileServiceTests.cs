// <copyright file="ProfileServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Identity;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Identity;
using slskd.Mesh.Transport;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class ProfileServiceTests : IDisposable
{
    private readonly Ed25519Signer _signer = new();
    private readonly Mock<ILogger<ProfileService>> _logMock = new();
    private readonly string _tempDir;
    private IOptionsMonitor<slskd.Options> _options;

    public ProfileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ProfileServiceTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Directories = new slskd.Options.DirectoriesOptions(),
            Soulseek = new slskd.Options.SoulseekOptions { Username = "testuser" },
            Web = new slskd.Options.WebOptions { Port = 8080 }
        });
    }

    public void Dispose()
    {
        // Clean up any profile files created in Program.AppDirectory
        try
        {
            var profileFile = Path.Combine(Program.AppDirectory ?? _tempDir, "peer-profile.json");
            if (File.Exists(profileFile)) File.Delete(profileFile);
            var keyFile = Path.Combine(Program.AppDirectory ?? _tempDir, "peer-profile.key");
            if (File.Exists(keyFile)) File.Delete(keyFile);
        }
        catch { }
        try { Directory.Delete(_tempDir, true); } catch { }
        _signer?.Dispose();
    }

    private ProfileService CreateService()
    {
        // ProfileService uses Program.AppDirectory which is set at startup
        // In tests, Program.AppDirectory might be null, so we need to ensure it's set
        if (string.IsNullOrEmpty(Program.AppDirectory))
        {
            // Use reflection or just ensure the directory exists
            // For now, tests will fail if AppDirectory is null - that's expected
            // In a real scenario, Program.AppDirectory is set during startup
        }
        return new ProfileService(_signer, _options, _logMock.Object);
    }

    [Fact]
    public async Task GetMyProfile_FirstCall_GeneratesProfile()
    {
        // Delete any existing profile file to ensure fresh generation
        var profileFile = Path.Combine(Program.AppDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "slskd"), "peer-profile.json");
        var keyFile = Path.ChangeExtension(profileFile, ".key");
        try { if (File.Exists(profileFile)) File.Delete(profileFile); } catch { }
        try { if (File.Exists(keyFile)) File.Delete(keyFile); } catch { }

        var svc = CreateService();
        var p = await svc.GetMyProfileAsync(CancellationToken.None);

        Assert.NotNull(p);
        Assert.NotNull(p.PeerId);
        Assert.NotNull(p.PublicKey);
        // DisplayName comes from Options.Soulseek.Username which is "testuser"
        Assert.Equal("testuser", p.DisplayName);
        Assert.NotNull(p.Signature);
    }

    [Fact]
    public async Task GetMyProfile_SecondCall_ReturnsCached()
    {
        var svc = CreateService();
        var p1 = await svc.GetMyProfileAsync(CancellationToken.None);
        var p2 = await svc.GetMyProfileAsync(CancellationToken.None);

        Assert.Equal(p1.PeerId, p2.PeerId);
        Assert.Equal(p1.PublicKey, p2.PublicKey);
    }

    [Fact]
    public async Task UpdateMyProfile_UpdatesAndReSigns()
    {
        var svc = CreateService();
        // Ensure we have an initial profile
        var original = await svc.GetMyProfileAsync(CancellationToken.None);
        var originalSig = original.Signature;
        var endpoints = new List<PeerEndpoint> { new() { Type = "Direct", Address = "https://example.com", Priority = 1 } };

        var updated = await svc.UpdateMyProfileAsync("NewName", "avatar.png", 1, endpoints, CancellationToken.None);

        Assert.Equal("NewName", updated.DisplayName);
        Assert.Equal("avatar.png", updated.Avatar);
        Assert.Equal(1, updated.Capabilities);
        Assert.Single(updated.Endpoints);
        Assert.NotNull(updated.Signature);
        // Signature should be different after update (profile content changed)
        Assert.NotEqual(originalSig, updated.Signature);
    }

    [Fact]
    public async Task VerifyProfile_ValidSignature_ReturnsTrue()
    {
        var svc = CreateService();
        // Get a real profile (which will be signed)
        var profile = await svc.GetMyProfileAsync(CancellationToken.None);

        var verified = svc.VerifyProfile(profile);

        Assert.True(verified);
    }

    [Fact]
    public void VerifyProfile_InvalidSignature_ReturnsFalse()
    {
        var svc = CreateService();
        var profile = new PeerProfile
        {
            PeerId = "test",
            PublicKey = Convert.ToBase64String(new byte[32]),
            DisplayName = "Test",
            Signature = "invalid",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        var verified = svc.VerifyProfile(profile);

        Assert.False(verified);
    }

    [Fact]
    public void SignProfile_SetsPeerIdAndPublicKey()
    {
        var svc = CreateService();
        var profile = new PeerProfile
        {
            DisplayName = "Test",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };

        var signed = svc.SignProfile(profile);

        Assert.NotNull(signed.PeerId);
        Assert.NotNull(signed.PublicKey);
        Assert.NotNull(signed.Signature);
    }

    [Fact]
    public void GetFriendCode_ReturnsFormattedCode()
    {
        var svc = CreateService();
        var code = svc.GetFriendCode("test-peer-id");

        Assert.NotNull(code);
        Assert.Contains("-", code);
        var parts = code.Split('-');
        Assert.Equal(4, parts.Length);
        Assert.All(parts, p => Assert.True(p.Length >= 3 && p.Length <= 5));
    }

    [Fact]
    public void GetFriendCode_SameInput_ReturnsSameCode()
    {
        var svc = CreateService();
        var code1 = svc.GetFriendCode("test-peer-id");
        var code2 = svc.GetFriendCode("test-peer-id");

        Assert.Equal(code1, code2);
    }

    [Fact]
    public void DecodeFriendCode_ReturnsNull()
    {
        var svc = CreateService();
        var decoded = svc.DecodeFriendCode("ABCD-EFGH-IJKL-MNOP");

        Assert.Null(decoded);
    }
}
