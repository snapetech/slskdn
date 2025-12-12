using System;
using System.Collections.Generic;
using slskd.Mesh.ServiceFabric;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// Unit tests for MeshServiceDescriptor ServiceId derivation.
/// </summary>
public class MeshServiceDescriptorTests
{
    [Fact]
    public void DeriveServiceId_WithSameInputs_ReturnsSameId()
    {
        // Arrange
        var serviceName = "test-service";
        var ownerPeerId = "peer123";

        // Act
        var id1 = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId);
        var id2 = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId);

        // Assert
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void DeriveServiceId_WithDifferentServiceName_ReturnsDifferentId()
    {
        // Arrange
        var serviceName1 = "test-service-1";
        var serviceName2 = "test-service-2";
        var ownerPeerId = "peer123";

        // Act
        var id1 = MeshServiceDescriptor.DeriveServiceId(serviceName1, ownerPeerId);
        var id2 = MeshServiceDescriptor.DeriveServiceId(serviceName2, ownerPeerId);

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void DeriveServiceId_WithDifferentOwner_ReturnsDifferentId()
    {
        // Arrange
        var serviceName = "test-service";
        var ownerPeerId1 = "peer123";
        var ownerPeerId2 = "peer456";

        // Act
        var id1 = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId1);
        var id2 = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId2);

        // Assert
        Assert.NotEqual(id1, id2);
    }

    [Theory]
    [InlineData("", "peer123")]
    [InlineData(null, "peer123")]
    [InlineData("  ", "peer123")]
    [InlineData("service", "")]
    [InlineData("service", null)]
    [InlineData("service", "  ")]
    public void DeriveServiceId_WithInvalidInputs_ThrowsArgumentException(
        string serviceName,
        string ownerPeerId)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId));
    }

    [Fact]
    public void DeriveServiceId_ReturnsLowercaseHex()
    {
        // Arrange
        var serviceName = "test-service";
        var ownerPeerId = "peer123";

        // Act
        var id = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId);

        // Assert
        Assert.Matches("^[0-9a-f]+$", id); // Lowercase hex
        Assert.Equal(64, id.Length); // SHA256 produces 64 hex chars
    }

    [Fact]
    public void DeriveServiceId_IsCollisionResistant()
    {
        // Arrange
        var serviceNames = new[] { "pods", "shadow-index", "mesh-stats", "echo-service" };
        var peerIds = new[] { "peer1", "peer2", "peer3", "peer4" };
        var ids = new HashSet<string>();

        // Act
        foreach (var serviceName in serviceNames)
        {
            foreach (var peerId in peerIds)
            {
                var id = MeshServiceDescriptor.DeriveServiceId(serviceName, peerId);
                ids.Add(id);
            }
        }

        // Assert - All IDs should be unique
        Assert.Equal(serviceNames.Length * peerIds.Length, ids.Count);
    }

    [Fact]
    public void GetBytesForSigning_ProducesDeterministicOutput()
    {
        // Arrange
        var descriptor = new MeshServiceDescriptor
        {
            ServiceId = "test-id",
            ServiceName = "test-service",
            Version = "1.0.0",
            OwnerPeerId = "peer123",
            Endpoint = new MeshServiceEndpoint
            {
                Protocol = "quic",
                Host = "peer123",
                Port = 5000
            },
            Metadata = new Dictionary<string, string>
            {
                { "type", "test" },
                { "version", "1.0" }
            },
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1000000),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(2000000),
            Signature = Array.Empty<byte>()
        };

        // Act
        var bytes1 = descriptor.GetBytesForSigning();
        var bytes2 = descriptor.GetBytesForSigning();

        // Assert
        Assert.Equal(bytes1, bytes2);
    }

    [Fact]
    public void GetBytesForSigning_MetadataOrderDoesNotMatter()
    {
        // Arrange
        var descriptor1 = new MeshServiceDescriptor
        {
            ServiceId = "test-id",
            ServiceName = "test-service",
            Version = "1.0.0",
            OwnerPeerId = "peer123",
            Endpoint = new MeshServiceEndpoint { Protocol = "quic", Host = "peer123" },
            Metadata = new Dictionary<string, string>
            {
                { "a", "1" },
                { "b", "2" },
                { "c", "3" }
            },
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1000000),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(2000000)
        };

        var descriptor2 = new MeshServiceDescriptor
        {
            ServiceId = "test-id",
            ServiceName = "test-service",
            Version = "1.0.0",
            OwnerPeerId = "peer123",
            Endpoint = new MeshServiceEndpoint { Protocol = "quic", Host = "peer123" },
            Metadata = new Dictionary<string, string>
            {
                { "c", "3" },
                { "a", "1" },
                { "b", "2" }
            },
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(1000000),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(2000000)
        };

        // Act
        var bytes1 = descriptor1.GetBytesForSigning();
        var bytes2 = descriptor2.GetBytesForSigning();

        // Assert - Should be equal because metadata is sorted
        Assert.Equal(bytes1, bytes2);
    }
}
