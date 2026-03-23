namespace slskd.Tests.Unit.Transfers.Uploads;

using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.Events;
using slskd.Files;
using slskd.Relay;
using slskd.Shares;
using slskd.Transfers;
using slskd.Transfers.Uploads;
using slskd.Users;
using Soulseek;
using Xunit;

public class UploadServiceLifecycleTests
{
    [Fact]
    public void Dispose_DisposesOwnedGovernorAndQueue()
    {
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var userService = new Mock<IUserService>();
        userService.Setup(x => x.GetGroup(It.IsAny<string>())).Returns(Application.DefaultGroup);
        var eventService = new EventService(Mock.Of<IDbContextFactory<EventsDbContext>>());
        var service = new UploadService(
            new FileService(optionsMonitor),
            userService.Object,
            Mock.Of<ISoulseekClient>(),
            optionsMonitor,
            Mock.Of<IShareService>(),
            Mock.Of<IRelayService>(),
            Mock.Of<IDbContextFactory<TransfersDbContext>>(),
            new EventBus(eventService));

        Assert.Equal(2, optionsMonitor.ListenerCount);

        service.Dispose();

        Assert.Equal(0, optionsMonitor.ListenerCount);
    }
}
