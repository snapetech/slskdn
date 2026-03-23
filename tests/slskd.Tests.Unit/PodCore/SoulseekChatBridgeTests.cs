// <copyright file="SoulseekChatBridgeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.PodCore;
using Soulseek;
using Xunit;

public class SoulseekChatBridgeTests
{
    [Fact]
    public void RegisterIdentityMapping_NormalizesBidirectionalKeys()
    {
        var bridge = CreateBridge();

        bridge.RegisterIdentityMapping(" Alice ", " peer:mesh:remote ");

        Assert.Equal("peer:mesh:remote", InvokePrivateString(bridge, "MapSoulseekToPodPeerId", " alice "));
        Assert.Equal("Alice", InvokePrivateString(bridge, "MapPodToSoulseekUsername", " PEER:MESH:REMOTE "));
    }

    [Fact]
    public void MapPodToSoulseekUsername_BackCompatBridgePrefixTrimsAndCachesMapping()
    {
        var bridge = CreateBridge();

        var username = InvokePrivateString(bridge, "MapPodToSoulseekUsername", " bridge:Alice ");

        Assert.Equal("Alice", username);
        Assert.Equal("bridge:Alice", InvokePrivateString(bridge, "MapSoulseekToPodPeerId", " alice "));
    }

    private static SoulseekChatBridge CreateBridge()
    {
        return new SoulseekChatBridge(
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IRoomService>(),
            new Mock<ISoulseekClient>().Object,
            NullLogger<SoulseekChatBridge>.Instance);
    }

    private static string? InvokePrivateString(SoulseekChatBridge bridge, string methodName, string argument)
    {
        var method = typeof(SoulseekChatBridge).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string?)method!.Invoke(bridge, new object?[] { argument });
    }
}
