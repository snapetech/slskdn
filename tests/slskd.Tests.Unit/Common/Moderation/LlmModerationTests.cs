// <copyright file="LlmModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP-LM01: LLM Moderation functionality.
    /// </summary>
    public class LlmModerationTests
    {
        [Fact]
        public void LlmModerationRequest_Validation_Works()
        {
            // Arrange & Act
            var request = new LlmModerationRequest
            {
                RequestId = "test-id",
                ContentType = LlmModeration.ContentType.Text,
                Content = "Test content",
                Context = "test",
                Source = "unit_test"
            };

            // Assert
            Assert.Equal("test-id", request.RequestId);
            Assert.Equal(LlmModeration.ContentType.Text, request.ContentType);
            Assert.Equal("Test content", request.Content);
            Assert.Equal("test", request.Context);
            Assert.Equal("unit_test", request.Source);
            Assert.True(request.Timestamp > default);
        }

        [Fact]
        public void LlmModerationResponse_Defaults_AreSafe()
        {
            // Arrange & Act
            var response = new LlmModerationResponse
            {
                RequestId = "test-id"
            };

            // Assert - Safe defaults
            Assert.Equal("test-id", response.RequestId);
            Assert.Equal(ModerationVerdict.Unknown, response.Verdict);
            Assert.Equal(LlmModeration.SeverityLevel.Safe, response.Severity);
            Assert.Equal(LlmModeration.ContentCategory.None, response.Categories);
            Assert.Equal(0.0, response.Confidence);
            Assert.Equal(string.Empty, response.Reasoning);
            Assert.NotNull(response.Details);
            Assert.True(response.Timestamp > default);
            Assert.False(response.FromCache);
            Assert.Null(response.Error);
        }

        [Fact]
        public void LlmModerationOptions_Defaults_AreConservative()
        {
            // Arrange & Act
            var options = new LlmModerationOptions();

            // Assert - Conservative defaults
            Assert.False(options.Enabled); // Disabled by default
            Assert.Equal(string.Empty, options.Endpoint);
            Assert.Equal(string.Empty, options.ApiKey);
            Assert.Equal("gpt-4", options.Model);
            Assert.Equal(0.8, options.MinConfidenceThreshold);
            Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
            Assert.Equal(10000, options.MaxContentLength);
            Assert.Equal(TimeSpan.FromHours(24), options.CacheTtl);
            Assert.Equal("pass_to_next_provider", options.FallbackBehavior);
        }

        [Fact]
        public async Task NoopLlmModerationProvider_AlwaysReturnsUnknown()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<NoopLlmModerationProvider>>();
            var provider = new NoopLlmModerationProvider(loggerMock.Object);

            var request = new LlmModerationRequest
            {
                ContentType = LlmModeration.ContentType.Text,
                Content = "Test content"
            };

            // Act
            var response = await provider.ModerateAsync(request);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, response.Verdict);
            Assert.Equal(LlmModeration.SeverityLevel.Safe, response.Severity);
            Assert.Equal(LlmModeration.ContentCategory.None, response.Categories);
            Assert.Equal(0.0, response.Confidence);
            Assert.Contains("disabled or unavailable", response.Reasoning);
            Assert.True(provider.IsAvailable);
            Assert.Equal("NoopLlmModerationProvider", provider.ProviderName);
        }

        [Fact]
        public async Task NoopLlmModerationProvider_HealthStatus_IsAlwaysHealthy()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<NoopLlmModerationProvider>>();
            var provider = new NoopLlmModerationProvider(loggerMock.Object);

            // Act
            var health = await provider.GetHealthStatusAsync();

            // Assert
            Assert.True(health.IsHealthy);
            Assert.Equal("NoopLlmModerationProvider", health.ProviderName);
            Assert.Null(health.LastError);
            Assert.Contains("always_healthy", health.Details);
        }

        [Fact]
        public void NoopLlmModerationProvider_CanHandleAnyContentType()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<NoopLlmModerationProvider>>();
            var provider = new NoopLlmModerationProvider(loggerMock.Object);

            // Act & Assert
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.Text));
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.FileContent));
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.Metadata));
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.UserProfile));
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.SocialContent));
        }

        [Fact]
        public async Task HttpLlmModerationProvider_IsAvailable_RequiresConfiguration()
        {
            // Arrange
            var httpClient = new HttpClient();
            var optionsMock = new Mock<IOptionsMonitor<LlmModerationOptions>>();
            var loggerMock = new Mock<ILogger<HttpLlmModerationProvider>>();

            // Test with missing endpoint
            optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                ApiKey = "test-key",
                Endpoint = string.Empty // Missing endpoint
            });

            var provider = new HttpLlmModerationProvider(httpClient, optionsMock.Object, loggerMock.Object);

            // Assert
            Assert.False(provider.IsAvailable);

            // Test with missing API key
            optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                Endpoint = "https://api.example.com",
                ApiKey = string.Empty // Missing API key
            });

            var provider2 = new HttpLlmModerationProvider(httpClient, optionsMock.Object, loggerMock.Object);

            // Assert
            Assert.False(provider2.IsAvailable);
        }

        [Fact]
        public void HttpLlmModerationProvider_CanHandleSupportedContentTypes()
        {
            // Arrange
            var httpClient = new HttpClient();
            var optionsMock = new Mock<IOptionsMonitor<LlmModerationOptions>>();
            optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                Endpoint = "https://api.example.com",
                ApiKey = "test-key"
            });
            var loggerMock = new Mock<ILogger<HttpLlmModerationProvider>>();

            var provider = new HttpLlmModerationProvider(httpClient, optionsMock.Object, loggerMock.Object);

            // Act & Assert
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.Text));
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.FileContent));
            Assert.True(provider.CanHandleContentType(LlmModeration.ContentType.Metadata));
            Assert.False(provider.CanHandleContentType(LlmModeration.ContentType.UserProfile)); // Not supported
            Assert.False(provider.CanHandleContentType(LlmModeration.ContentType.SocialContent)); // Not supported
        }

        [Fact]
        public async Task HttpLlmModerationProvider_HandlesHttpErrorsGracefully()
        {
            // Arrange
            var handler = new MockHttpMessageHandler();
            handler.SetupResponse(req => req.RequestUri?.ToString().Contains("/chat/completions") == true,
                HttpStatusCode.InternalServerError, "Internal Server Error");

            var httpClient = new HttpClient(handler);
            var optionsMock = new Mock<IOptionsMonitor<LlmModerationOptions>>();
            optionsMock.Setup(x => x.CurrentValue).Returns(new LlmModerationOptions
            {
                Endpoint = "https://api.example.com",
                ApiKey = "test-key",
                Timeout = TimeSpan.FromSeconds(1)
            });
            var loggerMock = new Mock<ILogger<HttpLlmModerationProvider>>();

            var provider = new HttpLlmModerationProvider(httpClient, optionsMock.Object, loggerMock.Object);

            var request = new LlmModerationRequest
            {
                ContentType = LlmModeration.ContentType.Text,
                Content = "Test content"
            };

            // Act
            var response = await provider.ModerateAsync(request);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, response.Verdict);
            Assert.Equal(LlmModeration.SeverityLevel.Safe, response.Severity);
            Assert.Equal(0.0, response.Confidence);
            Assert.NotNull(response.Error);
            Assert.Contains("Internal Server Error", response.Error);
        }

        [Fact]
        public async Task CompositeModerationProvider_IntegratesLlmModeration()
        {
            // Arrange
            var optionsMock = new Mock<IOptionsMonitor<ModerationOptions>>();
            optionsMock.Setup(x => x.CurrentValue).Returns(new ModerationOptions
            {
                Enabled = true,
                FailsafeMode = "allow",
                LlmModeration = new LlmModerationOptions
                {
                    Enabled = true,
                    MinConfidenceThreshold = 0.5
                }
            });

            var loggerMock = new Mock<ILogger<CompositeModerationProvider>>();
            var llmProviderMock = new Mock<ILlmModerationProvider>();
            llmProviderMock.Setup(x => x.IsAvailable).Returns(true);
            llmProviderMock.Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Blocked,
                    Confidence = 0.9,
                    Reasoning = "Test block",
                    Categories = LlmModeration.ContentCategory.HateSpeech
                });

            var provider = new CompositeModerationProvider(
                optionsMock.Object,
                loggerMock.Object,
                llmProvider: llmProviderMock.Object);

            var fileMetadata = new LocalFileMetadata(
                id: "test.mp3",
                sizeBytes: 1024,
                primaryHash: "testhash",
                mediaInfo: "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(fileMetadata);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm:", result.Reason);
            llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CompositeModerationProvider_LlmLowConfidence_IsIgnored()
        {
            // Arrange
            var optionsMock = new Mock<IOptionsMonitor<ModerationOptions>>();
            optionsMock.Setup(x => x.CurrentValue).Returns(new ModerationOptions
            {
                Enabled = true,
                LlmModeration = new LlmModerationOptions
                {
                    Enabled = true,
                    MinConfidenceThreshold = 0.8 // High threshold
                }
            });

            var loggerMock = new Mock<ILogger<CompositeModerationProvider>>();
            var llmProviderMock = new Mock<ILlmModerationProvider>();
            llmProviderMock.Setup(x => x.IsAvailable).Returns(true);
            llmProviderMock.Setup(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LlmModerationResponse
                {
                    RequestId = "test",
                    Verdict = ModerationVerdict.Blocked,
                    Confidence = 0.3, // Below threshold
                    Reasoning = "Low confidence"
                });

            var provider = new CompositeModerationProvider(
                optionsMock.Object,
                loggerMock.Object,
                llmProvider: llmProviderMock.Object);

            var fileMetadata = new LocalFileMetadata(
                id: "test.mp3",
                sizeBytes: 1024,
                primaryHash: "testhash",
                mediaInfo: "audio/mp3");

            // Act
            var result = await provider.CheckLocalFileAsync(fileMetadata);

            // Assert - Low confidence LLM result is ignored
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            llmProviderMock.Verify(x => x.ModerateAsync(It.IsAny<LlmModerationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        // Mock HTTP message handler for testing
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Dictionary<Func<HttpRequestMessage, bool>, (HttpStatusCode StatusCode, string Content)> _responses = new();

            public void SetupResponse(Func<HttpRequestMessage, bool> predicate, HttpStatusCode statusCode, string content)
            {
                _responses[predicate] = (statusCode, content);
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                foreach (var (predicate, (statusCode, content)) in _responses)
                {
                    if (predicate(request))
                    {
                        return new HttpResponseMessage(statusCode)
                        {
                            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
                        };
                    }
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }
    }
}


