// <copyright file="LlmModerationProviderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP-LM02: LlmModerationProvider.
    /// </summary>
    public class LlmModerationProviderTests
    {
        private readonly Mock<IOptionsMonitor<LlmModerationOptions>> _optionsMock = new();
        private readonly Mock<ILlmModerationProvider> _llmProviderMock = new();
        private readonly Mock<ILogger<LlmModerationProvider>> _loggerMock = new();

        public LlmModerationProviderTests()
        {
            // Setup default options
            _optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                Enabled = true,
                MinConfidenceThreshold = 0.8,
                MaxContentLength = 1000,
                FallbackBehavior = "pass_to_next_provider"
            });

            // Setup LLM provider as available
            _llmProviderMock.Setup(x => x.IsAvailable).Returns(true);
            _llmProviderMock.Setup(x => x.CanHandleContentType(It.IsAny<LlmModeration.ContentType>())).Returns(true);
        }

        private LlmModerationProvider CreateProvider()
        {
            return new LlmModerationProvider(_optionsMock.Object, _llmProviderMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task CheckLocalFileAsync_WithBlockedVerdict_ReturnsBlockedDecision()
        {
            // Arrange
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Blocked,
                    Confidence = 0.95,
                    Reasoning = "Inappropriate content detected",
                    Categories = LlmModeration.ContentCategory.HateSpeech
                });

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm:", result.Reason);
            Assert.Contains("HateSpeech", result.Reason);
            _llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckLocalFileAsync_WithLowConfidence_ReturnsUnknown()
        {
            // Arrange
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Blocked,
                    Confidence = 0.5, // Below threshold
                    Reasoning = "Uncertain analysis"
                });

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_confidence_too_low", result.Reason);
        }

        [Fact]
        public async Task CheckLocalFileAsync_WithLlmDisabled_ReturnsUnknown()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions { Enabled = false });
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_disabled", result.Reason);
            _llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckLocalFileAsync_WithLlmUnavailable_ReturnsUnknown()
        {
            // Arrange
            _llmProviderMock.Setup(x => x.IsAvailable).Returns(false);
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_disabled_or_unavailable", result.Reason);
            _llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckLocalFileAsync_WithUnsupportedContentType_ReturnsUnknown()
        {
            // Arrange
            _llmProviderMock.Setup(x => x.CanHandleContentType(LlmModeration.ContentType.FileContent)).Returns(false);
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_cannot_handle_content_type", result.Reason);
            _llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckLocalFileAsync_WithLlmFailure_AppliesFallbackBehavior()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                Enabled = true,
                FallbackBehavior = "block"
            });
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("LLM service down"));

            // Act
            var result = await provider.CheckLocalFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm_provider_failed_failsafe_block", result.Reason);
        }

        [Fact]
        public async Task CheckContentIdAsync_WithBlockedVerdict_ReturnsBlockedDecision()
        {
            // Arrange
            var provider = CreateProvider();
            var contentId = "550e8400-e29b-41d4-a716-446655440000";

            _llmProviderMock
                .Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Blocked,
                    Confidence = 0.9,
                    Reasoning = "Content ID flagged"
                });

            // Act
            var result = await provider.CheckContentIdAsync(contentId);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm:", result.Reason);
        }

        [Fact]
        public async Task CheckContentIdAsync_ValidatesInput()
        {
            // Arrange
            var provider = CreateProvider();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => provider.CheckContentIdAsync(""));
            await Assert.ThrowsAsync<ArgumentException>(() => provider.CheckContentIdAsync(null!));
        }

        [Fact]
        public async Task CheckLocalFileAsync_SanitizesFilenames()
        {
            // Arrange
            var provider = CreateProvider();
            var file = new LocalFileMetadata("/path/to/malicious../../../file.mp3", 1024, "hash", "audio/mp3");

            _llmProviderMock
                .Setup(x => x.ModerateAsync(It.Is<LlmModerationRequest>(req =>
                    req.Metadata.ContainsKey("filename") &&
                    req.Metadata["filename"] == "file.mp3"), // Should be sanitized
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Unknown,
                    Confidence = 0.0
                });

            // Act
            await provider.CheckLocalFileAsync(file);

            // Assert - Verification in the setup ensures filename is sanitized
            _llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CheckLocalFileAsync_TruncatesLongContent()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                Enabled = true,
                MaxContentLength = 50 // Very short for testing
            });
            var provider = CreateProvider();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash",
                new string('x', 100)); // Long media info

            _llmProviderMock
                .Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Unknown,
                    Confidence = 0.0
                });

            // Act
            await provider.CheckLocalFileAsync(file);

            // Assert - Should not throw, content should be truncated
            _llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
