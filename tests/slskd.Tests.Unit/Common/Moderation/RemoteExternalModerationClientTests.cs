// <copyright file="RemoteExternalModerationClientTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Moq.Protected;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP-LM03: RemoteExternalModerationClient.
    /// </summary>
    public class RemoteExternalModerationClientTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
        private readonly HttpClient _httpClient;
        private readonly Mock<IOptionsMonitor<ExternalModerationOptions>> _optionsMock = new();
        private readonly Mock<ILogger<RemoteExternalModerationClient>> _loggerMock = new();

        public RemoteExternalModerationClientTests()
        {
            _httpClient = new HttpClient(_httpHandlerMock.Object);

            // Setup default options
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Remote",
                Endpoint = "https://api.example.com/v1",
                AllowedDomains = new[] { "api.example.com" },
                TimeoutSeconds = 5
            });
        }

        private RemoteExternalModerationClient CreateClient()
        {
            return new RemoteExternalModerationClient(_httpClient, _optionsMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithValidResponse_ReturnsModerationDecision()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            var openAiResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                verdict = "Blocked",
                                confidence = 0.95,
                                reasoning = "Inappropriate content detected",
                                categories = new[] { "HateSpeech" }
                            })
                        }
                    }
                }
            };

            SetupHttpResponse(openAiResponse, HttpStatusCode.OK);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("llm:", result.Reason);
            Assert.Contains("HateSpeech", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithLowConfidence_ReturnsUnknown()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            var openAiResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                verdict = "Blocked",
                                confidence = 0.3, // Below threshold
                                reasoning = "Uncertain analysis"
                            })
                        }
                    }
                }
            };

            SetupHttpResponse(openAiResponse, HttpStatusCode.OK);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_confidence_too_low", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithSsrpProtectedDomain_ReturnsUnknown()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Remote",
                Endpoint = "https://evil.com/api", // Not in allowed domains
                AllowedDomains = new[] { "api.example.com" }
            });

            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert - Should fail validation before making HTTP call
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("endpoint_configuration_invalid", result.Reason);

            // Verify no HTTP call was made
            _httpHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithHttpEndpoint_ReturnsUnknown()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Remote",
                Endpoint = "http://api.example.com/api", // HTTP not allowed for remote
                AllowedDomains = new[] { "api.example.com" }
            });

            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("endpoint_configuration_invalid", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithApiError_ReturnsUnknown()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            SetupHttpResponse(null, HttpStatusCode.InternalServerError);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_api_error", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithInvalidJsonResponse_HandlesGracefully()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            var openAiResponse = new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = "Invalid JSON {{{"
                        }
                    }
                }
            };

            SetupHttpResponse(openAiResponse, HttpStatusCode.OK);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert - Should handle parsing error gracefully
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("llm_api_error", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WhenModeOff_ReturnsUnknown()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Off"
            });

            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("external_moderation_disabled", result.Reason);

            // Verify no HTTP call was made
            _httpHandlerMock.Protected().Verify(
                "SendAsync",
                Times.Never(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task AnalyzeFileAsync_SanitizesFilenameInRequest()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("/path/to/../../../sensitive.mp3", 1024, "hash", "audio/mp3");

            var capturedRequest = default(HttpRequestMessage);
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = JsonContent.Create(new
                    {
                        choices = new[]
                        {
                            new { message = new { content = "{\"verdict\":\"Allowed\",\"confidence\":0.9,\"reasoning\":\"Clean content\"}" } }
                        }
                    })
                });

            // Act
            await client.AnalyzeFileAsync(file);

            // Assert - Verify filename was sanitized in the request
            Assert.NotNull(capturedRequest);
            var requestContent = await capturedRequest.Content!.ReadAsStringAsync();
            Assert.Contains("sensitive.mp3", requestContent); // Filename sanitized
            Assert.DoesNotContain("/path/to/../../../", requestContent); // Path removed
        }

        private void SetupHttpResponse(object? responseContent, HttpStatusCode statusCode)
        {
            var response = new HttpResponseMessage
            {
                StatusCode = statusCode
            };

            if (responseContent != null)
            {
                response.Content = JsonContent.Create(responseContent);
            }

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
