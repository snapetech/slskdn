// <copyright file="Ed25519SignerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class Ed25519SignerTests : IDisposable
{
    private readonly Ed25519Signer _signer;

    public Ed25519SignerTests()
    {
        _signer = new Ed25519Signer();
    }

    public void Dispose()
    {
        _signer.Dispose();
    }

    [Fact]
    public void GenerateKeyPair_ReturnsValidKeys()
    {
        // Act
        var (privateKey, publicKey) = _signer.GenerateKeyPair();

        // Assert
        Assert.NotNull(privateKey);
        Assert.NotEmpty(privateKey);
        Assert.NotNull(publicKey);
        Assert.NotEmpty(publicKey);

        // ECDSA keys should be around 256-512 bytes
        Assert.True(privateKey.Length >= 256);
        Assert.True(publicKey.Length >= 128);
    }

    [Fact]
    public void Sign_ReturnsValidSignature()
    {
        // Arrange
        var (privateKey, publicKey) = _signer.GenerateKeyPair();
        var data = System.Text.Encoding.UTF8.GetBytes("test message");

        // Act
        var signature = _signer.Sign(data, privateKey);

        // Assert
        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
        // ECDSA signatures are typically 64 bytes
        Assert.True(signature.Length >= 32);
    }

    [Fact]
    public void Verify_ValidSignature_ReturnsTrue()
    {
        // Arrange
        var (privateKey, publicKey) = _signer.GenerateKeyPair();
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = _signer.Sign(data, privateKey);

        // Act
        var isValid = _signer.Verify(data, signature, publicKey);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_TamperedData_ReturnsFalse()
    {
        // Arrange
        var (privateKey, publicKey) = _signer.GenerateKeyPair();
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var tamperedData = System.Text.Encoding.UTF8.GetBytes("tampered message");
        var signature = _signer.Sign(data, privateKey);

        // Act
        var isValid = _signer.Verify(tamperedData, signature, publicKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void Verify_WrongKey_ReturnsFalse()
    {
        // Arrange
        var (privateKey1, publicKey1) = _signer.GenerateKeyPair();
        var (privateKey2, publicKey2) = _signer.GenerateKeyPair();
        var data = System.Text.Encoding.UTF8.GetBytes("test message");
        var signature = _signer.Sign(data, privateKey1);

        // Act - Verify with wrong public key
        var isValid = _signer.Verify(data, signature, publicKey2);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void DerivePeerId_ConsistentForSameKey()
    {
        // Arrange
        var (privateKey, publicKey) = _signer.GenerateKeyPair();

        // Act
        var peerId1 = Ed25519Signer.DerivePeerId(publicKey);
        var peerId2 = Ed25519Signer.DerivePeerId(publicKey);

        // Assert
        Assert.Equal(peerId1, peerId2);
        Assert.NotNull(peerId1);
        Assert.NotEmpty(peerId1);
        // Base32 encoding of 20 bytes should be 32 characters
        Assert.Equal(32, peerId1.Length);
    }

    [Fact]
    public void DerivePeerId_DifferentForDifferentKeys()
    {
        // Arrange
        var (_, publicKey1) = _signer.GenerateKeyPair();
        var (_, publicKey2) = _signer.GenerateKeyPair();

        // Act
        var peerId1 = Ed25519Signer.DerivePeerId(publicKey1);
        var peerId2 = Ed25519Signer.DerivePeerId(publicKey2);

        // Assert
        Assert.NotEqual(peerId1, peerId2);
    }

    [Fact]
    public void DerivePeerId_ValidBase32Format()
    {
        // Arrange
        var (_, publicKey) = _signer.GenerateKeyPair();

        // Act
        var peerId = Ed25519Signer.DerivePeerId(publicKey);

        // Assert - Should only contain valid base32 characters (a-z, 2-7)
        foreach (var c in peerId)
        {
            Assert.True((c >= 'a' && c <= 'z') || (c >= '2' && c <= '7'),
                $"Invalid character '{c}' in peer ID '{peerId}'");
        }
    }

    [Fact]
    public void Sign_InvalidPrivateKey_ThrowsException()
    {
        // Arrange
        var invalidKey = new byte[10]; // Too short
        var data = System.Text.Encoding.UTF8.GetBytes("test");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _signer.Sign(data, invalidKey));
    }

    [Fact]
    public void Verify_InvalidPublicKey_DoesNotThrow_ReturnsFalse()
    {
        // Arrange
        var data = System.Text.Encoding.UTF8.GetBytes("test");
        var signature = new byte[64];
        var invalidKey = new byte[10]; // Too short

        // Act
        var result = _signer.Verify(data, signature, invalidKey);

        // Assert - Should return false rather than throw
        Assert.False(result);
    }
}
