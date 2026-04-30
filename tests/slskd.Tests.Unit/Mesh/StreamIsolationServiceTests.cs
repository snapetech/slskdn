// <copyright file="StreamIsolationServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Xunit;
using System.Security.Cryptography;
using System.Text;

namespace slskd.Tests.Unit.Mesh;

public class StreamIsolationServiceTests
{
    [Fact]
    public void GenerateIsolationKey_CreatesUniqueKeys()
    {
        var first = StreamIsolationService.GenerateIsolationKey("circuit-a", "stream-1");
        var second = StreamIsolationService.GenerateIsolationKey("circuit-a", "stream-2");

        Assert.NotEqual(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void IsolateStreams_PreventsCorrelation()
    {
        var first = StreamIsolationService.GenerateIsolationKey("circuit-a", "stream-1");
        var second = StreamIsolationService.GenerateIsolationKey("circuit-b", "stream-1");

        Assert.True(StreamIsolationService.ValidateStreamIsolation(first, second));
    }

    [Fact]
    public void ValidateIsolation_PreventsFingerprinting()
    {
        var key = StreamIsolationService.GenerateIsolationKey("circuit-a", "stream-1");

        Assert.False(StreamIsolationService.ValidateStreamIsolation(key, key));
    }
}

public class StreamIsolationService
{
    public static string GenerateIsolationKey(string circuitId, string streamId)
    {
        var input = $"{circuitId.Trim()}:{streamId.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    public static bool ValidateStreamIsolation(string isolationKey1, string isolationKey2)
    {
        return !string.IsNullOrWhiteSpace(isolationKey1) &&
               !string.IsNullOrWhiteSpace(isolationKey2) &&
               !CryptographicOperations.FixedTimeEquals(
                   Encoding.UTF8.GetBytes(isolationKey1),
                   Encoding.UTF8.GetBytes(isolationKey2));
    }
}
