namespace slskd.Tests.Unit.Sharing;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.Sharing;
using Soulseek;
using Xunit;

public class ShareGrantAnnouncementServiceTests
{
    [Fact]
    public void Dispose_UnsubscribesSoulseekEvent()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        var service = new ShareGrantAnnouncementService(
            Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<CollectionsDbContext>>(),
            NullLogger<ShareGrantAnnouncementService>.Instance,
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            soulseekClient.Object);

        service.Dispose();

        soulseekClient.VerifyRemove(x => x.PrivateMessageReceived -= It.IsAny<EventHandler<PrivateMessageReceivedEventArgs>>(), Times.Once);
    }
}
