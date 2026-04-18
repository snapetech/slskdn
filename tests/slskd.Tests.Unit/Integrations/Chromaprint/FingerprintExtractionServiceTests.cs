// <copyright file="FingerprintExtractionServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Integrations.Chromaprint;

using System;
using System.IO;
using System.Threading.Tasks;
using slskd.Integrations.Chromaprint;
using Xunit;
using ChromaprintOptions = slskd.Options.IntegrationOptions.ChromaprintOptions;

public class FingerprintExtractionServiceTests
{
    [Fact]
    public void GetMaximumPcmBytes_ReturnsExpectedBound()
    {
        var options = new ChromaprintOptions
        {
            SampleRate = 44100,
            Channels = 2,
            DurationSeconds = 120,
        };

        var maxBytes = FingerprintExtractionService.GetMaximumPcmBytes(options);

        Assert.Equal(21168000, maxBytes);
    }

    [Fact]
    public async Task ReadBoundedPcmAsync_ReturnsBufferWithinLimit()
    {
        var expected = new byte[4096];
        new Random(1234).NextBytes(expected);
        await using var stream = new MemoryStream(expected);

        var actual = await FingerprintExtractionService.ReadBoundedPcmAsync(stream, expected.Length, default);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task ReadBoundedPcmAsync_ThrowsWhenStreamExceedsLimit()
    {
        await using var stream = new MemoryStream(new byte[FingerprintExtractionService.CopyBufferSize + 1]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FingerprintExtractionService.ReadBoundedPcmAsync(stream, FingerprintExtractionService.CopyBufferSize, default));

        Assert.Contains("more PCM output than expected", ex.Message, StringComparison.Ordinal);
    }
}
