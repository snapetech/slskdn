// <copyright file="RoomsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Messaging;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Messaging;
using slskd.Messaging.API;
using Soulseek;
using Xunit;

public class RoomsControllerTests
{
    [Fact]
    public async Task SendMessage_Trims_Room_And_Message_Before_Dispatch()
    {
        var client = new Mock<ISoulseekClient>();
        var tracker = CreateTracker();
        tracker
            .Setup(x => x.TryGet("room-1", out It.Ref<Room?>.IsAny))
            .Returns((string _, out Room? room) =>
            {
                room = null;
                return true;
            });
        var controller = CreateController(client: client.Object, tracker: tracker.Object);

        var result = await controller.SendMessage(" room-1 ", " hello ");

        Assert.IsType<StatusCodeResult>(result);
        client.Verify(x => x.SendRoomMessageAsync("room-1", "hello", null), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_With_Blank_Name_Returns_BadRequest()
    {
        var controller = CreateController();

        var result = await controller.JoinRoom("   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetByRoomName_With_Blank_Name_Returns_BadRequest()
    {
        var controller = CreateController();

        var result = controller.GetByRoomName("   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static RoomsController CreateController(
        ISoulseekClient? client = null,
        IRoomService? roomService = null,
        IRoomTracker? tracker = null)
    {
        var stateMonitor = new Mock<IStateMonitor<State>>();
        stateMonitor.Setup(x => x.CurrentValue).Returns(new State());

        var optionsSnapshot = new Mock<IOptionsSnapshot<slskd.Options>>();
        optionsSnapshot.Setup(x => x.Value).Returns(new slskd.Options());

        return new RoomsController(
            client ?? Mock.Of<ISoulseekClient>(),
            roomService ?? Mock.Of<IRoomService>(),
            stateMonitor.Object,
            optionsSnapshot.Object,
            tracker ?? CreateTracker().Object);
    }

    private static Mock<IRoomTracker> CreateTracker()
    {
        var tracker = new Mock<IRoomTracker>();
        var roomMap = new System.Collections.Concurrent.ConcurrentDictionary<string, Room>();
        tracker.SetupGet(x => x.Rooms).Returns(roomMap);
        tracker
            .Setup(x => x.TryGet(It.IsAny<string>(), out It.Ref<Room?>.IsAny))
            .Returns((string roomName, out Room? room) =>
            {
                var found = roomMap.TryGetValue(roomName, out var value);
                room = value;
                return found;
            });

        return tracker;
    }
}
