// <copyright file="CanonicalSerializationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;
using slskd.Mesh.Transport;
using Xunit;
using TransportEndpoint = slskd.Mesh.TransportEndpoint;
using TransportType = slskd.Mesh.TransportType;
using TransportScope = slskd.Mesh.TransportScope;

namespace slskd.Tests.Unit.Mesh.Transport;

public class CanonicalSerializationTests
{
    [Fact]
    public void SerializeForSigning_DeterministicOutput()
    {
        // Arrange
        var descriptor1 = CreateTestDescriptor();
        var descriptor2 = CreateTestDescriptor();

        // Act
        var bytes1 = CanonicalSerialization.SerializeForSigning(descriptor1);
        var bytes2 = CanonicalSerialization.SerializeForSigning(descriptor2);

        // Assert
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void SerializeForSigning_FieldOrderMatters()
    {
        // Arrange
        var desc1 = new MeshPeerDescriptor
        {
            PeerId = "peer1",
            SequenceNumber = 1,
            ExpiresAtUnixMs = 1234567890,
            Endpoints = new List<string> { "endpoint1", "endpoint2" }
        };

        var desc2 = new MeshPeerDescriptor
        {
            PeerId = "peer1",
            SequenceNumber = 1,
            ExpiresAtUnixMs = 1234567890,
            Endpoints = new List<string> { "endpoint2", "endpoint3" } // Different set → different canonical output
        };

        // Act
        var bytes1 = CanonicalSerialization.SerializeForSigning(desc1);
        var bytes2 = CanonicalSerialization.SerializeForSigning(desc2);

        // Assert - Should be different due to different endpoint sets
        Assert.NotEqual(bytes1, bytes2);
    }

    [Fact]
    public void SerializeEnvelopeForSigning_IncludesAllRequiredFields()
    {
        // Arrange
        var envelope = new ControlEnvelope
        {
            Type = "test-type",
            MessageId = "test-message-id",
            TimestampUnixMs = 1234567890,
            Payload = new byte[] { 1, 2, 3, 4, 5 }
        };

        // Act
        var data = CanonicalSerialization.SerializeEnvelopeForSigning(envelope);
        var dataString = System.Text.Encoding.UTF8.GetString(data);

        // Assert - format is Type|MessageId|Timestamp|Base64(SHA256(Payload))
        Assert.Contains("test-type", dataString);
        Assert.Contains("test-message-id", dataString);
        Assert.Contains("1234567890", dataString);
        var parts = dataString.Split('|');
        Assert.Equal(4, parts.Length);
        Assert.NotEmpty(parts[3]); // Payload hash (base64)
    }

    [Fact]
    public void AreEquivalent_IdenticalDescriptors_ReturnsTrue()
    {
        // Arrange
        var desc1 = CreateTestDescriptor();
        var desc2 = CreateTestDescriptor();

        // Act
        var result = CanonicalSerialization.AreEquivalent(desc1, desc2);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void AreEquivalent_DifferentDescriptors_ReturnsFalse()
    {
        // Arrange
        var desc1 = CreateTestDescriptor();
        var desc2 = CreateTestDescriptor();
        desc2.SequenceNumber = 999; // Different sequence

        // Act
        var result = CanonicalSerialization.AreEquivalent(desc1, desc2);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void SerializeEnvelopeForSigning_IncludesPayloadHash()
    {
        // Arrange
        var envelope1 = new ControlEnvelope
        {
            Type = "test",
            MessageId = "msg1",
            TimestampUnixMs = 1000,
            Payload = new byte[] { 1, 2, 3 }
        };

        var envelope2 = new ControlEnvelope
        {
            Type = "test",
            MessageId = "msg1",
            TimestampUnixMs = 1000,
            Payload = new byte[] { 4, 5, 6 } // Different payload
        };

        // Act
        var data1 = CanonicalSerialization.SerializeEnvelopeForSigning(envelope1);
        var data2 = CanonicalSerialization.SerializeEnvelopeForSigning(envelope2);

        // Assert - Should be different due to payload hash
        Assert.NotEqual(data1, data2);
    }

    private static MeshPeerDescriptor CreateTestDescriptor()
    {
        return new MeshPeerDescriptor
        {
            PeerId = "peer:test:canonical",
            SequenceNumber = 42,
            ExpiresAtUnixMs = 1640995200000, // 2022-01-01
            Endpoints = new List<string> { "tcp://192.168.1.100:8080", "tcp://10.0.0.1:8080" },
            TransportEndpoints = new List<TransportEndpoint>
            {
                new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = "192.168.1.100",
                    Port = 8080,
                    Scope = TransportScope.Control,
                    Preference = 1,
                    Cost = 1
                }
            },
            CertificatePins = new List<string> { "pin1", "pin2" },
            ControlSigningKeys = new List<string> { "key1", "key2" },
            NatType = "unknown",
            RelayRequired = false,
            TimestampUnixMs = 1640995200000
        };
    }
}


