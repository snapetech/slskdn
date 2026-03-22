using System;
using System.Collections.Generic;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Moderation;
using slskd.Mesh.ServiceFabric;
using Xunit;
using Moq;

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

/// <summary>
/// Unit tests for MeshServiceDescriptorValidator.
/// </summary>
public class MeshServiceDescriptorValidatorTests
{
    private readonly Mock<ILogger<MeshServiceDescriptorValidator>> _loggerMock;
    private readonly Mock<IPeerReputationStore> _peerReputationStoreMock;
    private readonly MeshServiceFabricOptions _options;
    private readonly MeshServiceDescriptorValidator _validator;

    public MeshServiceDescriptorValidatorTests()
    {
        _loggerMock = new Mock<ILogger<MeshServiceDescriptorValidator>>();
        _peerReputationStoreMock = new Mock<IPeerReputationStore>();
        _options = new MeshServiceFabricOptions();
        var optionsMock = new Mock<IOptions<MeshServiceFabricOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        _peerReputationStoreMock.Setup(x => x.IsPeerBannedAsync(It.IsAny<string>(), default)).ReturnsAsync(false);

        var peerReputationService = new PeerReputationService(
            new Mock<ILogger<PeerReputationService>>().Object,
            _peerReputationStoreMock.Object);

        _validator = new MeshServiceDescriptorValidator(_loggerMock.Object, optionsMock.Object, peerReputationService);
    }

    [Fact]
    public async Task Validate_WithValidDescriptor_ReturnsTrue()
    {
        // Arrange
        var descriptor = CreateValidDescriptor();

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.True(isValid);
        Assert.Empty(reason);
    }

    [Fact]
    public async Task Validate_WithEmptyServiceId_ReturnsFalse()
    {
        // Arrange
        var descriptor = CreateValidDescriptor() with { ServiceId = "" };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("ServiceId", reason);
    }

    [Fact]
    public async Task Validate_WithMismatchedServiceId_ReturnsFalse()
    {
        // Arrange
        var descriptor = CreateValidDescriptor() with { ServiceId = "wrong-id" };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("ServiceId mismatch", reason);
    }

