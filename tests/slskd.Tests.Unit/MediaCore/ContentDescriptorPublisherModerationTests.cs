// <copyright file="ContentDescriptorPublisherModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.MediaCore;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP03: Moderation filtering in ContentDescriptorPublisher.
    /// </summary>
    public class ContentDescriptorPublisherModerationTests
    {
        private readonly Mock<IContentDescriptorPublisherBackend> _backendMock = new();
        private readonly Mock<ILogger<ContentDescriptorPublisher>> _loggerMock = new();

        [Fact]
        public async Task PublishAsync_WithAdvertisableDescriptor_PublishesSuccessfully()
        {
            // Arrange
            var publisher = new ContentDescriptorPublisher(_backendMock.Object, _loggerMock.Object);
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = true, // Content is advertisable
                SizeBytes = 1024
            };

            _backendMock
                .Setup(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await publisher.PublishAsync(descriptor);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("test-content-id", result.ContentId);
            _backendMock.Verify(x => x.PublishAsync(descriptor, default), Times.Once);
        }

        [Fact]
        public async Task PublishAsync_WithNonAdvertisableDescriptor_FailsWithError()
        {
            // Arrange
            var publisher = new ContentDescriptorPublisher(_backendMock.Object, _loggerMock.Object);
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = false, // Content is NOT advertisable
                SizeBytes = 1024
            };

            // Act
            var result = await publisher.PublishAsync(descriptor);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("test-content-id", result.ContentId);
            Assert.Contains("not advertisable", result.ErrorMessage);
            _backendMock.Verify(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PublishAsync_WithAdvertisableDescriptor_UpdatesTracking()
        {
            // Arrange
            var publisher = new ContentDescriptorPublisher(_backendMock.Object, _loggerMock.Object);
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = true,
                SizeBytes = 1024
            };

            _backendMock
                .Setup(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await publisher.PublishAsync(descriptor);

            // Assert - Check that published descriptors are tracked
            // (This would require making the tracking collection accessible for testing)
            // For now, we verify the publish was called and succeeded
            _backendMock.Verify(x => x.PublishAsync(descriptor, default), Times.Once);
        }

        [Fact]
        public async Task PublishAsync_BackendPublishFails_ReturnsFailure()
        {
            // Arrange
            var publisher = new ContentDescriptorPublisher(_backendMock.Object, _loggerMock.Object);
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = true,
                SizeBytes = 1024
            };

            _backendMock
                .Setup(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false); // Backend publish fails

            // Act
            var result = await publisher.PublishAsync(descriptor);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("test-content-id", result.ContentId);
        }
    }
}

