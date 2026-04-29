// <copyright file="CapabilityFileServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Capabilities;

using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Capabilities;
using Soulseek;
using Xunit;

public sealed class CapabilityFileServiceTests
{
    [Fact]
    public void ParseCapabilityFile_AcceptsTrimmedIdentityWithoutFlags()
    {
        var service = new CapabilityFileService(
            Mock.Of<ILogger<CapabilityFileService>>(),
            Mock.Of<ICapabilityService>(),
            Mock.Of<ISoulseekClient>());

        var content = service.ParseCapabilityFile("""
            {
              "client": " slskdn ",
              "version": " 1.2.3 ",
              "protocolVersion": 1,
              "capabilities": 0,
              "features": []
            }
            """u8.ToArray());

        Assert.NotNull(content);
        Assert.Equal("slskdn", content!.Client);
        Assert.Equal("1.2.3", content.Version);
        Assert.Equal(PeerCapabilityFlags.None, content.Capabilities);
    }

    [Fact]
    public void ParseCapabilityFile_DerivesCapabilitiesFromFeatures()
    {
        var service = new CapabilityFileService(
            Mock.Of<ILogger<CapabilityFileService>>(),
            Mock.Of<ICapabilityService>(),
            Mock.Of<ISoulseekClient>());

        var content = service.ParseCapabilityFile("""
            {
              "client": "slskdn",
              "version": "1.2.3",
              "protocolVersion": 1,
              "capabilities": 0,
              "features": ["mesh_sync", "swarm_download", "mesh_sync"]
            }
            """u8.ToArray());

        Assert.NotNull(content);
        Assert.True(content!.Capabilities.HasFlag(PeerCapabilityFlags.SupportsMeshSync));
        Assert.True(content.Capabilities.HasFlag(PeerCapabilityFlags.SupportsSwarm));
    }

    [Fact]
    public void IsCapabilityFileRequest_TrimsAndNormalizesSeparators()
    {
        var service = new CapabilityFileService(
            Mock.Of<ILogger<CapabilityFileService>>(),
            Mock.Of<ICapabilityService>(),
            Mock.Of<ISoulseekClient>());

        Assert.True(service.IsCapabilityFileRequest("  @@slskdn/__caps__.json  "));
    }

    [Fact]
    public async Task RequestCapabilityFileAsync_UsesOriginalRemoteFilenameForDownload()
    {
        var client = new Mock<ISoulseekClient>();
        var capabilityJson = """
            {
              "client": "slskdn",
              "version": "1.2.3",
              "protocolVersion": 1,
              "capabilities": 0,
              "features": []
            }
            """;
        client
            .Setup(soulseekClient => soulseekClient.BrowseAsync("alice", It.IsAny<BrowseOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrowseResponse(new[]
            {
                new Directory("@@slskdn", new[]
                {
                    new File(1, "__caps__.json", capabilityJson.Length, "json"),
                }),
            }));

        string? capturedRemoteFilename = null;
        client
            .Setup(soulseekClient => soulseekClient.DownloadAsync(
                "alice",
                It.IsAny<string>(),
                It.IsAny<Func<Task<Stream>>>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<int?>(),
                It.IsAny<TransferOptions>(),
                It.IsAny<CancellationToken?>()))
            .Returns(async (string username, string remoteFilename, Func<Task<Stream>> outputStreamFactory, long size, long startOffset, int? token, TransferOptions options, CancellationToken? cancellationToken) =>
            {
                capturedRemoteFilename = remoteFilename;
                await using var stream = await outputStreamFactory();
                var bytes = Encoding.UTF8.GetBytes(capabilityJson);
                await stream.WriteAsync(bytes);
                return (Transfer)null!;
            });

        var service = new CapabilityFileService(
            Mock.Of<ILogger<CapabilityFileService>>(),
            Mock.Of<ICapabilityService>(),
            client.Object);

        var content = await service.RequestCapabilityFileAsync("alice");

        Assert.NotNull(content);
        Assert.Equal("@@slskdn\\__caps__.json", capturedRemoteFilename);
    }
}
