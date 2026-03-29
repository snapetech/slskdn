namespace slskd.Tests.Unit.Transfers.Downloads;

using System;
using System.Reflection;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Events;
using slskd.Files;
using slskd.Integrations.FTP;
using slskd.Relay;
using slskd.Transfers;
using slskd.Transfers.Downloads;
using Soulseek;
using Xunit;

public class DownloadServiceTests
{
    [Fact]
    public void Dispose_UnsubscribesClockMinuteHandler()
    {
        var optionsMonitor = new TestOptionsMonitor<slskd.Options>(new slskd.Options());
        var clockEveryMinuteListenersBefore = GetStaticEventInvocationCount(typeof(Clock), "EveryMinute");
        var service = new DownloadService(
            optionsMonitor,
            Mock.Of<ISoulseekClient>(),
            Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<TransfersDbContext>>(),
            new FileService(optionsMonitor),
            Mock.Of<IRelayService>(),
            Mock.Of<IFTPService>(),
            new EventBus(new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>())));

        Assert.Equal(clockEveryMinuteListenersBefore + 1, GetStaticEventInvocationCount(typeof(Clock), "EveryMinute"));

        service.Dispose();

        Assert.Equal(clockEveryMinuteListenersBefore, GetStaticEventInvocationCount(typeof(Clock), "EveryMinute"));
    }

    private static int GetStaticEventInvocationCount(Type type, string eventName)
    {
        var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{type.FullName}.{eventName} backing field was not found.");

        return (field.GetValue(null) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }
}
