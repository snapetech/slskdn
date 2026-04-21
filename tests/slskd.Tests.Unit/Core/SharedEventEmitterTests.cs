namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Soulseek;
using slskd.Tests.Unit;
using slskd.VirtualSoulfind.DisasterMode;
using Xunit;

[Collection(StaticEventCollection.Name)]
public class SharedEventEmitterTests
{
    [Fact]
    public void ProgramLogEmitted_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        var invokedHealthySubscriber = false;
        EventHandler<LogRecord> throwingHandler = (_, _) => throw new InvalidOperationException("boom");
        EventHandler<LogRecord> healthyHandler = (_, record) => invokedHealthySubscriber = record.Message == "test";

        Program.LogEmitted += throwingHandler;
        Program.LogEmitted += healthyHandler;

        try
        {
            var raiseMethod = typeof(Program).GetMethod(
                "RaiseLogEmitted",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Program.RaiseLogEmitted method was not found.");

            raiseMethod.Invoke(null, [new LogRecord
            {
                Context = "test",
                Message = "test",
                Timestamp = DateTime.UtcNow,
            }]);

            Assert.True(invokedHealthySubscriber);
        }
        finally
        {
            Program.LogEmitted -= throwingHandler;
            Program.LogEmitted -= healthyHandler;
        }
    }

    [Fact]
    public async Task DisasterModeCoordinator_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        var healthMonitor = new Mock<ISoulseekHealthMonitor>();
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.Setup(x => x.CurrentValue).Returns(new slskd.Options());

        var coordinator = new DisasterModeCoordinator(
            NullLogger<DisasterModeCoordinator>.Instance,
            healthMonitor.Object,
            optionsMonitor.Object);

        var invokedHealthySubscriber = false;
        coordinator.DisasterModeLevelChanged += (_, _) => throw new InvalidOperationException("boom");
        coordinator.DisasterModeLevelChanged += (_, args) => invokedHealthySubscriber = args.Level == DisasterModeLevel.SoulseekDegraded;

        await coordinator.SetDisasterModeLevelAsync(DisasterModeLevel.SoulseekDegraded, "test");

        Assert.True(invokedHealthySubscriber);
    }

    [Fact]
    public void SoulseekClientWrapper_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        var soulseekClient = new Mock<Soulseek.ISoulseekClient>();
        using var wrapper = new SoulseekClientWrapper(soulseekClient.Object);

        var invokedHealthySubscriber = false;
        EventHandler<RoomMessageReceivedEventArgs> throwingHandler = (_, _) => throw new InvalidOperationException("boom");
        EventHandler<RoomMessageReceivedEventArgs> healthyHandler = (_, _) => invokedHealthySubscriber = true;
        wrapper.RoomMessageReceived += throwingHandler;
        wrapper.RoomMessageReceived += healthyHandler;

        try
        {
            var raiseMethod = typeof(SoulseekClientWrapper).GetMethod(
                "RaiseRoomMessageReceived",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("SoulseekClientWrapper.RaiseRoomMessageReceived method was not found.");

            raiseMethod.Invoke(wrapper, [null, null]);

            Assert.True(invokedHealthySubscriber);
        }
        finally
        {
            wrapper.RoomMessageReceived -= throwingHandler;
            wrapper.RoomMessageReceived -= healthyHandler;
        }
    }

    [Fact]
    public void SoulseekHealthMonitor_WhenOneSubscriberThrows_ContinuesInvokingRemainingSubscribers()
    {
        var soulseekClient = new Mock<Soulseek.ISoulseekClient>();
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.Setup(x => x.CurrentValue).Returns(new slskd.Options());

        using var monitor = new SoulseekHealthMonitor(
            NullLogger<SoulseekHealthMonitor>.Instance,
            soulseekClient.Object,
            optionsMonitor.Object);

        var invokedHealthySubscriber = false;
        monitor.HealthChanged += (_, _) => throw new InvalidOperationException("boom");
        monitor.HealthChanged += (_, args) => invokedHealthySubscriber = args.NewHealth == SoulseekHealth.Degraded;

        var raiseMethod = typeof(SoulseekHealthMonitor).GetMethod(
            "RaiseHealthChanged",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SoulseekHealthMonitor.RaiseHealthChanged method was not found.");

        raiseMethod.Invoke(monitor, [new SoulseekHealthChangedEventArgs
        {
            OldHealth = SoulseekHealth.Healthy,
            NewHealth = SoulseekHealth.Degraded,
            Timestamp = DateTimeOffset.UtcNow,
            Reason = "test",
        }]);

        Assert.True(invokedHealthySubscriber);
    }
}
