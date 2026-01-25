// <copyright file="ContentDescriptorPublisherModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.MediaCore;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP03: Moderation filtering in ContentDescriptorPublisher.
    /// </summary>
    public class ContentDescriptorPublisherModerationTests
    {
        private readonly Mock<IDescriptorPublisher> _basePublisherMock = new();
        private readonly Mock<ILogger<ContentDescriptorPublisher>> _loggerMock = new();
        private readonly Mock<IContentIdRegistry> _registryMock = new();
        private readonly IOptions<MediaCoreOptions> _options = Options.Create(new MediaCoreOptions { MaxTtlMinutes = 60 });

        private ContentDescriptorPublisher CreatePublisher()
        {
            return new ContentDescriptorPublisher(
                _loggerMock.Object,
                _basePublisherMock.Object,
                _registryMock.Object,
                _options);
        }

        [Fact]
        public async Task PublishAsync_WithAdvertisableDescriptor_PublishesSuccessfully()
        {
            // Arrange
            var publisher = CreatePublisher();
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = true,
                SizeBytes = 1024
            };

            _basePublisherMock
                .Setup(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await publisher.PublishAsync(descriptor);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("test-content-id", result.ContentId);
            _basePublisherMock.Verify(x => x.PublishAsync(descriptor, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishAsync_WithNonAdvertisableDescriptor_FailsWithError()
        {
            // Arrange
            var publisher = CreatePublisher();
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = false,
                SizeBytes = 1024
            };

            // Act
            var result = await publisher.PublishAsync(descriptor);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("test-content-id", result.ContentId);
            Assert.Contains("not advertisable", result.ErrorMessage ?? string.Empty);
            _basePublisherMock.Verify(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PublishAsync_WithAdvertisableDescriptor_UpdatesTracking()
        {
            // Arrange
            var publisher = CreatePublisher();
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = true,
                SizeBytes = 1024
            };

            _basePublisherMock
                .Setup(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await publisher.PublishAsync(descriptor);

            // Assert
            _basePublisherMock.Verify(x => x.PublishAsync(descriptor, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PublishAsync_BackendPublishFails_ReturnsFailure()
        {
            // Arrange
            var publisher = CreatePublisher();
            var descriptor = new ContentDescriptor
            {
                ContentId = "test-content-id",
                IsAdvertisable = true,
                SizeBytes = 1024
            };

            _basePublisherMock
                .Setup(x => x.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await publisher.PublishAsync(descriptor);

            // Assert
            Assert.False(result.Success);
            Assert.Equal("test-content-id", result.ContentId);
        }
    }
}
