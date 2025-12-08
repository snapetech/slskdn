using Microsoft.Extensions.Options;

namespace slskd.Tests.Unit.Files
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Files;
    using Xunit;

    public class FileServiceSecurityTests : IDisposable
    {
        public FileServiceSecurityTests()
        {
            OptionsMonitorMock = new Mock<IOptionsMonitor<Options>>();

            Temp = Path.Combine(Path.GetTempPath(), $"slskd.test.security.{Guid.NewGuid()}");
            Directory.CreateDirectory(Temp);

            // Create "downloads" and "downloads-secret" directories
            Downloads = Path.Combine(Temp, "downloads");
            DownloadsSecret = Path.Combine(Temp, "downloads-secret");
            
            Directory.CreateDirectory(Downloads);
            Directory.CreateDirectory(DownloadsSecret);

            FileService = new FileService(
                optionsMonitor: OptionsMonitorMock.Object);
        }

        public void Dispose()
        {
            if (Directory.Exists(Temp))
            {
                Directory.Delete(Temp, recursive: true);
            }
        }

        private Mock<IOptionsMonitor<Options>> OptionsMonitorMock { get; init; }
        private string Temp { get; init; }
        private string Downloads { get; init; }
        private string DownloadsSecret { get; init; }
        private FileService FileService { get; init; }

        [Fact]
        public async Task DeleteFilesAsync_Should_Prevent_Path_Prefix_Bypass()
        {
            // Arrange
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Downloads, // e.g. /tmp/slskd.test.../downloads
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            // Create a sensitive file in a directory that starts with the same prefix
            var sensitiveFile = Path.Combine(DownloadsSecret, "secret.txt");
            await File.WriteAllTextAsync(sensitiveFile, "top secret");

            // Act & Assert
            // This should throw UnauthorizedException because downloads-secret is NOT downloads
            var ex = await Record.ExceptionAsync(() => FileService.DeleteFilesAsync(sensitiveFile));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
            Assert.True(File.Exists(sensitiveFile), "File should not have been deleted");
        }
    }
}

