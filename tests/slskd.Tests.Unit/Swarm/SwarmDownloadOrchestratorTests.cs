// <copyright file="SwarmDownloadOrchestratorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Swarm;

using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Swarm;
using slskd.Transfers.MultiSource.Scheduling;
using Soulseek;
using Xunit;

public class SwarmDownloadOrchestratorTests
{
    [Fact]
    public async Task DownloadChunkAsync_WhenPeerSourceIsMissing_ReturnsSanitizedError()
    {
        var orchestrator = CreateOrchestrator();
        var method = typeof(SwarmDownloadOrchestrator).GetMethod("DownloadChunkAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = (Task<ChunkResult>)method!.Invoke(
            orchestrator,
            new object[]
            {
                new SwarmJob("job-1", new SwarmFile("content-1", "hash-1", 1024), new List<SwarmSource>()),
                new ChunkInfo { Index = 0, StartOffset = 0, EndOffset = 512 },
                "peer-secret",
                Path.GetTempPath(),
                CancellationToken.None,
            })!;

        var result = await task;

        Assert.False(result.Success);
        Assert.Equal("Chunk source not found", result.Error);
        Assert.DoesNotContain("peer-secret", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadChunkAsync_WhenTransportIsUnsupported_ReturnsSanitizedError()
    {
        var orchestrator = CreateOrchestrator();
        var method = typeof(SwarmDownloadOrchestrator).GetMethod("DownloadChunkAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = (Task<ChunkResult>)method!.Invoke(
            orchestrator,
            new object[]
            {
                new SwarmJob(
                    "job-1",
                    new SwarmFile("content-1", "hash-1", 1024),
                    new List<SwarmSource> { new("peer-1", "secret-transport") }),
                new ChunkInfo { Index = 0, StartOffset = 0, EndOffset = 512 },
                "peer-1",
                Path.GetTempPath(),
                CancellationToken.None,
            })!;

        var result = await task;

        Assert.False(result.Success);
        Assert.Equal("Unsupported chunk transport", result.Error);
        Assert.DoesNotContain("secret-transport", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadChunkAsync_WhenMeshTransportIsRequested_ReturnsSanitizedError()
    {
        var orchestrator = CreateOrchestrator();
        var method = typeof(SwarmDownloadOrchestrator).GetMethod("DownloadChunkAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var task = (Task<ChunkResult>)method!.Invoke(
            orchestrator,
            new object[]
            {
                new SwarmJob(
                    "job-1",
                    new SwarmFile("content-1", "hash-1", 1024),
                    new List<SwarmSource> { new("peer-1", "mesh") }),
                new ChunkInfo { Index = 0, StartOffset = 0, EndOffset = 512 },
                "peer-1",
                Path.GetTempPath(),
                CancellationToken.None,
            })!;

        var result = await task;

        Assert.False(result.Success);
        Assert.Equal("Mesh transport chunk download is unavailable", result.Error);
        Assert.DoesNotContain("not yet implemented", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static SwarmDownloadOrchestrator CreateOrchestrator()
    {
        return new SwarmDownloadOrchestrator(
            NullLogger<SwarmDownloadOrchestrator>.Instance,
            Mock.Of<IVerificationEngine>(),
            Mock.Of<IChunkScheduler>(),
            Mock.Of<ISoulseekClient>());
    }
}
