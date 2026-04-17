using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using slskd.Core.API;
using slskd.Events;
using slskd.Files;
using slskd.Integrations.Notifications;
using slskd.Integrations.VPN;
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
    public void CreateStartupSoulseekClientOptionsPatch_ConfiguresIncomingConnectionOptions()
    {
        var patch = Application.CreateStartupSoulseekClientOptionsPatch(
            new OptionsAtStartup(),
            static (_, _) => { },
            static (_, _) => Task.FromResult<UserInfo>(null!),
            static (_, _) => Task.FromResult<BrowseResponse>(null!),
            static (_, _, _, _) => Task.FromResult<IEnumerable<Soulseek.Directory>>(Array.Empty<Soulseek.Directory>()),
            static (_, _, _) => Task.CompletedTask,
            static (_, _, _) => Task.FromResult<SearchResponse?>(null),
            static (_, _, _) => Task.FromResult<int?>(null));

        Assert.NotNull(patch.PeerConnectionOptions);
        Assert.NotNull(patch.TransferConnectionOptions);
        Assert.NotNull(patch.IncomingConnectionOptions);
        Assert.Equal(patch.PeerConnectionOptions.ReadBufferSize, patch.IncomingConnectionOptions.ReadBufferSize);
        Assert.Equal(patch.PeerConnectionOptions.WriteBufferSize, patch.IncomingConnectionOptions.WriteBufferSize);
        Assert.Equal(patch.PeerConnectionOptions.InactivityTimeout, patch.IncomingConnectionOptions.InactivityTimeout);
    }

    [Fact]
    public void CreateStartupSoulseekClientOptionsPatch_DoesNotReapplyListenerSettings()
    {
        var patch = Application.CreateStartupSoulseekClientOptionsPatch(
            new OptionsAtStartup(),
            static (_, _) => { },
            static (_, _) => Task.FromResult<UserInfo>(null!),
            static (_, _) => Task.FromResult<BrowseResponse>(null!),
            static (_, _, _, _) => Task.FromResult<IEnumerable<Soulseek.Directory>>(Array.Empty<Soulseek.Directory>()),
            static (_, _, _) => Task.CompletedTask,
            static (_, _, _) => Task.FromResult<SearchResponse?>(null),
            static (_, _, _) => Task.FromResult<int?>(null));

        Assert.Null(patch.EnableListener);
        Assert.Null(patch.ListenIPAddress);
        Assert.Null(patch.ListenPort);
    }

    [Fact]
    public void Dispose_DetachesManagedStateSubscriptions()
    {
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var applicationState = new ManagedState<State>();
        var shareState = new ManagedState<ShareState>();
        var relayState = new ManagedState<RelayState>();

        var application = CreateApplication(optionsMonitor, applicationState, shareState, relayState, out var applicationHub, out _);
        application.Dispose();

        shareState.SetValue(_ => new ShareState { Ready = true, Files = 7, Directories = 3 });
        relayState.SetValue(_ => new RelayState { Mode = RelayMode.Agent });
        applicationState.SetValue(state => state with { PendingReconnect = true });

        Assert.False(applicationState.CurrentValue.Shares.Ready);
        Assert.Equal(0, applicationState.CurrentValue.Shares.Files);
        Assert.Equal(0, applicationState.CurrentValue.Shares.Directories);
        Assert.NotEqual(RelayMode.Agent, applicationState.CurrentValue.Relay.Mode);
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
            out _,
            out _);

        Assert.Equal(2, optionsMonitor.ListenerCount);

        application.Dispose();

        Assert.Equal(1, optionsMonitor.ListenerCount);
    }

    [Fact]
    public void Dispose_UnsubscribesGlobalAndSoulseekEvents()
    {
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var programLogEmittedListenersBefore = GetStaticEventInvocationCount(typeof(Program), "LogEmitted");
        var clockEveryMinuteListenersBefore = GetStaticEventInvocationCount(typeof(Clock), "EveryMinute");
        var application = CreateApplication(
            optionsMonitor,
            new ManagedState<State>(),
            new ManagedState<ShareState>(),
            new ManagedState<RelayState>(),
            out _,
            out var soulseekClient);

        Assert.Equal(programLogEmittedListenersBefore + 1, GetStaticEventInvocationCount(typeof(Program), "LogEmitted"));
        Assert.Equal(clockEveryMinuteListenersBefore + 1, GetStaticEventInvocationCount(typeof(Clock), "EveryMinute"));

        application.Dispose();

        Assert.Equal(programLogEmittedListenersBefore, GetStaticEventInvocationCount(typeof(Program), "LogEmitted"));
        Assert.Equal(clockEveryMinuteListenersBefore, GetStaticEventInvocationCount(typeof(Clock), "EveryMinute"));
        soulseekClient.VerifyRemove(x => x.DiagnosticGenerated -= It.IsAny<EventHandler<Soulseek.Diagnostics.DiagnosticEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.TransferStateChanged -= It.IsAny<EventHandler<TransferStateChangedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.TransferProgressUpdated -= It.IsAny<EventHandler<TransferProgressUpdatedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.BrowseProgressUpdated -= It.IsAny<EventHandler<BrowseProgressUpdatedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.UserStatusChanged -= It.IsAny<EventHandler<UserStatus>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.PrivateMessageReceived -= It.IsAny<EventHandler<PrivateMessageReceivedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.PrivateRoomMembershipAdded -= It.IsAny<EventHandler<string>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.PrivateRoomMembershipRemoved -= It.IsAny<EventHandler<string>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.PrivateRoomModerationAdded -= It.IsAny<EventHandler<string>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.PrivateRoomModerationRemoved -= It.IsAny<EventHandler<string>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.RoomMessageReceived -= It.IsAny<EventHandler<RoomMessageReceivedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.Disconnected -= It.IsAny<EventHandler<SoulseekClientDisconnectedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.Connected -= It.IsAny<EventHandler>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.LoggedIn -= It.IsAny<EventHandler>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.StateChanged -= It.IsAny<EventHandler<SoulseekClientStateChangedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.DownloadDenied -= It.IsAny<EventHandler<DownloadDeniedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.DownloadFailed -= It.IsAny<EventHandler<DownloadFailedEventArgs>>(), Times.Once);
        soulseekClient.VerifyRemove(x => x.ExcludedSearchPhrasesReceived -= It.IsAny<EventHandler<IReadOnlyCollection<string>>>(), Times.Once);
    }

    private static Application CreateApplication(
        OptionsAtStartup optionsAtStartup,
        TestOptionsMonitor<Options> optionsMonitor,
        ManagedState<State> applicationState,
        ManagedState<ShareState> shareState,
        ManagedState<RelayState> relayState,
        out Mock<IClientProxy> applicationHub,
        out Mock<ISoulseekClient> soulseekClient)
    {
        applicationHub = new Mock<IClientProxy>();
        soulseekClient = new Mock<ISoulseekClient>();
        var vpnService = new VPNService(
            optionsAtStartup,
            optionsMonitor,
            Mock.Of<IStateMutator<State>>(),
            soulseekClient.Object,
            Mock.Of<IHttpClientFactory>());
        var connectionWatchdog = new ConnectionWatchdog(
            soulseekClient.Object,
            vpnService,
            optionsMonitor,
            optionsAtStartup,
            applicationState);
        var relayService = new Mock<IRelayService>();
        relayService.SetupGet(x => x.StateMonitor).Returns(relayState);
        relayService.SetupGet(x => x.Client).Returns(new NullRelayClient());

        var shareService = new Mock<IShareService>();
        shareService.SetupGet(x => x.StateMonitor).Returns(shareState);
        shareService.Setup(service => service.InitializeAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

        var uploadService = new Mock<slskd.Transfers.Uploads.IUploadService>();
        uploadService
            .Setup(service => service.List(
                It.IsAny<System.Linq.Expressions.Expression<Func<slskd.Transfers.Transfer, bool>>>(),
                It.IsAny<bool>()))
            .Returns([]);

        var downloadService = new Mock<slskd.Transfers.Downloads.IDownloadService>();
        downloadService
            .Setup(service => service.List(
                It.IsAny<System.Linq.Expressions.Expression<Func<slskd.Transfers.Transfer, bool>>>(),
                It.IsAny<bool>()))
            .Returns([]);

        var transferService = new Mock<ITransferService>();
        transferService.SetupGet(service => service.Uploads).Returns(uploadService.Object);
        transferService.SetupGet(service => service.Downloads).Returns(downloadService.Object);

        var searchService = new Mock<ISearchService>();
        searchService
            .Setup(service => service.ListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<slskd.Search.Search, bool>>>(),
                It.IsAny<int>(),
                It.IsAny<int>()))
            .ReturnsAsync([]);

        var hubClients = new Mock<IHubClients>();
        hubClients.SetupGet(x => x.All).Returns(applicationHub.Object);

        var appHubContext = new Mock<IHubContext<ApplicationHub>>();
        appHubContext.SetupGet(x => x.Clients).Returns(hubClients.Object);
        var eventService = new EventService(Mock.Of<Microsoft.EntityFrameworkCore.IDbContextFactory<EventsDbContext>>());

        return new Application(
            optionsAtStartup,
            optionsMonitor,
            applicationState,
            soulseekClient.Object,
            new FileService(optionsMonitor),
            connectionWatchdog,
            transferService.Object,
            Mock.Of<IBrowseTracker>(),
            Mock.Of<IRoomService>(),
            Mock.Of<IUserService>(),
            Mock.Of<IMessagingService>(),
            shareService.Object,
            searchService.Object,
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

    private static Application CreateApplication(
        TestOptionsMonitor<Options> optionsMonitor,
        ManagedState<State> applicationState,
        ManagedState<ShareState> shareState,
        ManagedState<RelayState> relayState,
        out Mock<IClientProxy> applicationHub,
        out Mock<ISoulseekClient> soulseekClient)
    {
        return CreateApplication(
            new OptionsAtStartup(),
            optionsMonitor,
            applicationState,
            shareState,
            relayState,
            out applicationHub,
            out soulseekClient);
    }

    private static int GetStaticEventInvocationCount(Type type, string eventName)
    {
        var field = type.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{type.FullName}.{eventName} backing field was not found.");

        return (field.GetValue(null) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }
}
