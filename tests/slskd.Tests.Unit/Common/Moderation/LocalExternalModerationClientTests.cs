// <copyright file="LocalExternalModerationClientTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Moq.Protected;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP-LM03: LocalExternalModerationClient.
    /// </summary>
    public class LocalExternalModerationClientTests : IDisposable
    {
        private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
        private readonly HttpClient _httpClient;
        private readonly Mock<IOptionsMonitor<ExternalModerationOptions>> _optionsMock = new();
        private readonly Mock<ILogger<LocalExternalModerationClient>> _loggerMock = new();

        public LocalExternalModerationClientTests()
        {
            _httpClient = new HttpClient(_httpHandlerMock.Object);

            // Setup default options
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Local",
                Endpoint = "http://localhost:8080/api",
                TimeoutSeconds = 5
            });
        }

        private LocalExternalModerationClient CreateClient()
        {
            return new LocalExternalModerationClient(_httpClient, _optionsMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithLocalhostEndpoint_ReturnsModerationDecision()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            var localResponse = new
            {
                verdict = "Blocked",
                confidence = 0.8,
                reasoning = "Local LLM detected inappropriate content",
                categories = new[] { "HateSpeech" },
                processingTimeMs = 150
            };

            SetupHttpResponse(localResponse, HttpStatusCode.OK);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Blocked, result.Verdict);
            Assert.Contains("local_llm:", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithLocalNetworkIp_Allowed()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Local",
                Endpoint = "http://192.168.1.100:8080/api"
            });

            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            var localResponse = new
            {
                verdict = "Allowed",
                confidence = 0.9,
                reasoning = "Clean content"
            };

            SetupHttpResponse(localResponse, HttpStatusCode.OK);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Allowed, result.Verdict);
            Assert.Contains("local_llm:", result.Reason);
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithRemoteDomain_ReturnsUnknown()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Local",
                Endpoint = "http://api.example.com/api", // Remote domain not allowed for local mode
                AllowedDomains = new[] { "api.example.com" } // Even if explicitly allowed
            });

            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert - Should fail validation
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
        public async Task AnalyzeFileAsync_WithHttpAllowedDomain_Allowed()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions
            {
                Mode = "Local",
                Endpoint = "http://my-local-llm.local/api",
                AllowedDomains = new[] { "my-local-llm.local" }
            });

            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            var localResponse = new
            {
                verdict = "Quarantined",
                confidence = 0.7,
                reasoning = "Suspicious content detected"
            };

            SetupHttpResponse(localResponse, HttpStatusCode.OK);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Quarantined, result.Verdict);
        }

        [Fact]
        public async Task AnalyzeFileAsync_IncludesMoreDataThanRemoteClient()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash123456789", "audio/mp3");

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
                        verdict = "Allowed",
                        confidence = 0.9,
                        reasoning = "Clean"
                    })
                });

            // Act
            await client.AnalyzeFileAsync(file);

            // Assert - Local client includes more detailed information
            Assert.NotNull(capturedRequest);
            var requestContent = await capturedRequest.Content!.ReadAsStringAsync();
            Assert.Contains("hash123456789", requestContent); // Full hash included
            Assert.Contains("audio/mp3", requestContent); // Media info included
        }

        [Fact]
        public async Task AnalyzeFileAsync_WithApiError_ReturnsUnknown()
        {
            // Arrange
            var client = CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            SetupHttpResponse(null, HttpStatusCode.ServiceUnavailable);

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("local_llm_error", result.Reason);
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
