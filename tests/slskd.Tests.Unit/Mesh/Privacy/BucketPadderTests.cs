// <copyright file="BucketPadderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Privacy;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Privacy;

public class BucketPadderTests : IDisposable
{
    private readonly Mock<ILogger<BucketPadder>> _loggerMock;
    private readonly BucketPadder _padder;

    public BucketPadderTests()
    {
        _loggerMock = new Mock<ILogger<BucketPadder>>();
        _padder = new BucketPadder(_loggerMock.Object, 1024);
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    [Fact]
    public void Constructor_WithValidBucketSize_Succeeds()
    {
        // Act & Assert - Should not throw
        var padder = new BucketPadder(_loggerMock.Object, 2048);
        Assert.Equal(2048, padder.BucketSize);
    }

    [Theory]
    [InlineData(512)]
    [InlineData(1024)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(8192)]
    [InlineData(16384)]
    public void Constructor_WithStandardBucketSizes_Succeeds(int bucketSize)
    {
        // Act & Assert - Should not throw
        var padder = new BucketPadder(_loggerMock.Object, bucketSize);
        Assert.Equal(bucketSize, padder.BucketSize);
    }

    [Fact]
    public void Pad_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _padder.Pad(null!));
    }

    [Fact]
    public void Pad_WithMessageLargerThanBucket_ThrowsArgumentException()
    {
        // Arrange
        var largeMessage = new byte[2048];

        // Act & Assert
        var padder = new BucketPadder(_loggerMock.Object, 1024);
        Assert.Throws<ArgumentException>(() => padder.Pad(largeMessage));
    }

    [Fact]
    public void Pad_WithValidMessage_ReturnsPaddedMessage()
    {
        // Arrange
        var originalMessage = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var paddedMessage = _padder.Pad(originalMessage);

        // Assert
        Assert.Equal(1024, paddedMessage.Length);
        Assert.Equal(originalMessage.Length, BitConverter.ToUInt16(paddedMessage, 1022)); // Length in big-endian
    }

    [Fact]
    public void Unpad_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _padder.Unpad(null!));
    }

    [Fact]
    public void Unpad_WithWrongSizeMessage_ThrowsArgumentException()
    {
        // Arrange
        var wrongSizeMessage = new byte[512]; // Wrong bucket size

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _padder.Unpad(wrongSizeMessage));
    }

    [Fact]
    public void PadAndUnpad_WithValidMessage_RoundTripsCorrectly()
    {
        // Arrange
        var originalMessage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        // Act
        var paddedMessage = _padder.Pad(originalMessage);
        var unpaddedMessage = _padder.Unpad(paddedMessage);

        // Assert
        Assert.Equal(originalMessage, unpaddedMessage);
    }

    [Fact]
    public void PadAndUnpad_WithEmptyMessage_RoundTripsCorrectly()
    {
        // Arrange
        var originalMessage = Array.Empty<byte>();

        // Act
        var paddedMessage = _padder.Pad(originalMessage);
        var unpaddedMessage = _padder.Unpad(paddedMessage);

        // Assert
        Assert.Equal(originalMessage, unpaddedMessage);
    }

    [Fact]
    public void PadAndUnpad_WithMaxSizeMessage_RoundTripsCorrectly()
    {
        // Arrange
        var originalMessage = new byte[1022]; // Max size (bucket - 2 for header)
        for (int i = 0; i < originalMessage.Length; i++)
        {
            originalMessage[i] = (byte)(i % 256);
        }

        // Act
        var paddedMessage = _padder.Pad(originalMessage);
        var unpaddedMessage = _padder.Unpad(paddedMessage);

        // Assert
        Assert.Equal(originalMessage, unpaddedMessage);
    }

    [Fact]
    public void SetBucketSize_WithValidSize_Succeeds()
    {
        // Act
        _padder.SetBucketSize(2048);

        // Assert
        Assert.Equal(2048, _padder.BucketSize);
    }

    [Fact]
    public void SetBucketSize_WithInvalidSize_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _padder.SetBucketSize(999));
    }

    [Theory]
    [InlineData(100, 512)]
    [InlineData(400, 512)]
    [InlineData(600, 1024)]
    [InlineData(1500, 2048)]
    public void GetOptimalBucketSize_WithVariousSizes_ReturnsCorrectBucket(int messageSize, int expectedBucket)
    {
        // Act
        var result = BucketPadder.GetOptimalBucketSize(messageSize);

        // Assert
        Assert.Equal(expectedBucket, result);
    }

    [Theory]
    [InlineData(100, 512, 412)]
    [InlineData(400, 512, 112)]
    [InlineData(500, 1024, 524)]
    public void GetPaddingOverhead_WithVariousSizes_ReturnsCorrectOverhead(int messageSize, int bucketSize, int expectedOverhead)
    {
        // Act
        var result = BucketPadder.GetPaddingOverhead(messageSize, bucketSize);

        // Assert
        Assert.Equal(expectedOverhead, result);
    }

    [Fact]
    public void GetPaddingOverhead_WithMessageTooLarge_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => BucketPadder.GetPaddingOverhead(600, 512));
    }

    [Fact]
    public void StandardBucketSizes_ContainsExpectedValues()
    {
        // Assert
        Assert.Contains(512, BucketPadder.StandardBucketSizes);
        Assert.Contains(1024, BucketPadder.StandardBucketSizes);
        Assert.Contains(2048, BucketPadder.StandardBucketSizes);
        Assert.Contains(4096, BucketPadder.StandardBucketSizes);
        Assert.Contains(8192, BucketPadder.StandardBucketSizes);
        Assert.Contains(16384, BucketPadder.StandardBucketSizes);
        Assert.Equal(6, BucketPadder.StandardBucketSizes.Length);
    }
}


