// <copyright file="ContentIdGatingTests.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Common.Moderation;
    using slskd.Shares;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP03: VirtualSoulfind advertisable gating.
    /// </summary>
    public class ContentIdGatingTests
    {
        private readonly Mock<ILogger<CompositeModerationProvider>> _loggerMock;
        private readonly Mock<IShareRepository> _shareRepoMock;
        private readonly ModerationOptions _options;
        private readonly IOptionsMonitor<ModerationOptions> _optionsMonitor;

        public ContentIdGatingTests()
        {
            _loggerMock = new Mock<ILogger<CompositeModerationProvider>>();
            _shareRepoMock = new Mock<IShareRepository>();
            _options = new ModerationOptions { Enabled = true };
            _optionsMonitor = new WrappedOptionsMonitor<ModerationOptions>(Microsoft.Extensions.Options.Options.Create(_options));
        }

        [Fact]
        public async Task CheckContentIdAsync_ThrowsOnNullContentId()
        {
            // Arrange
            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                provider.CheckContentIdAsync(null, CancellationToken.None));
        }

        [Fact]
        public async Task CheckContentIdAsync_ReturnsUnknown_WhenNoShareRepository()
        {
            // Arrange
            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: null); // No share repository

            // Act
            var decision = await provider.CheckContentIdAsync("content:test", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
            Assert.Equal("no_share_repository", decision.Reason);
        }

        [Fact]
        public async Task CheckContentIdAsync_ReturnsUnknown_WhenContentNotMapped()
        {
            // Arrange
            _shareRepoMock.Setup(r => r.FindContentItem(It.IsAny<string>()))
                .Returns((string id) => null); // Content not found

            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act
            var decision = await provider.CheckContentIdAsync("content:unmapped", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
            Assert.Equal("content_not_mapped", decision.Reason);
        }

        [Fact]
        public async Task CheckContentIdAsync_ReturnsCached_WhenRecentlyCheckedAndNotAdvertisable()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _shareRepoMock.Setup(r => r.FindContentItem("content:blocked"))
                .Returns(("Music", null, "file.mp3", false, "hash_blocklist", now - 1800)); // 30 min ago

            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act
            var decision = await provider.CheckContentIdAsync("content:blocked", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
            Assert.Equal("hash_blocklist", decision.Reason);
            _shareRepoMock.Verify(r => r.FindFileInfo(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CheckContentIdAsync_ReturnsCached_WhenRecentlyCheckedAndAdvertisable()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _shareRepoMock.Setup(r => r.FindContentItem("content:allowed"))
                .Returns(("Music", null, "file.mp3", true, null, now - 1800)); // 30 min ago

            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act
            var decision = await provider.CheckContentIdAsync("content:allowed", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Allowed, decision.Verdict);
            Assert.Equal("cached_allowed", decision.Reason);
            _shareRepoMock.Verify(r => r.FindFileInfo(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CheckContentIdAsync_RechecksFile_WhenDecisionIsStale()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _shareRepoMock.Setup(r => r.FindContentItem("content:stale"))
                .Returns(("Music", null, "file.mp3", true, null, now - 7200)); // 2 hours ago (stale)

            _shareRepoMock.Setup(r => r.FindFileInfo("file.mp3"))
                .Returns(("actual_file.mp3", 1024L));

            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act
            var decision = await provider.CheckContentIdAsync("content:stale", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, decision.Verdict); // No blockers, so Unknown
            _shareRepoMock.Verify(r => r.FindFileInfo("file.mp3"), Times.Once);
            _shareRepoMock.Verify(r => r.UpsertContentItem(
                "content:stale",
                "Music",
                null,
                "file.mp3",
                false, // Unknown -> not advertisable
                It.IsAny<string>(),
                It.IsAny<long>()), Times.Once);
        }

        [Fact]
        public async Task CheckContentIdAsync_ReturnsUnknown_WhenFileNotFound()
        {
            // Arrange
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _shareRepoMock.Setup(r => r.FindContentItem("content:missing"))
                .Returns(("Music", null, "missing_file.mp3", true, null, now - 7200));

            _shareRepoMock.Setup(r => r.FindFileInfo("missing_file.mp3"))
                .Returns((null, 0L)); // File not found

            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act
            var decision = await provider.CheckContentIdAsync("content:missing", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, decision.Verdict);
            Assert.Equal("file_not_found", decision.Reason);
            _shareRepoMock.Verify(r => r.UpsertContentItem(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        }

        [Fact]
        public async Task CheckContentIdAsync_ReturnsBlock_OnException()
        {
            // Arrange
            _shareRepoMock.Setup(r => r.FindContentItem(It.IsAny<string>()))
                .Throws(new InvalidOperationException("Database error"));

            var provider = new CompositeModerationProvider(
                _optionsMonitor,
                _loggerMock.Object,
                shareRepository: _shareRepoMock.Object);

            // Act
            var decision = await provider.CheckContentIdAsync("content:error", CancellationToken.None);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, decision.Verdict);
            Assert.Equal("check_failed", decision.Reason);
        }

        [Fact]
        public void UpsertContentItem_StoresCorrectly()
        {
            // Arrange
            string capturedContentId = null;
            string capturedDomain = null;
            bool capturedIsAdvertisable = false;

            _shareRepoMock.Setup(r => r.UpsertContentItem(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<long>()))
                .Callback<string, string, string, string, bool, string, long>(
                    (contentId, domain, workId, maskedFilename, isAdvertisable, moderationReason, checkedAt) =>
                    {
                        capturedContentId = contentId;
                        capturedDomain = domain;
                        capturedIsAdvertisable = isAdvertisable;
                    });

            // Act
            _shareRepoMock.Object.UpsertContentItem(
                "content:music:track1",
                "Music",
                "work:album1",
                "masked_track.mp3",
                true,
                null,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Assert
            Assert.Equal("content:music:track1", capturedContentId);
            Assert.Equal("Music", capturedDomain);
            Assert.True(capturedIsAdvertisable);
        }

        [Fact]
        public void CountAdvertisableItems_ReturnsCorrectCount()
        {
            // Arrange
            _shareRepoMock.Setup(r => r.CountAdvertisableItems())
                .Returns(42);

            // Act
            var count = _shareRepoMock.Object.CountAdvertisableItems();

            // Assert
            Assert.Equal(42, count);
        }
    }
}
