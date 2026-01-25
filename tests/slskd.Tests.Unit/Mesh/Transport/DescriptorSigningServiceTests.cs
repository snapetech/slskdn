// <copyright file="DescriptorSigningServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Dht;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Transport;

public class DescriptorSigningServiceTests
{
    private readonly DescriptorSigningService _service;
    private readonly byte[] _privateKey;

    public DescriptorSigningServiceTests()
    {
        // Ed25519 keys are 32 bytes; SignDescriptor requires exactly 32.
        _privateKey = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(_privateKey);

        _service = new DescriptorSigningService(Mock.Of<ILogger<DescriptorSigningService>>());
    }

    [Fact]
    public void SignDescriptor_WithValidDescriptor_ReturnsSignature()
    {
        // Arrange
        var descriptor = new slskd.Mesh.Dht.MeshPeerDescriptor
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
        var descriptor = new slskd.Mesh.Dht.MeshPeerDescriptor
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
        var descriptor = new slskd.Mesh.Dht.MeshPeerDescriptor
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
        var descriptor = new slskd.Mesh.Dht.MeshPeerDescriptor
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

        // Assert: GetSignableData returns canonical MessagePack; key string values appear as raw bytes
        Assert.NotEmpty(data);
        var dataString = System.Text.Encoding.UTF8.GetString(data);
        Assert.Contains("peer-123", dataString);
        Assert.Contains("example.com", dataString);
        Assert.Contains("pin1", dataString);
        Assert.Contains("key1", dataString);
    }
}


