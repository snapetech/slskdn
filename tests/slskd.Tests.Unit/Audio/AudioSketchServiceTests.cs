// <copyright file="AudioSketchServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Audio
{
    using System;
    using System.IO;
    using slskd.Audio;
    using Xunit;

    public class AudioSketchServiceTests
    {
        [Fact]
        public void ResolveExecutablePath_WhenCommandIsOnPath_ReturnsResolvedFile()
        {
            var originalPath = Environment.GetEnvironmentVariable("PATH");
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"slskdn-audiosketch-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var executableName = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
                var executablePath = Path.Combine(tempDirectory, executableName);
                File.WriteAllText(executablePath, string.Empty);
                Environment.SetEnvironmentVariable("PATH", tempDirectory);

                var resolved = AudioSketchService.ResolveExecutablePath("ffmpeg");

                Assert.Equal(executablePath, resolved);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PATH", originalPath);
                Directory.Delete(tempDirectory, recursive: true);
            }
        }

        [Fact]
        public void ResolveExecutablePath_WhenExplicitPathMissing_ReturnsNull()
        {
            var missingPath = Path.Combine(Path.GetTempPath(), $"missing-ffmpeg-{Guid.NewGuid():N}");

            var resolved = AudioSketchService.ResolveExecutablePath(missingPath);

            Assert.Null(resolved);
        }
    }
}
