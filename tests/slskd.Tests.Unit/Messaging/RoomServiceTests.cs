// <copyright file="RoomServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Messaging;

using Microsoft.Extensions.Options;
using Moq;
using slskd.Events;
using slskd.Messaging;
using slskd.Users;
using Soulseek;
using Xunit;

public class RoomServiceTests
{
    [Fact]
    public void Dispose_UnsubscribesSoulseekEvents()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        var service = new RoomService(
            soulseekClient.Object,
            Mock.Of<IOptionsMonitor<slskd.Options>>(),
            Mock.Of<IStateMutator<slskd.State>>(),
            Mock.Of<IRoomTracker>(),
            Mock.Of<IUserService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        service.Dispose();

        soulseekClient.VerifyRemove(x => x.LoggedIn -= It.IsAny<EventHandler>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.RoomJoined -= It.IsAny<EventHandler<RoomJoinedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.RoomLeft -= It.IsAny<EventHandler<RoomLeftEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.RoomMessageReceived -= It.IsAny<EventHandler<RoomMessageReceivedEventArgs>>(), Times.Once);
    }
}
