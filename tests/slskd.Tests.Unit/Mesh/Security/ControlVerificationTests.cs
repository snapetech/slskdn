// <copyright file="ControlVerificationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.Mesh.Security;

using System;
using System.Collections.Generic;
using AutoFixture.Xunit2;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using NSec.Cryptography;
using slskd.Mesh.Overlay;
using slskd.Mesh.Security;
using Xunit;

public class ControlVerificationTests
{
    [Theory]
    [AutoData]
    public void Verify_WithValidSignature_ReturnsTrue(string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ControlVerification>>();
        var verification = new ControlVerification(logger.Object);

        // Generate Ed25519 keypair
        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var publicKey = key.Export(KeyBlobFormat.RawPublicKey);

        // Create and sign envelope
        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var signablePayload = $"{envelope.Type}|{envelope.TimestampUnixMs}|{envelope.MessageId}|{Convert.ToBase64String(envelope.Payload)}";
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, System.Text.Encoding.UTF8.GetBytes(signablePayload));
        envelope.Signature = Convert.ToBase64String(signatureBytes);

        var allowedKeys = new List<byte[]> { publicKey };

        // Act
        var result = verification.Verify(envelope, allowedKeys);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void Verify_WithWrongKey_ReturnsFalse(string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ControlVerification>>();
        var verification = new ControlVerification(logger.Object);

        // Generate two different keypairs
        using var signingKey = Key.Create(SignatureAlgorithm.Ed25519);
        using var otherKey = Key.Create(SignatureAlgorithm.Ed25519);
        var otherPublicKey = otherKey.Export(KeyBlobFormat.RawPublicKey);

        // Create and sign envelope with signingKey
        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var signablePayload = $"{envelope.Type}|{envelope.TimestampUnixMs}|{envelope.MessageId}|{Convert.ToBase64String(envelope.Payload)}";
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(signingKey, System.Text.Encoding.UTF8.GetBytes(signablePayload));
        envelope.Signature = Convert.ToBase64String(signatureBytes);

        // But provide otherPublicKey for verification
        var allowedKeys = new List<byte[]> { otherPublicKey };

        // Act
        var result = verification.Verify(envelope, allowedKeys);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void Verify_WithMultipleKeys_SucceedsWithAnyValidKey(string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ControlVerification>>();
        var verification = new ControlVerification(logger.Object);

        // Generate three keypairs (simulating key rotation)
        using var key1 = Key.Create(SignatureAlgorithm.Ed25519);
        using var key2 = Key.Create(SignatureAlgorithm.Ed25519);
        using var key3 = Key.Create(SignatureAlgorithm.Ed25519);

        var publicKey1 = key1.Export(KeyBlobFormat.RawPublicKey);
        var publicKey2 = key2.Export(KeyBlobFormat.RawPublicKey);
        var publicKey3 = key3.Export(KeyBlobFormat.RawPublicKey);

        // Sign with key2
        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var signablePayload = $"{envelope.Type}|{envelope.TimestampUnixMs}|{envelope.MessageId}|{Convert.ToBase64String(envelope.Payload)}";
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key2, System.Text.Encoding.UTF8.GetBytes(signablePayload));
        envelope.Signature = Convert.ToBase64String(signatureBytes);

        // Provide all three keys
        var allowedKeys = new List<byte[]> { publicKey1, publicKey2, publicKey3 };

        // Act
        var result = verification.Verify(envelope, allowedKeys);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [AutoData]
    public void Verify_WithNoAllowedKeys_ReturnsFalse(string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ControlVerification>>();
        var verification = new ControlVerification(logger.Object);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            Signature = Convert.ToBase64String(new byte[64]),
        };

        // Act
        var result = verification.Verify(envelope, new List<byte[]>());

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [AutoData]
    public void Verify_WithTamperedPayload_ReturnsFalse(string type, byte[] payload)
    {
        // Arrange
        var logger = new Mock<ILogger<ControlVerification>>();
        var verification = new ControlVerification(logger.Object);

        using var key = Key.Create(SignatureAlgorithm.Ed25519);
        var publicKey = key.Export(KeyBlobFormat.RawPublicKey);

        var envelope = new ControlEnvelope
        {
            Type = type,
            Payload = payload,
            MessageId = Guid.NewGuid().ToString("N"),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var signablePayload = $"{envelope.Type}|{envelope.TimestampUnixMs}|{envelope.MessageId}|{Convert.ToBase64String(envelope.Payload)}";
        var signatureBytes = SignatureAlgorithm.Ed25519.Sign(key, System.Text.Encoding.UTF8.GetBytes(signablePayload));
        envelope.Signature = Convert.ToBase64String(signatureBytes);

        // Tamper with payload after signing
        envelope.Payload = new byte[] { 0x00, 0x01, 0x02 };

        var allowedKeys = new List<byte[]> { publicKey };

        // Act
        var result = verification.Verify(envelope, allowedKeys);

        // Assert
        result.Should().BeFalse();
    }
}