    [Fact]
    public async Task Validate_WithFutureCreatedAt_ReturnsFalse()
    {
        // Arrange
        var futureTime = DateTimeOffset.UtcNow.AddMinutes(10);
        var descriptor = CreateValidDescriptor() with
        {
            CreatedAt = futureTime,
            ExpiresAt = futureTime.AddHours(1)
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("future", reason);
    }

    [Fact]
    public async Task Validate_WithExpiredDescriptor_ReturnsFalse()
    {
        // Arrange
        var pastTime = DateTimeOffset.UtcNow.AddHours(-2);
        var descriptor = CreateValidDescriptor() with
        {
            CreatedAt = pastTime.AddHours(-1),
            ExpiresAt = pastTime
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("expired", reason);
    }

    [Fact]
    public async Task Validate_WithCreatedAfterExpires_ReturnsFalse()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var descriptor = CreateValidDescriptor() with
        {
            CreatedAt = now.AddMinutes(10),
            ExpiresAt = now.AddMinutes(5) // Expires before created!
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        // Either "before ExpiresAt" or "expired" or "future" is acceptable
        Assert.True(
            reason.Contains("before ExpiresAt") ||
            reason.Contains("expired") ||
            reason.Contains("future"),
            $"Unexpected reason: {reason}");
    }

    [Fact]
    public async Task Validate_WithTooManyMetadataEntries_ReturnsFalse()
    {
        // Arrange
        var metadata = new Dictionary<string, string>();
        for (int i = 0; i < 15; i++) // More than default max of 10
        {
            metadata[$"key{i}"] = $"value{i}";
        }

        var descriptor = CreateValidDescriptor() with { Metadata = metadata };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("Too many metadata", reason);
    }

    [Fact]
    public async Task Validate_WhenSerializationThrows_ReturnsSanitizedReason()
    {
        // Arrange
        var descriptor = CreateValidDescriptor() with
        {
            Metadata = new Dictionary<string, string>
            {
                { "type", "\ud800" }
            }
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Equal("Failed to serialize descriptor", reason);
    }

    [Theory]
    [InlineData("username")]
    [InlineData("Username")]
    [InlineData("email")]
    [InlineData("Email")]
    [InlineData("ip")]
    [InlineData("IP")]
    public async Task Validate_WithPIIInMetadata_ReturnsFalse(string piiKey)
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { piiKey, "some-value" }
        };
        var descriptor = CreateValidDescriptor() with { Metadata = metadata };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("PII", reason);
    }

    [Fact]
    public async Task Validate_WithOversizedDescriptor_ReturnsFalse()
    {
        // Arrange
        _options.MaxDescriptorBytes = 100; // Very small limit
        var largeMetadata = new Dictionary<string, string>();
        for (int i = 0; i < 5; i++)
        {
            largeMetadata[$"key{i}"] = new string('x', 1000); // Large values
        }
        var descriptor = CreateValidDescriptor() with { Metadata = largeMetadata };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("too large", reason);
    }

    [Fact]
    public async Task Validate_WithInvalidSignatureLength_ReturnsFalse()
    {
        // Arrange
        var descriptor = CreateValidDescriptor() with
        {
            Signature = new byte[32] // Wrong length, should be 64 for Ed25519
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("signature length", reason);
    }

    [Fact]
    public async Task Validate_WithCorrectSignatureLength_ReturnsTrue()
    {
        // Arrange
        var descriptor = CreateValidDescriptor() with
        {
            Signature = new byte[64] // Correct Ed25519 signature length
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.True(isValid);
        Assert.Empty(reason);
    }

    [Fact]
    public async Task Validate_WithoutSignature_WhenValidationEnabled_ReturnsFalse()
    {
        // Arrange
        _options.ValidateDhtSignatures = true;
        var descriptor = CreateValidDescriptor() with
        {
            Signature = Array.Empty<byte>()
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.False(isValid);
        Assert.Contains("Signature required", reason);
    }

    [Fact]
    public async Task Validate_WithBannedPeer_ReturnsFalse()
    {
        // Arrange
        _peerReputationStoreMock.Reset();
        _peerReputationStoreMock.Setup(x => x.IsPeerBannedAsync(It.IsAny<string>(), default)).ReturnsAsync(true);

        var peerReputationService = new PeerReputationService(
            new Mock<ILogger<PeerReputationService>>().Object,
            _peerReputationStoreMock.Object);

        var validator = new MeshServiceDescriptorValidator(
            _loggerMock.Object,
            Mock.Of<IOptions<MeshServiceFabricOptions>>(x => x.Value == _options),
            peerReputationService);

        // Act
        var (isValid, reason) = await validator.ValidateAsync(CreateValidDescriptor());

        // Assert
        Assert.False(isValid);
        Assert.Contains("banned or quarantined", reason);
    }

    [Fact]
    public async Task Validate_WithoutSignature_WhenValidationDisabled_ReturnsTrue()
    {
        // Arrange
        _options.ValidateDhtSignatures = false;
        var descriptor = CreateValidDescriptor() with
        {
            Signature = Array.Empty<byte>()
        };

        // Act
        var (isValid, reason) = await _validator.ValidateAsync(descriptor);

        // Assert
        Assert.True(isValid);
        Assert.Empty(reason);
    }

    private MeshServiceDescriptor CreateValidDescriptor()
    {
        var serviceName = "test-service";
        var ownerPeerId = "peer123";
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
            Metadata = new Dictionary<string, string>
            {
                { "type", "test" }
            },
            CreatedAt = now,
            ExpiresAt = now.AddHours(1),
            Signature = new byte[64]
        };
    }
}
