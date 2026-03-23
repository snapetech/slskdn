// <copyright file="CapabilityFileServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Capabilities;

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
}
