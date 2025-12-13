// <copyright file="MessageValidatorTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Security;

using System;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Security;
using Xunit;

public class MessageValidatorTests
{
    [Fact]
    public void ValidateTimestamp_AcceptsCurrentTimestamp()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(now);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTimestamp_AcceptsTimestampWithin5Minutes()
    {
        // Arrange - 4 minutes ago
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-4).ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(timestamp);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTimestamp_AcceptsFutureTimestampWithin5Minutes()
    {
        // Arrange - 4 minutes in future
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(4).ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(timestamp);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTimestamp_RejectsStaleTimestamp()
    {
        // Arrange - 10 minutes ago (beyond max skew)
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(timestamp);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too old or in future", result.Error);
    }

    [Fact]
    public void ValidateTimestamp_RejectsFutureTimestamp()
    {
        // Arrange - 10 minutes in future (beyond max skew)
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(timestamp);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too old or in future", result.Error);
    }

    [Fact]
    public void ValidateTimestamp_EdgeCase_ExactlyAtMaxSkew()
    {
        // Arrange - Just under 5 minutes (should be accepted)
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-4).AddSeconds(-59).ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(timestamp);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateTimestamp_EdgeCase_JustBeyondMaxSkew()
    {
        // Arrange - Just beyond 5 minutes (should be rejected)
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5).AddSeconds(-2).ToUnixTimeSeconds();

        // Act
        var result = MessageValidator.ValidateTimestamp(timestamp);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateMeshHello_RejectsMessageWithStaleTimestamp()
    {
        // Arrange
        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(), // Stale
            Username = "testuser",
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too old or in future", result.Error);
    }

    [Fact]
    public void ValidateMeshHello_AcceptsMessageWithCurrentTimestamp()
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
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateMeshHello_RequiresMeshPeerId()
    {
        // Arrange
        var message = new MeshHelloMessage
        {
            MeshPeerId = "", // Missing
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = "testuser",
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("MeshPeerId is required", result.Error);
    }

    [Fact]
    public void ValidateMeshHello_ValidatesPublicKeyFormat()
    {
        // Arrange - Invalid base64
        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = "testuser",
            PublicKey = "not-valid-base64!!!",
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not valid base64", result.Error);
    }

    [Fact]
    public void ValidateMeshHello_ValidatesPublicKeyLength()
    {
        // Arrange - Valid base64 but wrong length (Ed25519 needs 32 bytes)
        var shortKey = Convert.ToBase64String(new byte[16]); // Only 16 bytes
        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = "testuser",
            PublicKey = shortKey,
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("wrong length", result.Error);
    }

    [Fact]
    public void ValidateMeshHello_ValidatesSignatureLength()
    {
        // Arrange - Valid base64 but wrong length (Ed25519 signatures need 64 bytes)
        var shortSig = Convert.ToBase64String(new byte[32]); // Only 32 bytes
        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = "testuser",
            Signature = shortSig,
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("wrong length", result.Error);
    }

    [Fact]
    public void ValidateMeshHello_AcceptsValidPublicKeyAndSignature()
    {
        // Arrange
        var validPublicKey = Convert.ToBase64String(new byte[32]); // Correct length
        var validSignature = Convert.ToBase64String(new byte[64]); // Correct length
        var message = new MeshHelloMessage
        {
            MeshPeerId = "test-peer-id",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Username = "testuser",
            PublicKey = validPublicKey,
            Signature = validSignature,
        };

        // Act
        var result = MessageValidator.ValidateMeshHello(message);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateUsername_RejectsEmptyUsername()
    {
        // Act
        var result = MessageValidator.ValidateUsername("");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Username is empty", result.Error);
    }

    [Fact]
    public void ValidateUsername_RejectsTooLongUsername()
    {
        // Arrange - 65 characters (max is 64)
        var longUsername = new string('a', 65);

        // Act
        var result = MessageValidator.ValidateUsername(longUsername);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("too long", result.Error);
    }

    [Fact]
    public void ValidateUsername_RejectsInvalidCharacters()
    {
        // Act
        var result = MessageValidator.ValidateUsername("user@name"); // @ is invalid

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("invalid characters", result.Error);
    }

    [Fact]
    public void ValidateUsername_AcceptsValidUsername()
    {
        // Act
        var result = MessageValidator.ValidateUsername("valid_user.name-123");

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ConstantTimeEquals_ReturnsTrueForEqualStrings()
    {
        // Act
        var result = MessageValidator.ConstantTimeEquals("test", "test");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ConstantTimeEquals_ReturnsFalseForDifferentStrings()
    {
        // Act
        var result = MessageValidator.ConstantTimeEquals("test1", "test2");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ConstantTimeEquals_HandlesBothNull()
    {
        // Act
        var result = MessageValidator.ConstantTimeEquals(null, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ConstantTimeEquals_HandlesSingleNull()
    {
        // Act
        var result1 = MessageValidator.ConstantTimeEquals("test", null);
        var result2 = MessageValidator.ConstantTimeEquals(null, "test");

        // Assert
        Assert.False(result1);
        Assert.False(result2);
    }
}

