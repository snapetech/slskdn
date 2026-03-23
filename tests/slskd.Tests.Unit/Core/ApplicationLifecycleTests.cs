using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using slskd.Core.API;
using slskd.Events;
using slskd.Files;
using slskd.Integrations.Notifications;
using slskd.Messaging;
using slskd.Relay;
using slskd.Search;
using slskd.Shares;
using slskd.Transfers;
using slskd.Transfers.API;
using slskd.Users;
using Soulseek;
using Xunit;

namespace slskd.Tests.Unit.Core;

public class ApplicationLifecycleTests
{
    [Fact]
    public void Dispose_DetachesManagedStateSubscriptions()
    {
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var applicationState = new ManagedState<State>();
        var shareState = new ManagedState<ShareState>();
        var relayState = new ManagedState<RelayState>();

        var application = CreateApplication(optionsMonitor, applicationState, shareState, relayState, out var applicationHub);
        application.Dispose();

        shareState.SetValue(_ => new ShareState { Ready = true, Files = 7, Directories = 3 });
        relayState.SetValue(_ => new RelayState { Mode = RelayMode.Controller });
        applicationState.SetValue(state => state with { PendingReconnect = true });

        Assert.False(applicationState.CurrentValue.Shares.Ready);
        Assert.Equal(0, applicationState.CurrentValue.Shares.Files);
        Assert.Equal(0, applicationState.CurrentValue.Shares.Directories);
        Assert.NotEqual(RelayMode.Controller, applicationState.CurrentValue.Relay.Mode);
        applicationHub.Verify(
            hub => hub.SendCoreAsync(
                ApplicationHubMethods.State,
                It.IsAny<object?[]>(),
                default),
            Times.Never);
    }

    [Fact]
    public void Dispose_UnsubscribesOptionsMonitor()
    {
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var application = CreateApplication(
            optionsMonitor,
            new ManagedState<State>(),
            new ManagedState<ShareState>(),
            new ManagedState<RelayState>(),
            out _);

        Assert.Equal(1, optionsMonitor.ListenerCount);

        application.Dispose();

        Assert.Equal(0, optionsMonitor.ListenerCount);
    }

    private static Application CreateApplication(
        TestOptionsMonitor<Options> optionsMonitor,
        ManagedState<State> applicationState,
        ManagedState<ShareState> shareState,
        ManagedState<RelayState> relayState,
        out Mock<IClientProxy> applicationHub)
    {
        applicationHub = new Mock<IClientProxy>();
        var relayService = new Mock<IRelayService>();
        relayService.SetupGet(x => x.StateMonitor).Returns(relayState);
        relayService.SetupGet(x => x.Client).Returns(new NullRelayClient());

        var shareService = new Mock<IShareService>();
        shareService.SetupGet(x => x.StateMonitor).Returns(shareState);

        var hubClients = new Mock<IHubClients>();
        hubClients.SetupGet(x => x.All).Returns(applicationHub.Object);

        var appHubContext = new Mock<IHubContext<ApplicationHub>>();
        appHubContext.SetupGet(x => x.Clients).Returns(hubClients.Object);
        var eventService = new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>());

        return new Application(
            new OptionsAtStartup(),
            optionsMonitor,
            applicationState,
            Mock.Of<ISoulseekClient>(),
            new FileService(optionsMonitor),
            Mock.Of<ConnectionWatchdog>(),
            Mock.Of<ITransferService>(),
            Mock.Of<IBrowseTracker>(),
            Mock.Of<IRoomService>(),
            Mock.Of<IUserService>(),
            Mock.Of<IMessagingService>(),
            shareService.Object,
            Mock.Of<ISearchService>(),
            Mock.Of<INotificationService>(),
            relayService.Object,
            appHubContext.Object,
            Mock.Of<IHubContext<LogsHub>>(),
            Mock.Of<IHubContext<TransfersHub>>(),
            new EventBus(eventService),
            eventService,
            Mock.Of<IServiceProvider>(),
            Mock.Of<IServiceScopeFactory>(),
            Mock.Of<slskd.NowPlaying.NowPlayingService>());
    }
}
