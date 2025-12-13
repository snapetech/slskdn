// <copyright file="HandshakeIntegrationTests.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous.Integration;

using System;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Security;
using slskd.Mesh.Identity;
using Xunit;

/// <summary>
/// Integration tests for the complete handshake flow including
/// signature generation, validation, and replay protection.
/// </summary>
public class HandshakeIntegrationTests : IDisposable
{
    private readonly string _clientKeyPath;
    private readonly string _serverKeyPath;
    private readonly LocalMeshIdentityService _clientIdentity;
    private readonly LocalMeshIdentityService _serverIdentity;
    private readonly ReplayCache _replayCache;

    public HandshakeIntegrationTests()
    {
        var testDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"slskdn-test-{Guid.NewGuid()}");
        System.IO.Directory.CreateDirectory(testDir);
        
        _clientKeyPath = System.IO.Path.Combine(testDir, "client-identity.key");
        _serverKeyPath = System.IO.Path.Combine(testDir, "server-identity.key");
        
        _clientIdentity = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _clientKeyPath);
        
        _serverIdentity = new LocalMeshIdentityService(
            NullLogger<LocalMeshIdentityService>.Instance,
            _serverKeyPath);
        
        _replayCache = new ReplayCache(TimeSpan.FromMinutes(5));
    }

    public void Dispose()
    {
        _replayCache.Dispose();
        
        try
        {
            System.IO.File.Delete(_clientKeyPath);
            System.IO.File.Delete(_serverKeyPath);
            var dir = System.IO.Path.GetDirectoryName(_clientKeyPath);
            if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.Delete(dir, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void FullHandshakeFlow_ValidSignature_Succeeds()
    {
        // Arrange - Client creates handshake message
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var signature = _clientIdentity.Sign(payload);

        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server validates message
        var validationResult = MessageValidator.ValidateMeshHello(helloMessage);

        // Reconstruct payload on server side
        var reconstructedPayload = BuildHandshakePayload(
            helloMessage.MeshPeerId,
            helloMessage.Timestamp);

        var signatureBytes = Convert.FromBase64String(helloMessage.Signature);
        var publicKeyBytes = Convert.FromBase64String(helloMessage.PublicKey);

        var signatureValid = LocalMeshIdentityService.Verify(
            reconstructedPayload,
            signatureBytes,
            publicKeyBytes);

        var isReplay = _replayCache.IsReplay(
            helloMessage.MeshPeerId,
            helloMessage.Signature);

        // Assert
        validationResult.IsValid.Should().BeTrue();
        signatureValid.Should().BeTrue("signature should be cryptographically valid");
        isReplay.Should().BeFalse("first use should not be a replay");
    }

    [Fact]
    public void FullHandshakeFlow_InvalidSignature_Fails()
    {
        // Arrange - Client creates handshake message
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var signature = _clientIdentity.Sign(payload);

        // Tamper with signature
        signature[0] ^= 0xFF;

        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server validates message
        var reconstructedPayload = BuildHandshakePayload(
            helloMessage.MeshPeerId,
            helloMessage.Timestamp);

        var signatureBytes = Convert.FromBase64String(helloMessage.Signature);
        var publicKeyBytes = Convert.FromBase64String(helloMessage.PublicKey);

        var signatureValid = LocalMeshIdentityService.Verify(
            reconstructedPayload,
            signatureBytes,
            publicKeyBytes);

        // Assert
        signatureValid.Should().BeFalse("tampered signature should fail verification");
    }

    [Fact]
    public void FullHandshakeFlow_ReplayedSignature_DetectedByCache()
    {
        // Arrange - Client creates handshake message
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var signature = _clientIdentity.Sign(payload);

        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - First handshake
        var firstReplay = _replayCache.IsReplay(
            helloMessage.MeshPeerId,
            helloMessage.Signature);

        // Second handshake with same signature (replay attack)
        var secondReplay = _replayCache.IsReplay(
            helloMessage.MeshPeerId,
            helloMessage.Signature);

        // Assert
        firstReplay.Should().BeFalse("first attempt should succeed");
        secondReplay.Should().BeTrue("replayed signature should be detected");
    }

    [Fact]
    public void FullHandshakeFlow_ModifiedPeerId_FailsVerification()
    {
        // Arrange - Client creates handshake message
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var signature = _clientIdentity.Sign(payload);

        // Create message with MODIFIED PeerId (identity spoofing attempt)
        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = "spoofed-peer-id", // Different from what was signed
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server validates message
        var reconstructedPayload = BuildHandshakePayload(
            helloMessage.MeshPeerId, // Using spoofed PeerId
            helloMessage.Timestamp);

        var signatureBytes = Convert.FromBase64String(helloMessage.Signature);
        var publicKeyBytes = Convert.FromBase64String(helloMessage.PublicKey);

        var signatureValid = LocalMeshIdentityService.Verify(
            reconstructedPayload,
            signatureBytes,
            publicKeyBytes);

        // Assert
        signatureValid.Should().BeFalse("modified PeerId should fail signature verification");
    }

    [Fact]
    public void FullHandshakeFlow_ModifiedTimestamp_FailsVerification()
    {
        // Arrange - Client creates handshake message
        var originalTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, originalTimestamp);
        var signature = _clientIdentity.Sign(payload);

        // Create message with MODIFIED timestamp
        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = originalTimestamp + 1000, // Modified
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server validates message
        var reconstructedPayload = BuildHandshakePayload(
            helloMessage.MeshPeerId,
            helloMessage.Timestamp); // Using modified timestamp

        var signatureBytes = Convert.FromBase64String(helloMessage.Signature);
        var publicKeyBytes = Convert.FromBase64String(helloMessage.PublicKey);

        var signatureValid = LocalMeshIdentityService.Verify(
            reconstructedPayload,
            signatureBytes,
            publicKeyBytes);

        // Assert
        signatureValid.Should().BeFalse("modified timestamp should fail signature verification");
    }

    [Fact]
    public void FullHandshakeFlow_StaleTimestamp_FailsValidation()
    {
        // Arrange - Client creates handshake message with old timestamp
        var staleTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, staleTimestamp);
        var signature = _clientIdentity.Sign(payload);

        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = staleTimestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server validates message
        var validationResult = MessageValidator.ValidateMeshHello(helloMessage);

        // Assert
        validationResult.IsValid.Should().BeFalse("stale timestamp should fail validation");
        validationResult.Error.Should().Contain("too old or in future");
    }

    [Fact]
    public void FullHandshakeFlow_FutureTimestamp_FailsValidation()
    {
        // Arrange - Client creates handshake message with future timestamp
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, futureTimestamp);
        var signature = _clientIdentity.Sign(payload);

        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = futureTimestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server validates message
        var validationResult = MessageValidator.ValidateMeshHello(helloMessage);

        // Assert
        validationResult.IsValid.Should().BeFalse("future timestamp should fail validation");
        validationResult.Error.Should().Contain("too old or in future");
    }

    [Fact]
    public void FullHandshakeFlow_DifferentPeers_IsolatedReplayDetection()
    {
        // Arrange - Two clients create handshake messages
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        var clientPayload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var clientSignature = _clientIdentity.Sign(clientPayload);

        var serverPayload = BuildHandshakePayload(_serverIdentity.MeshPeerId.Value, timestamp);
        var serverSignature = _serverIdentity.Sign(serverPayload);

        // Act - Mark both as seen
        var client1stReplay = _replayCache.IsReplay(
            _clientIdentity.MeshPeerId.Value,
            Convert.ToBase64String(clientSignature));

        var server1stReplay = _replayCache.IsReplay(
            _serverIdentity.MeshPeerId.Value,
            Convert.ToBase64String(serverSignature));

        // Try replays
        var client2ndReplay = _replayCache.IsReplay(
            _clientIdentity.MeshPeerId.Value,
            Convert.ToBase64String(clientSignature));

        var server2ndReplay = _replayCache.IsReplay(
            _serverIdentity.MeshPeerId.Value,
            Convert.ToBase64String(serverSignature));

        // Assert
        client1stReplay.Should().BeFalse("client first attempt should succeed");
        server1stReplay.Should().BeFalse("server first attempt should succeed");
        client2ndReplay.Should().BeTrue("client replay should be detected");
        server2ndReplay.Should().BeTrue("server replay should be detected");
    }

    [Fact]
    public void FullHandshakeFlow_PeerIdMatchesPublicKey_Succeeds()
    {
        // Arrange - Client creates handshake message
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var signature = _clientIdentity.Sign(payload);

        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_clientIdentity.PublicKey),
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server verifies PeerId matches public key
        var publicKeyBytes = Convert.FromBase64String(helloMessage.PublicKey);
        var derivedPeerId = MeshPeerId.FromPublicKey(publicKeyBytes);

        // Assert
        derivedPeerId.Value.Should().Be(helloMessage.MeshPeerId,
            "PeerId should be derived from the public key");
    }

    [Fact]
    public void FullHandshakeFlow_PeerIdMismatchesPublicKey_Detected()
    {
        // Arrange - Create handshake with mismatched PeerId and public key
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        
        // Sign with client identity
        var payload = BuildHandshakePayload(_clientIdentity.MeshPeerId.Value, timestamp);
        var signature = _clientIdentity.Sign(payload);

        // But claim server's public key (identity spoofing attempt)
        var helloMessage = new MeshHelloMessage
        {
            MeshPeerId = _clientIdentity.MeshPeerId.Value,
            Timestamp = timestamp,
            Username = "testuser",
            PublicKey = Convert.ToBase64String(_serverIdentity.PublicKey), // Wrong key!
            Signature = Convert.ToBase64String(signature),
        };

        // Act - Server verifies PeerId matches public key
        var publicKeyBytes = Convert.FromBase64String(helloMessage.PublicKey);
        var derivedPeerId = MeshPeerId.FromPublicKey(publicKeyBytes);

        // Assert
        derivedPeerId.Value.Should().NotBe(helloMessage.MeshPeerId,
            "PeerId should not match when public key is spoofed");
    }

    // Helper method matching MeshOverlayConnection.BuildHandshakePayload
    private static byte[] BuildHandshakePayload(string meshPeerId, long timestamp)
    {
        var payload = $"{meshPeerId}|MeshConnectable|{timestamp}";
        return Encoding.UTF8.GetBytes(payload);
    }
}

