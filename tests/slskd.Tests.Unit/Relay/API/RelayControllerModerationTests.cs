// <copyright file="RelayControllerModerationTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Relay.API
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;
    using Moq;
    using slskd;
    using slskd.Relay;
    using slskd.Shares;
    using Xunit;

    /// <summary>
    ///     Tests for T-MCP03: Moderation filtering in RelayController.
    /// </summary>
    public class RelayControllerModerationTests
    {
        private readonly Mock<IRelayService> _relayServiceMock = new();
        private readonly Mock<IShareRepository> _shareRepositoryMock = new();
        private readonly Mock<IOptionsMonitor<slskd.Options>> _optionsMonitorMock = new();

        private static readonly OptionsAtStartup _optionsAtStartup = new()
        {
            Relay = new slskd.Options.RelayOptions { Enabled = true, Mode = RelayMode.Controller.ToString().ToLowerInvariant() }
        };

        private RelayController CreateController()
        {
            _relayServiceMock
                .Setup(x => x.RegisteredAgents)
                .Returns(new System.Collections.ObjectModel.ReadOnlyCollection<Agent>(new List<Agent> { new() { Name = "test-agent", IPAddress = "127.0.0.1" } }));

            return new RelayController(
                _relayServiceMock.Object,
                _shareRepositoryMock.Object,
                _optionsMonitorMock.Object,
                _optionsAtStartup);
        }

        [Fact]
        public async Task DownloadFile_WithAdvertisableContent_AllowsDownload()
        {
            // Arrange
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();
            var tempDir = Path.Combine(Path.GetTempPath(), "RelayModTest_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);
            var testFile = Path.Combine(tempDir, "test.mp3");
            await File.WriteAllBytesAsync(testFile, new byte[] { 0x00 });

            try
            {
                controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
                controller.HttpContext.Request.Headers["X-Relay-Agent"] = "test-agent";
                controller.HttpContext.Request.Headers["X-Relay-Credential"] = "test-credential";
                controller.HttpContext.Request.Headers["X-Relay-Filename-Base64"] = "dGVzdC5tcDM="; // "test.mp3"

                _relayServiceMock
                    .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), "test-agent", "test.mp3", "test-credential"))
                    .Returns(true);

                _shareRepositoryMock
                    .Setup(x => x.ListContentItemsForFile("test.mp3"))
                    .Returns(new[] { ("c1", "Music", "w1", true, "ok") });

                _optionsMonitorMock
                    .Setup(x => x.CurrentValue)
                    .Returns(new slskd.Options { Directories = new slskd.Options.DirectoriesOptions { Downloads = tempDir } });

                // Act
                var result = await controller.DownloadFile(token);

                // Assert
                var fileResult = Assert.IsType<FileStreamResult>(result);
                Assert.Equal("application/octet-stream", fileResult.ContentType);
            }
            finally
            {
                try { File.Delete(testFile); } catch { }
                try { Directory.Delete(tempDir); } catch { }
            }
        }

        [Fact]
        public async Task DownloadFile_WithNonAdvertisableContent_ReturnsUnauthorized()
        {
            // Arrange
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            controller.HttpContext.Request.Headers["X-Relay-Agent"] = "test-agent";
            controller.HttpContext.Request.Headers["X-Relay-Credential"] = "test-credential";
            controller.HttpContext.Request.Headers["X-Relay-Filename-Base64"] = "dGVzdC5tcDM=";

            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), "test-agent", "test.mp3", "test-credential"))
                .Returns(true);

            _shareRepositoryMock
                .Setup(x => x.ListContentItemsForFile("test.mp3"))
                .Returns(new[] { ("c1", "Music", "w1", false, "Blocked by MCP") });

            _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(new slskd.Options { Directories = new slskd.Options.DirectoriesOptions { Downloads = "/tmp" } });

            // Act
            var result = await controller.DownloadFile(token);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task DownloadFile_WithNoAdvertisableContent_ReturnsUnauthorized()
        {
            // Arrange: ListContentItemsForFile returns items but none advertisable
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            controller.HttpContext.Request.Headers["X-Relay-Agent"] = "test-agent";
            controller.HttpContext.Request.Headers["X-Relay-Credential"] = "test-credential";
            controller.HttpContext.Request.Headers["X-Relay-Filename-Base64"] = "dGVzdC5tcDM=";

            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), "test-agent", "test.mp3", "test-credential"))
                .Returns(true);

            _shareRepositoryMock
                .Setup(x => x.ListContentItemsForFile("test.mp3"))
                .Returns(new[] { ("c1", "Music", "w1", false, "n/a") });

            _optionsMonitorMock.Setup(x => x.CurrentValue).Returns(new slskd.Options { Directories = new slskd.Options.DirectoriesOptions { Downloads = "/tmp" } });

            // Act
            var result = await controller.DownloadFile(token);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task DownloadFile_InvalidAuthentication_ReturnsUnauthorized()
        {
            // Arrange: no agent/credential headers -> Unauthorized before TryValidate
            var controller = CreateController();
            var token = Guid.NewGuid().ToString();

            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

            _relayServiceMock
                .Setup(x => x.TryValidateFileDownloadCredential(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            // Act
            var result = await controller.DownloadFile(token);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}
