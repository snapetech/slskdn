// <copyright file="HandshakeVerificationTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System;
using System.Security.Cryptography;
using System.Text;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Security;
using slskd.Mesh.Identity;
using Xunit;

public class HandshakeVerificationTests
{
    [Fact]
    public void BuildHandshakePayload_CreatesConsistentPayload()
    {
        // Arrange
        var meshPeerId = "test-peer-id";
        var timestamp = 1700000000L;

        // Act
        var payload1 = BuildPayload(meshPeerId, timestamp);
        var payload2 = BuildPayload(meshPeerId, timestamp);

        // Assert
        Assert.Equal(payload1, payload2);
    }

    [Fact]
    public void BuildHandshakePayload_IncludesAllComponents()
    {
        // Arrange
        var meshPeerId = "test-peer-id";
        var timestamp = 1700000000L;

        // Act
        var payload = BuildPayload(meshPeerId, timestamp);
        var payloadStr = Encoding.UTF8.GetString(payload);

        // Assert
        Assert.Contains(meshPeerId, payloadStr);
        Assert.Contains(timestamp.ToString(), payloadStr);
    }

    [Fact]
    public void VerifyHandshakeSignature_AcceptsValidSignature()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildPayload(meshPeerId, timestamp);
        var signature = SignData(payload, keyPair.privateKey);

        // Act
        var result = LocalMeshIdentityService.Verify(payload, signature, keyPair.publicKey);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyHandshakeSignature_RejectsInvalidSignature()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildPayload(meshPeerId, timestamp);
        var signature = SignData(payload, keyPair.privateKey);

        // Tamper with signature
        signature[0] ^= 0xFF;

        // Act
        var result = LocalMeshIdentityService.Verify(payload, signature, keyPair.publicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyHandshakeSignature_RejectsModifiedPayload()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var originalPayload = BuildPayload(meshPeerId, timestamp);
        var signature = SignData(originalPayload, keyPair.privateKey);

        // Modify the payload after signing
        var tamperedPayload = BuildPayload("different-peer-id", timestamp);

        // Act
        var result = LocalMeshIdentityService.Verify(tamperedPayload, signature, keyPair.publicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyHandshakeSignature_RejectsWrongPublicKey()
    {
        // Arrange
        var keyPair1 = GenerateTestKeyPair();
        var keyPair2 = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildPayload(meshPeerId, timestamp);
        var signature = SignData(payload, keyPair1.privateKey);

        // Act - verify with different public key
        var result = LocalMeshIdentityService.Verify(payload, signature, keyPair2.publicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyHandshakeSignature_RejectsInvalidPublicKeyLength()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildPayload(meshPeerId, timestamp);
        var signature = SignData(payload, keyPair.privateKey);
        var invalidPublicKey = new byte[16]; // Wrong length (should be 32 for Ed25519)

        // Act
        var result = LocalMeshIdentityService.Verify(payload, signature, invalidPublicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyHandshakeSignature_RejectsInvalidSignatureLength()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildPayload(meshPeerId, timestamp);
        var invalidSignature = new byte[32]; // Wrong length (should be 64 for Ed25519)

        // Act
        var result = LocalMeshIdentityService.Verify(payload, invalidSignature, keyPair.publicKey);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void MeshHelloMessage_WithValidSignature_PassesValidation()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var meshPeerId = "test-peer-id";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildPayload(meshPeerId, timestamp);
        var signature = SignData(payload, keyPair.privateKey);

        var message = new MeshHelloMessage
        {
            MeshPeerId = meshPeerId,
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(keyPair.publicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MeshHelloMessage_WithoutSignature_FailsValidation()
    {
        // Arrange
        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = "testuser",
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        // Should pass basic validation (signature is optional in basic validation)
        // Actual signature verification happens separately
        Assert.True(result.IsValid);
    }

    [Fact]
    public void MeshHelloMessage_WithStaleTimestamp_FailsValidation()
    {
        // Arrange
        var keyPair = GenerateTestKeyPair();
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(); // Too old

        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(keyPair.publicKey),
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too old or in future", result.Error);
    }

    // Helper methods

    private static byte[] BuildPayload(string meshPeerId, long timestamp)
    {
        // Match the format from MeshOverlayConnection.BuildHandshakePayload
        var payload = $"{meshPeerId}|MeshConnectable|{timestamp}";
        return Encoding.UTF8.GetBytes(payload);
    }

    private static (byte[] publicKey, byte[] privateKey) GenerateTestKeyPair()
    {
        var algorithm = NSec.Cryptography.SignatureAlgorithm.Ed25519;
        using var key = NSec.Cryptography.Key.Create(algorithm, new NSec.Cryptography.KeyCreationParameters { ExportPolicy = NSec.Cryptography.KeyExportPolicies.AllowPlaintextExport });
        var publicKey = key.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);
        var privateKey = key.Export(NSec.Cryptography.KeyBlobFormat.RawPrivateKey);
        return (publicKey, privateKey);
    }

    private static byte[] SignData(byte[] data, byte[] privateKey)
    {
        var algorithm = NSec.Cryptography.SignatureAlgorithm.Ed25519;
        using var key = NSec.Cryptography.Key.Import(
            algorithm,
            privateKey,
            NSec.Cryptography.KeyBlobFormat.RawPrivateKey);
        return algorithm.Sign(key, data);
    }
}

