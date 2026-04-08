// <copyright file="SoulseekFileFactoryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Shares;

using System;
using System.IO;
using slskd.Files;
using slskd.Shares;
using Xunit;

public class SoulseekFileFactoryTests
{
    [Fact]
    public void Create_WithVideoFile_SkipsAttributeExtraction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"slskdn-video-{Guid.NewGuid():N}.mp4");

        try
        {
            File.WriteAllText(path, "not-a-real-video-file");

            var factory = new SoulseekFileFactory(new FileService(new TestOptionsMonitor<slskd.Options>(new slskd.Options())));

            var file = factory.Create(path, "share\\sample.mp4");

            Assert.Equal("mp4", file.Extension);
            Assert.Empty(file.Attributes ?? Array.Empty<Soulseek.FileAttribute>());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Create_WithAudioFile_StillAttemptsAttributeExtraction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"slskdn-audio-{Guid.NewGuid():N}.wav");

        try
        {
            WriteMinimalWav(path);

            var factory = new SoulseekFileFactory(new FileService(new TestOptionsMonitor<slskd.Options>(new slskd.Options())));

            var file = factory.Create(path, "share\\sample.wav");

            Assert.Equal("wav", file.Extension);
            Assert.NotNull(file.Attributes);
            Assert.NotEmpty(file.Attributes);
            Assert.Contains(file.Attributes, attribute => attribute.Type == Soulseek.FileAttributeType.Length);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static void WriteMinimalWav(string path)
    {
        const short channels = 1;
        const int sampleRate = 8000;
        const short bitsPerSample = 16;
        const short samples = 8;
        const short blockAlign = (short)(channels * (bitsPerSample / 8));
        const int byteRate = sampleRate * blockAlign;
        var dataSize = samples * blockAlign;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataSize);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(dataSize);

        for (var index = 0; index < samples; index++)
        {
            writer.Write((short)0);
        }
    }
}
