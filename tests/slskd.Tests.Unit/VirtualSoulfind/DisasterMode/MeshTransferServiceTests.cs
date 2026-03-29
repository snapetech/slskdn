// <copyright file="MeshTransferServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.VirtualSoulfind.DisasterMode;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.VirtualSoulfind.DisasterMode;
using slskd.VirtualSoulfind.ShadowIndex;
using Xunit;

public class MeshTransferServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly MeshTransferService _service;
    private readonly Mock<IScenePeerDiscovery> _scenePeerDiscovery = new();

    public MeshTransferServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "slskdn-mesh-transfer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _scenePeerDiscovery
            .Setup(d => d.DiscoverPeersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new System.Collections.Generic.List<string> { "peer-a" });

        _service = new MeshTransferService(
            NullLogger<MeshTransferService>.Instance,
            new global::slskd.Tests.Unit.TestOptionsMonitor<global::slskd.Options>(new global::slskd.Options
            {
                Directories = new global::slskd.Options.DirectoriesOptions
                {
                    Downloads = _tempRoot
                }
            }),
            Mock.Of<IShadowIndexQuery>(),
            _scenePeerDiscovery.Object);
    }

    [Fact]
    public async Task StartTransferAsync_WithoutExpectedHash_CompletesAndCreatesTargetFile()
    {
        var targetPath = Path.Combine(_tempRoot, "complete.bin");

        var transferId = await _service.StartTransferAsync(
            peerId: "peer-a",
            fileHash: string.Empty,
            fileSize: 1024,
            targetPath: targetPath,
            ct: CancellationToken.None);

        var status = await WaitForTerminalStatusAsync(transferId);

        Assert.NotNull(status);
        Assert.Equal(MeshTransferState.Completed, status!.State);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(1024, new FileInfo(targetPath).Length);
    }

    [Fact]
    public async Task CancelTransferAsync_CancelsRunningTransferInsteadOfMarkingFailed()
    {
        var targetPath = Path.Combine(_tempRoot, "cancel.bin");

        var transferId = await _service.StartTransferAsync(
            peerId: "peer-a",
            fileHash: string.Empty,
            fileSize: 10 * 1024 * 1024,
            targetPath: targetPath,
            ct: CancellationToken.None);

        await Task.Delay(100);
        await _service.CancelTransferAsync(transferId, CancellationToken.None);

        var status = await WaitForTerminalStatusAsync(transferId);

        Assert.NotNull(status);
        Assert.Equal(MeshTransferState.Cancelled, status!.State);
    }

    [Fact]
    public async Task StartTransferAsync_WhenPeerDiscoveryFails_ReturnsSanitizedFailure()
    {
        _scenePeerDiscovery
            .Setup(d => d.DiscoverPeersAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var transferId = await _service.StartTransferAsync(
            peerId: "peer-a",
            fileHash: string.Empty,
            fileSize: 1024,
            targetPath: Path.Combine(_tempRoot, "failure.bin"),
            ct: CancellationToken.None);

        var status = await WaitForTerminalStatusAsync(transferId);

        Assert.NotNull(status);
        Assert.Equal(MeshTransferState.Failed, status!.State);
        Assert.Equal("Mesh transfer failed", status.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", status.ErrorMessage ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private async Task<MeshTransferStatus?> WaitForTerminalStatusAsync(string transferId)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = await _service.GetTransferStatusAsync(transferId, CancellationToken.None);
            if (status is { State: MeshTransferState.Completed or MeshTransferState.Failed or MeshTransferState.Cancelled })
            {
                return status;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException($"Transfer {transferId} did not reach a terminal state.");
    }
}
