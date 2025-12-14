// <copyright file="ExternalModerationClientFactoryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Moderation
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Moq;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP-LM03: ExternalModerationClientFactory.
    /// </summary>
    public class ExternalModerationClientFactoryTests
    {
        private readonly Mock<IHttpClientFactory> _httpClientFactoryMock = new();
        private readonly Mock<IOptionsMonitor<ExternalModerationOptions>> _optionsMock = new();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
        private readonly Mock<ILogger<LocalExternalModerationClient>> _localLoggerMock = new();
        private readonly Mock<ILogger<RemoteExternalModerationClient>> _remoteLoggerMock = new();
        private readonly Mock<ILogger<ExternalModerationClientFactory.NoopExternalModerationClient>> _noopLoggerMock = new();

        public ExternalModerationClientFactoryTests()
        {
            _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

            _loggerFactoryMock
                .Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("Local"))))
                .Returns(_localLoggerMock.Object);

            _loggerFactoryMock
                .Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("Remote"))))
                .Returns(_remoteLoggerMock.Object);

            _loggerFactoryMock
                .Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("Noop"))))
                .Returns(_noopLoggerMock.Object);
        }

        private ExternalModerationClientFactory CreateFactory()
        {
            return new ExternalModerationClientFactory(
                _httpClientFactoryMock.Object,
                _optionsMock.Object,
                _loggerFactoryMock.Object);
        }

        [Theory]
        [InlineData("Off")]
        [InlineData("off")]
        [InlineData("OFF")]
        public void CreateClient_WithModeOff_ReturnsNoopClient(string mode)
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions { Mode = mode });
            var factory = CreateFactory();

            // Act
            var client = factory.CreateClient();

            // Assert
            Assert.IsType<ExternalModerationClientFactory.NoopExternalModerationClient>(client);

            // Verify HTTP client not created
            _httpClientFactoryMock.Verify(x => x.CreateClient(It.IsAny<string>()), Times.Never);
        }

        [Theory]
        [InlineData("Local")]
        [InlineData("local")]
        [InlineData("LOCAL")]
        public void CreateClient_WithModeLocal_ReturnsLocalClient(string mode)
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions { Mode = mode });
            var factory = CreateFactory();

            // Act
            var client = factory.CreateClient();

            // Assert
            Assert.IsType<LocalExternalModerationClient>(client);

            // Verify HTTP client created
            _httpClientFactoryMock.Verify(x => x.CreateClient("ExternalModeration"), Times.Once);
        }

        [Theory]
        [InlineData("Remote")]
        [InlineData("remote")]
        [InlineData("REMOTE")]
        public void CreateClient_WithModeRemote_ReturnsRemoteClient(string mode)
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions { Mode = mode });
            var factory = CreateFactory();

            // Act
            var client = factory.CreateClient();

            // Assert
            Assert.IsType<RemoteExternalModerationClient>(client);

            // Verify HTTP client created
            _httpClientFactoryMock.Verify(x => x.CreateClient("ExternalModeration"), Times.Once);
        }

        [Theory]
        [InlineData("Invalid")]
        [InlineData("")]
        [InlineData(null)]
        public void CreateClient_WithInvalidMode_ReturnsNoopClient(string mode)
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions { Mode = mode });
            var factory = CreateFactory();

            // Act
            var client = factory.CreateClient();

            // Assert
            Assert.IsType<ExternalModerationClientFactory.NoopExternalModerationClient>(client);

            // Verify warning logged
            _noopLoggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Invalid external moderation mode")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NoopClient_AnalyzeFileAsync_ReturnsUnknown()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions { Mode = "Off" });
            var factory = CreateFactory();
            var client = factory.CreateClient();
            var file = new LocalFileMetadata("test.mp3", 1024, "hash", "audio/mp3");

            // Act
            var result = await client.AnalyzeFileAsync(file);

            // Assert
            Assert.Equal(ModerationVerdict.Unknown, result.Verdict);
            Assert.Contains("external_moderation_disabled", result.Reason);
        }

        [Fact]
        public void CreateClient_CreatesNewInstances()
        {
            // Arrange
            _optionsMock.Setup(x => x.CurrentValue).Returns(new ExternalModerationOptions { Mode = "Local" });
            var factory = CreateFactory();

            // Act
            var client1 = factory.CreateClient();
            var client2 = factory.CreateClient();

            // Assert - Should create different instances (not singletons)
            Assert.NotSame(client1, client2);
            Assert.IsType<LocalExternalModerationClient>(client1);
            Assert.IsType<LocalExternalModerationClient>(client2);
        }
    }
}
