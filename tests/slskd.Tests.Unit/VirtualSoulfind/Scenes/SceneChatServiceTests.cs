namespace slskd.Tests.Unit.VirtualSoulfind.Scenes;

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.VirtualSoulfind.Scenes;
using Xunit;

public class SceneChatServiceTests
{
    [Fact]
    public void Dispose_UnsubscribesPubSubMessages()
    {
        var pubsub = new Mock<IScenePubSubService>();
        var service = new SceneChatService(
            NullLogger<SceneChatService>.Instance,
            pubsub.Object,
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            Mock.Of<slskd.Identity.IProfileService>());

        service.Dispose();

        pubsub.VerifyRemove(x => x.MessageReceived -= It.IsAny<EventHandler<SceneMessageReceivedEventArgs>>(), Times.Once);
    }
}
