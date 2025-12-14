// <copyright file="GenericFileContentDomainProviderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.Core.GenericFile
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Moq;
    using slskd.VirtualSoulfind.Core.GenericFile;
    using Xunit;

    /// <summary>
    ///     Tests for T-VC03: GenericFile Domain Provider implementation.
    /// </summary>
    public class GenericFileContentDomainProviderTests
    {
        private readonly Mock<ILogger<GenericFileContentDomainProvider>> _loggerMock;

        public GenericFileContentDomainProviderTests()
        {
            _loggerMock = new Mock<ILogger<GenericFileContentDomainProvider>>();
        }

        [Fact]
        public async Task TryGetItemByLocalMetadataAsync_WithValidMetadata_ReturnsGenericFileItem()
        {
            // Arrange
            var fileMetadata = new LocalFileMetadata("test.pdf", 1024L)
            {
                PrimaryHash = "abc123"
            };

            var provider = new GenericFileContentDomainProvider(_loggerMock.Object);

            // Act
            var result = await provider.TryGetItemByLocalMetadataAsync(fileMetadata);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("test.pdf", result.Filename);
            Assert.Equal(1024L, result.SizeBytes);
            Assert.Equal("abc123", result.PrimaryHash);
            Assert.Equal("test.pdf", result.Title); // Title is filename for GenericFile
            Assert.False(result.IsAdvertisable); // Default to false
        }

        [Fact]
        public async Task TryGetItemByLocalMetadataAsync_WithNullMetadata_ReturnsNull()
        {
            // Arrange
            var provider = new GenericFileContentDomainProvider(_loggerMock.Object);

            // Act
            var result = await provider.TryGetItemByLocalMetadataAsync(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetItemByHashAndFilenameAsync_WithValidParameters_ReturnsGenericFileItem()
        {
            // Arrange
            var provider = new GenericFileContentDomainProvider(_loggerMock.Object);

            // Act
            var result = await provider.TryGetItemByHashAndFilenameAsync("def456", "document.docx", 2048L);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("document.docx", result.Filename);
            Assert.Equal(2048L, result.SizeBytes);
            Assert.Equal("def456", result.PrimaryHash);
            Assert.Equal("document.docx", result.Title);
            Assert.False(result.IsAdvertisable);
        }

        [Fact]
        public async Task TryGetItemByHashAndFilenameAsync_WithEmptyHash_ReturnsNull()
        {
            // Arrange
            var provider = new GenericFileContentDomainProvider(_loggerMock.Object);

            // Act
            var result = await provider.TryGetItemByHashAndFilenameAsync("", "file.txt", 100L);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task TryGetItemByHashAndFilenameAsync_WithEmptyFilename_ReturnsNull()
        {
            // Arrange
            var provider = new GenericFileContentDomainProvider(_loggerMock.Object);

            // Act
            var result = await provider.TryGetItemByHashAndFilenameAsync("hash123", "", 100L);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GenericFileItem_Domain_IsGenericFile()
        {
            // Arrange
            var item = GenericFileItem.FromLocalFileMetadata(
                new LocalFileMetadata("test.txt", 100L) { PrimaryHash = "hash" });

            // Assert
            Assert.Equal(VirtualSoulfind.Core.ContentDomain.GenericFile, item.Domain);
        }

        [Fact]
        public void GenericFileItem_WorkId_IsNull()
        {
            // Arrange
            var item = GenericFileItem.FromLocalFileMetadata(
                new LocalFileMetadata("test.txt", 100L) { PrimaryHash = "hash" });

            // Assert
            Assert.Null(item.WorkId); // GenericFile items don't have parent works
        }

        [Fact]
        public void GenericFileItem_Position_IsNull()
        {
            // Arrange
            var item = GenericFileItem.FromLocalFileMetadata(
                new LocalFileMetadata("test.txt", 100L) { PrimaryHash = "hash" });

            // Assert
            Assert.Null(item.Position); // No position in GenericFile domain
        }

        [Fact]
        public void GenericFileItem_Duration_IsNull()
        {
            // Arrange
            var item = GenericFileItem.FromLocalFileMetadata(
                new LocalFileMetadata("test.txt", 100L) { PrimaryHash = "hash" });

            // Assert
            Assert.Null(item.Duration); // No duration for generic files
        }
    }
}

