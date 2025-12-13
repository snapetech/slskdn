// <copyright file="LocalMeshIdentityServiceTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Identity;

using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.Mesh.Identity;
using Xunit;

public class LocalMeshIdentityServiceTests : IDisposable
{
    private readonly string _testKeyPath;
    private readonly string _testDirectory;

    public LocalMeshIdentityServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"slskdn-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
        _testKeyPath = Path.Combine(_testDirectory, "test-identity.key");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void Constructor_GeneratesNewKeys_WhenFileDoesNotExist()
    {
        // Act
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert
        Assert.True(File.Exists(_testKeyPath));
        Assert.NotNull(service.PublicKey);
        Assert.Equal(32, service.PublicKey.Length); // Ed25519 public key is 32 bytes
        Assert.NotEqual(default(MeshPeerId), service.MeshPeerId);
    }

    [Fact]
    public void Constructor_LoadsExistingKeys_WhenFileExists()
    {
        // Arrange - Create first instance to generate keys
        var service1 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var originalPeerId = service1.MeshPeerId;
        var originalPublicKey = service1.PublicKey;

        // Act - Create second instance to load existing keys
        var service2 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert
        Assert.Equal(originalPeerId, service2.MeshPeerId);
        Assert.Equal(originalPublicKey, service2.PublicKey);
    }

    [Fact]
    public void Sign_ProducesValidSignature()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var data = System.Text.Encoding.UTF8.GetBytes("test message");

        // Act
        var signature = service.Sign(data);

        // Assert
        Assert.NotNull(signature);
        Assert.Equal(64, signature.Length); // Ed25519 signature is 64 bytes
    }

    [Fact]
    public void Verify_SucceedsWithValidSignature()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = service.Sign(data);

        // Act
        var result = LocalMeshIdentityService.Verify(data, signature, service.PublicKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_FailsWithInvalidSignature()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = service.Sign(data);

        // Tamper with signature
        signature[0] ^= 0xFF;

        // Act
        var result = LocalMeshIdentityService.Verify(data, signature, service.PublicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_FailsWithModifiedData()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var originalData = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = service.Sign(originalData);

        // Act - Verify with different data
        var modifiedData = System.Text.Encoding.UTF8.GetBytes("modified message");
        var result = LocalMeshIdentityService.Verify(modifiedData, signature, service.PublicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_FailsWithWrongPublicKey()
    {
        // Arrange
        var service1 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = service1.Sign(data);

        // Create different key
        var otherKeyPath = Path.Combine(_testDirectory, "other-identity.key");
        var service2 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            otherKeyPath);

        // Act - Verify with wrong public key
        var result = LocalMeshIdentityService.Verify(data, signature, service2.PublicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_ReturnsFalse_WithInvalidPublicKeyFormat()
    {
        // Arrange
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = new byte[64];
        var invalidPublicKey = new byte[16]; // Wrong length

        // Act
        var result = LocalMeshIdentityService.Verify(data, signature, invalidPublicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MeshPeerId_IsDerivedFromPublicKey()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Act - Compute expected PeerId manually
        var expectedPeerId = MeshPeerId.FromPublicKey(service.PublicKey);

        // Assert
        Assert.Equal(expectedPeerId, service.MeshPeerId);
    }

    [Fact]
    public void MeshPeerId_IsStableAcrossInstances()
    {
        // Arrange
        var service1 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var peerId1 = service1.MeshPeerId;

        // Act - Load same keys in new instance
        var service2 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var peerId2 = service2.MeshPeerId;

        // Assert
        Assert.Equal(peerId1, peerId2);
    }

    [Fact]
    public void KeyFile_HasSecurePermissions_OnUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return; // Skip on non-Unix systems
        }

        // Arrange & Act
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert
        var permissions = File.GetUnixFileMode(_testKeyPath);
        
        // Should be 0600 (owner read/write only)
        Assert.Equal(
            System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite,
            permissions);
    }

    [Fact]
    public void LoadKeys_AppliesSecurePermissions_ToExistingFile()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return; // Skip on non-Unix systems
        }

        // Arrange - Create key file with insecure permissions
        var service1 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        
        // Make file readable by everyone (insecure)
        File.SetUnixFileMode(_testKeyPath,
            System.IO.UnixFileMode.UserRead | 
            System.IO.UnixFileMode.UserWrite |
            System.IO.UnixFileMode.GroupRead |
            System.IO.UnixFileMode.OtherRead);

        // Act - Load keys (should fix permissions)
        var service2 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert - Permissions should be fixed
        var permissions = File.GetUnixFileMode(_testKeyPath);
        Assert.Equal(
            System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite,
            permissions);
    }

    [Fact]
    public void ParentDirectory_HasSecurePermissions_OnUnix()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return; // Skip on non-Unix systems
        }

        // Arrange & Act
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert
        var parentDir = Path.GetDirectoryName(_testKeyPath);
        var permissions = File.GetUnixFileMode(parentDir!);
        
        // Should have at least owner read/write/execute
        Assert.True(permissions.HasFlag(System.IO.UnixFileMode.UserRead));
        Assert.True(permissions.HasFlag(System.IO.UnixFileMode.UserWrite));
        Assert.True(permissions.HasFlag(System.IO.UnixFileMode.UserExecute));
    }

    [Fact]
    public void Constructor_RegeneratesKeys_ForInvalidKeyFileFormat()
    {
        // Arrange - Write invalid key file
        File.WriteAllText(_testKeyPath, "invalid-key-data");

        // Act - Should regenerate keys instead of throwing
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert - Should have valid keys now
        Assert.NotNull(service.PublicKey);
        Assert.Equal(32, service.PublicKey.Length);
    }

    [Fact]
    public void Constructor_RegeneratesKeys_ForInvalidKeyLength()
    {
        // Arrange - Write key file with wrong length
        var shortKey = Convert.ToBase64String(new byte[16]);
        File.WriteAllText(_testKeyPath, $"{shortKey}:{shortKey}");

        // Act - Should regenerate keys instead of throwing
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);

        // Assert - Should have valid keys now
        Assert.NotNull(service.PublicKey);
        Assert.Equal(32, service.PublicKey.Length);
    }

    [Fact]
    public void Sign_ProducesDifferentSignatures_ForDifferentData()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var data1 = System.Text.Encoding.UTF8.GetBytes("message 1");
        var data2 = System.Text.Encoding.UTF8.GetBytes("message 2");

        // Act
        var signature1 = service.Sign(data1);
        var signature2 = service.Sign(data2);

        // Assert
        Assert.NotEqual(signature1, signature2);
    }

    [Fact]
    public void Sign_ProducesSameSignature_ForSameData()
    {
        // Arrange
        var service = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        var data = System.Text.Encoding.UTF8.GetBytes("test message");

        // Act
        var signature1 = service.Sign(data);
        var signature2 = service.Sign(data);

        // Assert
        Assert.Equal(signature1, signature2);
    }

    [Fact]
    public void DifferentInstances_ProduceDifferentKeys()
    {
        // Arrange & Act
        var service1 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _testKeyPath);
        
        var otherKeyPath = Path.Combine(_testDirectory, "other-identity.key");
        var service2 = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            otherKeyPath);

        // Assert
        Assert.NotEqual(service1.PublicKey, service2.PublicKey);
        Assert.NotEqual(service1.MeshPeerId, service2.MeshPeerId);
    }
}

