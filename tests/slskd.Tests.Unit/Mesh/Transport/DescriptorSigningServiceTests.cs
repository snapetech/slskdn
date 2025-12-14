// <copyright file="DescriptorSigningServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using slskd.Mesh.Dht;
using slskd.Mesh.Transport;

namespace slskd.Tests.Unit.Mesh.Transport;

public class DescriptorSigningServiceTests
{
    private readonly DescriptorSigningService _service;
    private readonly byte[] _privateKey;
    private readonly byte[] _publicKey;

    public DescriptorSigningServiceTests()
    {
        // Generate a test key pair (in reality this would be Ed25519)
        using var rsa = RSA.Create(2048);
        _privateKey = rsa.ExportRSAPrivateKey();
        _publicKey = rsa.ExportRSAPublicKey();

        // Note: Using RSA for testing since Ed25519 isn't implemented yet
        // In production, this would use proper Ed25519 keys
        _service = new DescriptorSigningService(null!); // Logger not needed for basic tests
    }

    [Fact]
    public void SignDescriptor_WithValidDescriptor_ReturnsSignature()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            PeerId = "test-peer-123",
            SequenceNumber = 1,
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = "192.168.1.1",
                    Port = 443,
                    Scope = TransportScope.ControlAndData
                }
            }
        };

        // Act
        var signature = _service.SignDescriptor(descriptor, _privateKey);

        // Assert
        Assert.NotNull(signature);
        Assert.NotEmpty(signature);
        // Note: Actual verification would require Ed25519 implementation
    }

    [Fact]
    public void ValidateSequenceNumber_FirstSequence_ReturnsTrue()
    {
        // Arrange
        var peerId = "test-peer";
        var sequence = 1;

        // Act
        var result = _service.ValidateSequenceNumber(peerId, sequence);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateSequenceNumber_IncreasingSequence_ReturnsTrue()
    {
        // Arrange
        var peerId = "test-peer";
        _service.AcceptSequenceNumber(peerId, 5);

        // Act
        var result = _service.ValidateSequenceNumber(peerId, 6);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateSequenceNumber_RollbackSequence_ReturnsFalse()
    {
        // Arrange
        var peerId = "test-peer";
        _service.AcceptSequenceNumber(peerId, 10);

        // Act
        var result = _service.ValidateSequenceNumber(peerId, 8);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateSequenceNumber_SameSequence_ReturnsFalse()
    {
        // Arrange
        var peerId = "test-peer";
        _service.AcceptSequenceNumber(peerId, 10);

        // Act
        var result = _service.ValidateSequenceNumber(peerId, 10);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsExpired_WithFutureExpiry_ReturnsFalse()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            ExpiresAtUnixMs = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeMilliseconds()
        };

        // Act
        var result = descriptor.IsExpired();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsExpired_WithPastExpiry_ReturnsTrue()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            ExpiresAtUnixMs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeMilliseconds()
        };

        // Act
        var result = descriptor.IsExpired();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetSignableData_IncludesAllFields()
    {
        // Arrange
        var descriptor = new MeshPeerDescriptor
        {
            PeerId = "peer-123",
            SequenceNumber = 42,
            ExpiresAtUnixMs = 1234567890,
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = "example.com",
                    Port = 443,
                    Scope = TransportScope.Control,
                    Preference = 1,
                    Cost = 5
                }
            },
            CertificatePins = new List<string> { "pin1", "pin2" },
            ControlSigningKeys = new List<string> { "key1" }
        };

        // Act
        var data = descriptor.GetSignableData();

        // Assert
        var dataString = System.Text.Encoding.UTF8.GetString(data);
        Assert.Contains("peer-123", dataString);
        Assert.Contains("42", dataString);
        Assert.Contains("1234567890", dataString);
        Assert.Contains("DirectQuic:example.com:443:Control:1:5", dataString);
        Assert.Contains("pin1", dataString);
        Assert.Contains("key1", dataString);
    }
}

