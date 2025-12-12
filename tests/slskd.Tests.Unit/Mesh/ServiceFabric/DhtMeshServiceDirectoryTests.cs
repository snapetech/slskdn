using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh.Dht;
using slskd.Mesh.ServiceFabric;
using Xunit;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// Unit tests for DhtMeshServiceDirectory.
/// </summary>
public class DhtMeshServiceDirectoryTests
{
    private readonly Mock<ILogger<DhtMeshServiceDirectory>> _loggerMock;
    private readonly Mock<IMeshDhtClient> _dhtClientMock;
    private readonly Mock<IMeshServiceDescriptorValidator> _validatorMock;
    private readonly MeshServiceFabricOptions _options;
    private readonly DhtMeshServiceDirectory _directory;

    public DhtMeshServiceDirectoryTests()
    {
        _loggerMock = new Mock<ILogger<DhtMeshServiceDirectory>>();
        _dhtClientMock = new Mock<IMeshDhtClient>();
        _validatorMock = new Mock<IMeshServiceDescriptorValidator>();
        _options = new MeshServiceFabricOptions();
        
        var optionsMock = new Mock<IOptions<MeshServiceFabricOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        
        _directory = new DhtMeshServiceDirectory(
            _loggerMock.Object,
            _dhtClientMock.Object,
            _validatorMock.Object,
            optionsMock.Object);
    }

    [Fact]
    public async Task FindByNameAsync_WithEmptyServiceName_ReturnsEmpty()
    {
        // Act
        var result = await _directory.FindByNameAsync("", CancellationToken.None);

        // Assert
        Assert.Empty(result);
        _dhtClientMock.Verify(
            d => d.GetRawAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FindByNameAsync_WhenDhtReturnsNull_ReturnsEmpty()
    {
        // Arrange
        _dhtClientMock
            .Setup(d => d.GetRawAsync("svc:test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _directory.FindByNameAsync("test-service", CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindByNameAsync_WithOversizedDhtValue_ReturnsEmpty()
    {
        // Arrange
        var hugeValue = new byte[_options.MaxDhtValueBytes + 1];
        _dhtClientMock
            .Setup(d => d.GetRawAsync("svc:test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(hugeValue);

        // Act
        var result = await _directory.FindByNameAsync("test-service", CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task FindByNameAsync_WithValidDescriptors_ReturnsValidatedList()
    {
        // Arrange
        var descriptors = new List<MeshServiceDescriptor>
        {
            CreateTestDescriptor("test-service", "peer1"),
            CreateTestDescriptor("test-service", "peer2"),
            CreateTestDescriptor("test-service", "peer3")
        };
        
        var serialized = MessagePackSerializer.Serialize(descriptors);
        _dhtClientMock
            .Setup(d => d.GetRawAsync("svc:test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serialized);

        // All descriptors are valid
        _validatorMock
            .Setup(v => v.Validate(It.IsAny<MeshServiceDescriptor>(), out It.Ref<string>.IsAny))
            .Returns((MeshServiceDescriptor d, out string reason) =>
            {
                reason = string.Empty;
                return true;
            });

        // Act
        var result = await _directory.FindByNameAsync("test-service", CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        _validatorMock.Verify(
            v => v.Validate(It.IsAny<MeshServiceDescriptor>(), out It.Ref<string>.IsAny),
            Times.Exactly(3));
    }

    [Fact]
    public async Task FindByNameAsync_FiltersInvalidDescriptors()
    {
        // Arrange
        var descriptors = new List<MeshServiceDescriptor>
        {
            CreateTestDescriptor("test-service", "peer1"),
            CreateTestDescriptor("test-service", "peer2"),
            CreateTestDescriptor("test-service", "peer3")
        };
        
        var serialized = MessagePackSerializer.Serialize(descriptors);
        _dhtClientMock
            .Setup(d => d.GetRawAsync("svc:test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serialized);

        // Only first descriptor is valid
        var callCount = 0;
        _validatorMock
            .Setup(v => v.Validate(It.IsAny<MeshServiceDescriptor>(), out It.Ref<string>.IsAny))
            .Returns((MeshServiceDescriptor d, out string reason) =>
            {
                callCount++;
                reason = callCount == 1 ? string.Empty : "Invalid";
                return callCount == 1;
            });

        // Act
        var result = await _directory.FindByNameAsync("test-service", CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal("peer1", result[0].OwnerPeerId);
    }

    [Fact]
    public async Task FindByNameAsync_EnforcesMaxDescriptorLimit()
    {
        // Arrange
        _options.MaxDescriptorsPerLookup = 5;
        
        var descriptors = new List<MeshServiceDescriptor>();
        for (int i = 0; i < 10; i++)
        {
            descriptors.Add(CreateTestDescriptor("test-service", $"peer{i}"));
        }
        
        var serialized = MessagePackSerializer.Serialize(descriptors);
        _dhtClientMock
            .Setup(d => d.GetRawAsync("svc:test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serialized);

        // All descriptors are valid
        _validatorMock
            .Setup(v => v.Validate(It.IsAny<MeshServiceDescriptor>(), out It.Ref<string>.IsAny))
            .Returns((MeshServiceDescriptor d, out string reason) =>
            {
                reason = string.Empty;
                return true;
            });

        // Act
        var result = await _directory.FindByNameAsync("test-service", CancellationToken.None);

        // Assert
        Assert.Equal(5, result.Count); // Clamped to max
    }

    [Fact]
    public async Task FindByNameAsync_WithInvalidSerialization_ReturnsEmpty()
    {
        // Arrange
        var invalidData = new byte[] { 0xFF, 0xFE, 0xFD }; // Not valid MessagePack
        _dhtClientMock
            .Setup(d => d.GetRawAsync("svc:test-service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(invalidData);

        // Act
        var result = await _directory.FindByNameAsync("test-service", CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    private MeshServiceDescriptor CreateTestDescriptor(string serviceName, string ownerPeerId)
    {
        var now = DateTimeOffset.UtcNow;
        return new MeshServiceDescriptor
        {
            ServiceId = MeshServiceDescriptor.DeriveServiceId(serviceName, ownerPeerId),
            ServiceName = serviceName,
            Version = "1.0.0",
            OwnerPeerId = ownerPeerId,
            Endpoint = new MeshServiceEndpoint
            {
                Protocol = "quic",
                Host = ownerPeerId,
                Port = 5000
            },
            Metadata = new Dictionary<string, string>(),
            CreatedAt = now,
            ExpiresAt = now.AddHours(1),
            Signature = Array.Empty<byte>()
        };
    }
}
