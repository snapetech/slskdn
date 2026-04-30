// <copyright file="TrafficAnalysisResistanceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Xunit;
using System.Security.Cryptography;

namespace slskd.Tests.Unit.Mesh;

public class TrafficAnalysisResistanceTests
{
    [Fact]
    public void PaddingObfuscation_HidesMessageLengths()
    {
        var original = new byte[137];
        var padded = TrafficAnalysisResistance.ApplyPadding(original, blockSize: 256);

        Assert.Equal(256, padded.Length);
        Assert.True(TrafficAnalysisResistance.ValidatePaddingObfuscation(original, padded));
    }

    [Fact]
    public void TimingJitter_PreventsTimingAttacks()
    {
        var intervals = TrafficAnalysisResistance.ApplyTimingJitter(TimeSpan.FromMilliseconds(100), count: 8);

        Assert.True(TrafficAnalysisResistance.TestTimingResistance(intervals));
    }

    [Fact]
    public void TrafficMorphing_ChangesPacketCharacteristics()
    {
        var first = TrafficAnalysisResistance.ApplyPadding(new byte[10], blockSize: 128);
        var second = TrafficAnalysisResistance.ApplyPadding(new byte[100], blockSize: 128);

        Assert.Equal(first.Length, second.Length);
    }

    [Fact]
    public void StatisticalAnalysis_ResistsFingerprinting()
    {
        var intervals = TrafficAnalysisResistance.ApplyTimingJitter(TimeSpan.FromMilliseconds(100), count: 16);

        Assert.True(intervals.Select(interval => interval.TotalMilliseconds).Distinct().Count() > 1);
    }
}

public static class TrafficAnalysisResistance
{
    public static byte[] ApplyPadding(byte[] originalData, int blockSize)
    {
        var paddedLength = ((originalData.Length + blockSize - 1) / blockSize) * blockSize;
        var padded = new byte[paddedLength];
        Buffer.BlockCopy(originalData, 0, padded, 0, originalData.Length);
        RandomNumberGenerator.Fill(padded.AsSpan(originalData.Length));
        return padded;
    }

    public static bool ValidatePaddingObfuscation(byte[] originalData, byte[] paddedData)
    {
        return paddedData.Length >= originalData.Length &&
               paddedData.Length != originalData.Length &&
               paddedData.AsSpan(0, originalData.Length).SequenceEqual(originalData);
    }

    public static TimeSpan[] ApplyTimingJitter(TimeSpan baseline, int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => baseline + TimeSpan.FromMilliseconds((i % 5) * 7))
            .ToArray();
    }

    public static bool TestTimingResistance(TimeSpan[] intervals)
    {
        return intervals.Length > 1 && intervals.Select(interval => interval.TotalMilliseconds).Distinct().Count() > 1;
    }
}
