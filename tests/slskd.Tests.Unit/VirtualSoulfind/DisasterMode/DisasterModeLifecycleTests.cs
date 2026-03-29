namespace slskd.Tests.Unit.VirtualSoulfind.DisasterMode;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.VirtualSoulfind.DisasterMode;
using Soulseek;
using Xunit;

public class DisasterModeLifecycleTests
{
    [Fact]
    public void SoulseekClientWrapper_Dispose_UnsubscribesRoomMessageProxy()
    {
        var soulseekClient = new Mock<Soulseek.ISoulseekClient>();
        var wrapper = new SoulseekClientWrapper(soulseekClient.Object);

        wrapper.Dispose();

        soulseekClient.VerifyRemove(x => x.RoomMessageReceived -= It.IsAny<EventHandler<RoomMessageReceivedEventArgs>>(), Times.Once);
    }

    [Fact]
    public void DisasterModeCoordinator_Dispose_UnsubscribesHealthMonitor()
    {
        var healthMonitor = new Mock<ISoulseekHealthMonitor>();
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.Setup(x => x.CurrentValue).Returns(new slskd.Options());
        var coordinator = new DisasterModeCoordinator(
            NullLogger<DisasterModeCoordinator>.Instance,
            healthMonitor.Object,
            optionsMonitor.Object);

        coordinator.Dispose();

        healthMonitor.VerifyRemove(x => x.HealthChanged -= It.IsAny<EventHandler<SoulseekHealthChangedEventArgs>>(), Times.Once);
    }

    [Fact]
    public void DisasterModeRecovery_Dispose_UnsubscribesHealthMonitor()
    {
        var healthMonitor = new Mock<ISoulseekHealthMonitor>();
        healthMonitor.SetupGet(x => x.CurrentHealth).Returns(SoulseekHealth.Healthy);
        var disasterMode = new Mock<IDisasterModeCoordinator>();
        disasterMode.SetupGet(x => x.CurrentLevel).Returns(DisasterModeLevel.SoulseekUnavailable);
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.Setup(x => x.CurrentValue).Returns(new slskd.Options());
        var recovery = new DisasterModeRecovery(
            NullLogger<DisasterModeRecovery>.Instance,
            healthMonitor.Object,
            disasterMode.Object,
            optionsMonitor.Object);

        recovery.Dispose();

        healthMonitor.VerifyRemove(x => x.HealthChanged -= It.IsAny<EventHandler<SoulseekHealthChangedEventArgs>>(), Times.Once);
    }
}
