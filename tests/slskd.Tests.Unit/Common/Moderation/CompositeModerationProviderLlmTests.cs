// <copyright file="CompositeModerationProviderLlmTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP-LM02: CompositeModerationProvider LLM integration.
    /// </summary>
    public class CompositeModerationProviderLlmTests
    {
        private readonly Mock<IOptionsMonitor<ModerationOptions>> _optionsMock = new();
        private readonly Mock<ILogger<CompositeModerationProvider>> _loggerMock = new();
        private readonly Mock<IModerationProvider> _llmProviderMock = new();

        public CompositeModerationProviderLlmTests()
        {
            // Setup default options
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ModerationOptions
            {
                Enabled = true,
                FailsafeMode = "allow",
                LlmModeration = new LlmModerationOptions
                {
                    Enabled = true,
                    FallbackBehavior = "pass_to_next_provider"
                }
            });
        }

        private CompositeModerationProvider CreateProvider()
        {
            return new CompositeModerationProvider(
                _optionsMock.Object,
                _loggerMock.Object,
                llmModerationProvider: _llmProviderMock.Object);
        }

        [Fact]
        public async Task CheckLocalFileAsync_LlmBlocksContent_ReturnsBlocked()
        {
            // Arrange
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Block("llm:blocked_content", "provider:llm"));

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm:", result.Reason);
            _llmProviderMock.Verify(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckLocalFileAsync_LlmAllowsContent_ContinuesToDefault()
        {
            // Arrange
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Allow("llm:allowed_content", "provider:llm"));

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert - Should continue to default Unknown since LLM only blocks, doesn't allow
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("no_blockers_triggered", result.Reason);
        }

        [Fact]
        public async Task CheckLocalFileAsync_LlmReturnsUnknown_ContinuesToDefault()
        {
            // Arrange
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Unknown("llm:inconclusive", "provider:llm"));

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("no_blockers_triggered", result.Reason);
        }

        [Fact]
        public async Task CheckLocalFileAsync_LlmDisabled_SkipsLlmCheck()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ModerationOptions
            {
                Enabled = true,
                LlmModeration = new LlmModerationOptions { Enabled = false }
            });
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            _llmProviderMock.Verify(x => x.CheckLocalFileAsync(It.IsAny<LocalFileMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckLocalFileAsync_LlmProviderFails_AppliesFallbackBehavior()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ModerationOptions
            {
                Enabled = true,
                LlmModeration = new LlmModerationOptions
                {
                    Enabled = true,
                    FallbackBehavior = "block"
                }
            });
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("LLM service unavailable"));

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm_provider_failed_failsafe_block", result.Reason);
        }

        [Fact]
        public async Task CheckLocalFileAsync_LlmProviderNull_SkipsLlmCheck()
        {
            // Arrange - Create provider without LLM provider
            var provider = new CompositeModerationProvider(
                _optionsMock.Object,
                _loggerMock.Object,
                llmModerationProvider: null);
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            _llmProviderMock.Verify(x => x.CheckLocalFileAsync(It.IsAny<LocalFileMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckContentIdAsync_LlmBlocksContent_ReturnsBlocked()
        {
            // Arrange
            var provider = CreateProvider();
            var contentId = "550e8400-e29b-41d4-a716-446655440000";

            _llmProviderMock
                .Setup(x => x.CheckContentIdAsync(contentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Block("llm:blocked_id", "provider:llm"));

            // Act
            var result = await provider.CheckContentIdAsync(contentId);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm:", result.Reason);
            _llmProviderMock.Verify(x => x.CheckContentIdAsync(contentId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckContentIdAsync_LlmReturnsUnknown_ContinuesToDefault()
        {
            // Arrange
            var provider = CreateProvider();
            var contentId = "550e8400-e29b-41d4-a716-446655440000";

            _llmProviderMock
                .Setup(x => x.CheckContentIdAsync(contentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Unknown("llm:inconclusive", "provider:llm"));

            // Act
            var result = await provider.CheckContentIdAsync(contentId);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("no_blockers_triggered", result.Reason);
        }

        [Fact]
        public async Task LlmCalledAfterDeterministicChecks()
        {
            // Arrange - Setup hash blocklist to allow (no block)
            var hashBlocklistMock = new Mock<IHashBlocklistChecker>();
            hashBlocklistMock
                .Setup(x => x.IsBlockedHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var provider = new CompositeModerationProvider(
                _optionsMock.Object,
                _loggerMock.Object,
                hashBlocklist: hashBlocklistMock.Object,
                llmModerationProvider: _llmProviderMock.Object);

            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ModerationDecision.Unknown("llm:processed", "provider:llm"));

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert - LLM was called after hash check
            hashBlocklistMock.Verify(x => x.IsBlockedHashAsync("hash", It.IsAny<CancellationToken>()), Times.Once);
            _llmProviderMock.Verify(x => x.CheckLocalFileAsync(file, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
        }

        [Fact]
        public async Task HashBlocklistBlocks_LlmNotCalled()
        {
            // Arrange - Setup hash blocklist to block
            var hashBlocklistMock = new Mock<IHashBlocklistChecker>();
            hashBlocklistMock
                .Setup(x => x.IsBlockedHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var provider = new CompositeModerationProvider(
                _optionsMock.Object,
                _loggerMock.Object,
                hashBlocklist: hashBlocklistMock.Object,
                llmModerationProvider: _llmProviderMock.Object);

            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert - LLM was not called because hash blocklist blocked first
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("hash_blocklist", result.Reason);
            _llmProviderMock.Verify(x => x.CheckLocalFileAsync(It.IsAny<LocalFileMetadata>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
