using Microsoft.Extensions.Options;

namespace slskd.Tests.Unit.Files
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Moq;
    using slskd.Files;
    using Xunit;

    public class FileServiceTests : IDisposable
    {
        public FileServiceTests()
        {
            OptionsMonitorMock = new Mock<IOptionsMonitor<Options>>();

            Temp = Path.Combine(Path.GetTempPath(), $"slskd.test.{Guid.NewGuid()}");
            Directory.CreateDirectory(Temp);

            FileService = new FileService(
                optionsMonitor: OptionsMonitorMock.Object);
        }

        public void Dispose()
        {
            Directory.Delete(Temp, recursive: true);
        }

        private Mock<IOptionsMonitor<Options>> OptionsMonitorMock { get; init; }
        private string Temp { get; init; }
        private FileService FileService { get; init; }

        [Fact]
        public async Task ListContentsAsync_Throws_ArgumentException_Given_Relative_Path()
        {
            var ex = await Record.ExceptionAsync(() => FileService.ListContentsAsync(directory: "../"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("directory", ((ArgumentException)ex).ParamName);
        }

        [Fact]
        public async Task ListContentsAsync_Throws_UnauthorizedException_Given_Disallowed_Directory()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.ListContentsAsync(directory: Path.Combine(Temp, "foo")));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
        }

        [Fact]
        public async Task ListContentsAsync_Throws_NotFoundException_Given_NonExistent_Directory()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.ListContentsAsync(directory: Path.Combine(Temp, "downloads", "foo")));

            Assert.NotNull(ex);
            Assert.IsType<NotFoundException>(ex);
        }

        [Fact]
        public async Task DeleteDirectoriesAsync_Throws_ArgumentException_Given_Relative_Path()
        {
            var ex = await Record.ExceptionAsync(() => FileService.DeleteDirectoriesAsync("../foo"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("directories", ((ArgumentException)ex).ParamName);
        }

        [Fact]
        public async Task DeleteDirectoriesAsync_Throws_ArgumentException_Given_Disallowed_Path()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.DeleteDirectoriesAsync(Path.Combine(Temp, "foo")));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
        }


        [Fact]
        public void CreateFile_Does_Not_Throw_When_Permission_Mode_Is_Unset()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                },
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = string.Empty,
                    },
                },
            });

            var filename = Path.Combine(Temp, "incomplete", "test.bin");

            using var stream = FileService.CreateFile(filename);

            Assert.True(File.Exists(filename));
        }

        [Fact]
        public void MoveFile_Does_Not_Throw_When_Permission_Mode_Is_Unset()
        {
            var downloads = Path.Combine(Temp, "downloads");
            var incomplete = Path.Combine(Temp, "incomplete");

            Directory.CreateDirectory(downloads);
            Directory.CreateDirectory(incomplete);

            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = downloads,
                    Incomplete = incomplete,
                },
                Permissions = new Options.PermissionsOptions
                {
                    File = new Options.PermissionsOptions.FileOptions
                    {
                        Mode = string.Empty,
                    },
                },
            });

            var sourceFilename = Path.Combine(incomplete, "move-me.bin");
            File.WriteAllText(sourceFilename, "test");

            var movedFilename = FileService.MoveFile(sourceFilename, downloads);

            Assert.Equal(Path.Combine(downloads, "move-me.bin"), movedFilename);
            Assert.True(File.Exists(movedFilename));
            Assert.False(File.Exists(sourceFilename));
        }

        [Fact]
        public async Task DeleteFilesAsync_Throws_ArgumentException_Given_Relative_Path()
        {
            var ex = await Record.ExceptionAsync(() => FileService.DeleteFilesAsync("../foo.bar"));

            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Equal("files", ((ArgumentException)ex).ParamName);
        }

        [Fact]
        public async Task DeleteFilesAsync_Throws_ArgumentException_Given_Disallowed_Path()
        {
            OptionsMonitorMock.Setup(o => o.CurrentValue).Returns(new Options
            {
                Directories = new Options.DirectoriesOptions
                {
                    Downloads = Path.Combine(Temp, "downloads"),
                    Incomplete = Path.Combine(Temp, "incomplete"),
                }
            });

            var ex = await Record.ExceptionAsync(() => FileService.DeleteFilesAsync(Path.Combine(Temp, "foo")));

            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedException>(ex);
        }
    }
}

