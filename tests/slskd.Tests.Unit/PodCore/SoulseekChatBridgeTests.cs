// <copyright file="SoulseekChatBridgeTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Messaging;
using slskd.PodCore;
using Soulseek;
using Xunit;

public class SoulseekChatBridgeTests
{
    [Fact]
    public void Dispose_UnsubscribesSoulseekRoomMessageEvent()
    {
        var (bridge, soulseekClient) = CreateFixture();

        bridge.Dispose();

        soulseekClient.VerifyRemove(x => x.RoomMessageReceived -= It.IsAny<EventHandler<RoomMessageReceivedEventArgs>>(), Times.Once);
    }

    [Fact]
    public void RegisterIdentityMapping_NormalizesBidirectionalKeys()
    {
        var (bridge, _) = CreateFixture();

        bridge.RegisterIdentityMapping(" Alice ", " peer:mesh:remote ");

        Assert.Equal("peer:mesh:remote", InvokePrivateString(bridge, "MapSoulseekToPodPeerId", " alice "));
        Assert.Equal("Alice", InvokePrivateString(bridge, "MapPodToSoulseekUsername", " PEER:MESH:REMOTE "));
    }

    [Fact]
    public void MapPodToSoulseekUsername_BackCompatBridgePrefixTrimsAndCachesMapping()
    {
        var (bridge, _) = CreateFixture();

        var username = InvokePrivateString(bridge, "MapPodToSoulseekUsername", " bridge:Alice ");

        Assert.Equal("Alice", username);
        Assert.Equal("bridge:Alice", InvokePrivateString(bridge, "MapSoulseekToPodPeerId", " alice "));
    }

    private static (SoulseekChatBridge Bridge, Mock<ISoulseekClient> SoulseekClient) CreateFixture()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        var bridge = new SoulseekChatBridge(
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<IRoomService>(),
            soulseekClient.Object,
            NullLogger<SoulseekChatBridge>.Instance);

        return (bridge, soulseekClient);
    }

    private static string? InvokePrivateString(SoulseekChatBridge bridge, string methodName, string argument)
    {
        var method = typeof(SoulseekChatBridge).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (string?)method!.Invoke(bridge, new object?[] { argument });
    }
}
