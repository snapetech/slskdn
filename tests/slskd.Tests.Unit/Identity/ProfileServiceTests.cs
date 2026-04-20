// <copyright file="ProfileServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

#nullable enable

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
using slskd.Tests.Unit;
using System.Reflection;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

[Collection("ProgramAppDirectory")]
public class ProfileServiceTests : IDisposable
{
    private readonly Ed25519Signer _signer = new();
    private readonly Mock<ILogger<ProfileService>> _logMock = new();
    private readonly string _tempDir;
    private readonly string? _originalAppDirectory;
    private IOptionsMonitor<slskd.Options> _options;

    public ProfileServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ProfileServiceTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _originalAppDirectory = Program.AppDirectory;
        SetAppDirectory(_tempDir);
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
            var profileFile = GetProfileFilePath();
            if (File.Exists(profileFile)) File.Delete(profileFile);
            var keyFile = Path.ChangeExtension(profileFile, ".key");
            if (File.Exists(keyFile)) File.Delete(keyFile);
        }
        catch { }
        SetAppDirectory(_originalAppDirectory);
        try { Directory.Delete(_tempDir, true); } catch { }
        _signer?.Dispose();
    }

    private ProfileService CreateService()
    {
        return new ProfileService(_signer, _options, _logMock.Object);
    }

    [Fact]
    public async Task GetMyProfile_FirstCall_GeneratesProfile()
    {
        // Delete any existing profile file to ensure fresh generation
        var profileFile = GetProfileFilePath();
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
    public async Task GetMyProfile_FirstCall_SetsRestrictiveKeyPermissionsOnUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        var profileFile = GetProfileFilePath();
        var keyFile = Path.ChangeExtension(profileFile, ".key");
        try { if (File.Exists(profileFile)) File.Delete(profileFile); } catch { }
        try { if (File.Exists(keyFile)) File.Delete(keyFile); } catch { }

        var svc = CreateService();
        _ = await svc.GetMyProfileAsync(CancellationToken.None);

        var mode = File.GetUnixFileMode(keyFile);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
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

    // HARDENING-2026-04-20 H10: operator submits a mix of public and leaky endpoints; only the
    // public ones must make it into the signed profile served anonymously over GET /profile/{id}.
    [Fact]
    public async Task UpdateMyProfile_StripsLeakyEndpoints_H10()
    {
        var svc = CreateService();
        _ = await svc.GetMyProfileAsync(CancellationToken.None);

        var mixed = new List<PeerEndpoint>
        {
            new() { Type = "Direct", Address = "https://peer.example.com:5030", Priority = 1 },
            new() { Type = "Direct", Address = "https://192.168.1.100:5030", Priority = 2 },
            new() { Type = "Direct", Address = "http://127.0.0.1:5030", Priority = 3 },
            new() { Type = "Direct", Address = "https://169.254.169.254/", Priority = 4 },
            new() { Type = "Direct", Address = "https://server.internal:5030", Priority = 5 },
        };

        var updated = await svc.UpdateMyProfileAsync("TestUser", null, 0, mixed, CancellationToken.None);

        Assert.Single(updated.Endpoints);
        Assert.Equal("https://peer.example.com:5030", updated.Endpoints[0].Address);
    }

    // HARDENING-2026-04-20 H10: a pre-hardening profile saved to disk with a LAN-IP endpoint
    // must be migrated on load (leaky entries dropped, re-signed, persisted).
    [Fact]
    public async Task GetMyProfile_MigratesPreHardeningProfileWithLeakyEndpoint_H10()
    {
        var profileFile = GetProfileFilePath();
        var keyFile = Path.ChangeExtension(profileFile, ".key");
        try { if (File.Exists(profileFile)) File.Delete(profileFile); } catch { }
        try { if (File.Exists(keyFile)) File.Delete(keyFile); } catch { }

        // Let the service mint a valid keypair and a throwaway profile, then replace its
        // endpoints with a leaky one and re-sign so the on-disk blob looks like a legacy record.
        var bootstrap = CreateService();
        var seed = await bootstrap.GetMyProfileAsync(CancellationToken.None);
        seed.Endpoints = new List<PeerEndpoint>
        {
            new() { Type = "Direct", Address = "https://192.168.50.85:5030", Priority = 1 },
        };
        var legacy = bootstrap.SignProfile(seed);
        await System.IO.File.WriteAllTextAsync(
            profileFile,
            System.Text.Json.JsonSerializer.Serialize(legacy, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            CancellationToken.None);

        // New service instance (clean cache) — load + migrate.
        var svc = CreateService();
        var loaded = await svc.GetMyProfileAsync(CancellationToken.None);

        Assert.Empty(loaded.Endpoints);
        Assert.True(svc.VerifyProfile(loaded));

        // Disk copy should be the scrubbed, re-signed one.
        var onDiskJson = await System.IO.File.ReadAllTextAsync(profileFile, CancellationToken.None);
        var onDisk = System.Text.Json.JsonSerializer.Deserialize<PeerProfile>(onDiskJson);
        Assert.NotNull(onDisk);
        Assert.Empty(onDisk!.Endpoints);
    }

    private static void SetAppDirectory(string? value)
    {
        const string propertyName = nameof(Program.AppDirectory);
        var property = typeof(Program).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var setter = property?.GetSetMethod(nonPublic: true);
        Assert.NotNull(property);
        Assert.NotNull(setter);
        setter!.Invoke(null, new object[] { value ?? string.Empty });
    }

    private static string GetProfileFilePath() => Path.Combine(Program.GetWriteBaseDirectory(), "peer-profile.json");
}
