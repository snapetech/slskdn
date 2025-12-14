// <copyright file="RelayControllerModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Relay.API
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd.Relay.API.Controllers;
    using slskd.Shares;
    using slskd;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP03: Moderation filtering in RelayController.
    /// </summary>
    public class RelayControllerModerationTests
    {
        private readonly Mock<IRelayService> _relayServiceMock = new();
        private readonly Mock<IShareRepository> _shareRepositoryMock = new();
        private readonly Mock<IOptionsMonitor<Options>> _optionsMonitorMock = new();
        private readonly Mock<IOptions<OptionsAtStartup>> _optionsAtStartupMock = new();

        private RelayController CreateController()
        {
            _optionsAtStartupMock
                .Setup(x => x.Value)
                .Returns(new OptionsAtStartup { Relay = new RelayOptions { Enabled = true, Mode = RelayMode.Controller } });

            return new RelayController(
                _relayServiceMock.Object,
                _shareRepositoryMock.Object,
                _optionsMonitorMock.Object,
                _optionsAtStartupMock.Object);
        }

        [Fact]
        public void DownloadFile_WithAdvertisableContent_AllowsDownload()
        {
            // Arrange
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            // Setup controller context
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Request.Headers["X-Relay-Agent"] = "test-agent";
            controller.HttpContext.Request.Headers["X-Relay-Credential"] = "test-credential";
            controller.HttpContext.Request.Headers["X-Relay-Filename-Base64"] = "dGVzdC5tcDM="; // "test.mp3" base64

            // Setup relay service validation
            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), "test-agent", "test.mp3", "test-credential"))
                .Returns(true);

            // Setup share repository to return advertisable content
            _shareRepositoryMock
                .Setup(x => x.FindContentItem("test.mp3", 8)) // filename.Length = 8
                .Returns(("test.mp3", "Music", "album-123", true, null)); // IsAdvertisable = true

            // Setup options
            _optionsMonitorMock
                .Setup(x => x.CurrentValue)
                .Returns(new Options { Directories = new DirectoriesOptions { Downloads = "/tmp" } });

            // Act
            var result = controller.DownloadFile(token);

            // Assert
            var fileResult = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("application/octet-stream", fileResult.ContentType);
        }

        [Fact]
        public void DownloadFile_WithNonAdvertisableContent_ReturnsUnauthorized()
        {
            // Arrange
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            // Setup controller context
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Request.Headers["X-Relay-Agent"] = "test-agent";
            controller.HttpContext.Request.Headers["X-Relay-Credential"] = "test-credential";
            controller.HttpContext.Request.Headers["X-Relay-Filename-Base64"] = "dGVzdC5tcDM="; // "test.mp3" base64

            // Setup relay service validation
            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), "test-agent", "test.mp3", "test-credential"))
                .Returns(true);

            // Setup share repository to return NON-advertisable content
            _shareRepositoryMock
                .Setup(x => x.FindContentItem("test.mp3", 8))
                .Returns(("test.mp3", "Music", "album-123", false, "Blocked by MCP")); // IsAdvertisable = false

            // Act
            var result = controller.DownloadFile(token);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public void DownloadFile_WithNoContentFound_ReturnsUnauthorized()
        {
            // Arrange
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            // Setup controller context
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Request.Headers["X-Relay-Agent"] = "test-agent";
            controller.HttpContext.Request.Headers["X-Relay-Credential"] = "test-credential";
            controller.HttpContext.Request.Headers["X-Relay-Filename-Base64"] = "dGVzdC5tcDM="; // "test.mp3" base64

            // Setup relay service validation
            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), "test-agent", "test.mp3", "test-credential"))
                .Returns(true);

            // Setup share repository to return no content
            _shareRepositoryMock
                .Setup(x => x.FindContentItem("test.mp3", 8))
                .Returns<(string, string, string, bool, string)?>(null); // No content found

            // Act
            var result = controller.DownloadFile(token);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public void DownloadFile_InvalidAuthentication_ReturnsUnauthorized()
        {
            // Arrange
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            // Setup relay service to fail validation
            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            // Act
            var result = controller.DownloadFile(token);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedResult>(result);
        }
    }
}

