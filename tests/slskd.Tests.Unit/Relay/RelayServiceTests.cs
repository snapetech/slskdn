using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Files;
using slskd.Relay;
using slskd.Shares;
using Xunit;

namespace slskd.Tests.Unit.Relay;

public class RelayServiceTests
{
    [Fact]
    public void Configure_WhenOnlyTopLevelRelayOptionsChange_ReconfiguresInsteadOfReturningEarly()
    {
        var initialOptions = CreateOptions(enabled: false, RelayMode.Controller);
        var optionsMonitor = CreateOptionsMonitor(initialOptions);
        var previousClient = new TestRelayClient();

        var service = CreateService(optionsMonitor, previousClient);

        InvokeConfigure(service, CreateOptions(enabled: true, RelayMode.Controller));

        Assert.Equal(RelayMode.Controller, service.StateMonitor.CurrentValue.Mode);
        Assert.IsType<NullRelayClient>(service.Client);
        Assert.True(previousClient.Disposed);
    }

    [Fact]
    public void Configure_WhenReplacingAgentClient_DisposesPreviousClient()
    {
        var initialOptions = CreateOptions(enabled: false, RelayMode.Agent);
        var optionsMonitor = CreateOptionsMonitor(initialOptions);
        var previousClient = new TestRelayClient();

        var service = CreateService(optionsMonitor, previousClient);

        InvokeConfigure(service, CreateOptions(enabled: true, RelayMode.Agent));

        Assert.IsType<RelayClient>(service.Client);
        Assert.True(previousClient.Disposed);
    }

    [Fact]
    public void ReplaceClient_DisposesPreviousStateMonitorSubscription()
    {
        var optionsMonitor = CreateOptionsMonitor(CreateOptions(enabled: false, RelayMode.Controller));
        var service = CreateService(optionsMonitor, new NullRelayClient());
        var previousClient = new TestRelayClient();
        var nextClient = new TestRelayClient();

        InvokeReplaceClient(service, previousClient);
        InvokeAttachClientStateMonitor(service, previousClient);
        previousClient.SetState(RelayClientState.Connected);

        Assert.Equal(RelayClientState.Connected, service.StateMonitor.CurrentValue.Controller.State);

        InvokeReplaceClient(service, nextClient);
        InvokeAttachClientStateMonitor(service, nextClient);

        previousClient.SetState(RelayClientState.Disconnected);

        Assert.Equal(RelayClientState.Connected, service.StateMonitor.CurrentValue.Controller.State);

        nextClient.SetState(RelayClientState.Reconnecting);

        Assert.Equal(RelayClientState.Reconnecting, service.StateMonitor.CurrentValue.Controller.State);
    }

    private static RelayService CreateService(IOptionsMonitor<Options> optionsMonitor, IRelayClient relayClient)
    {
        return new RelayService(
            Mock.Of<IWaiter>(),
            new FileService(optionsMonitor),
            Mock.Of<IShareService>(),
            Mock.Of<IShareRepositoryFactory>(),
            optionsMonitor,
            Mock.Of<IHubContext<RelayHub, IRelayHub>>(),
            Mock.Of<IHttpClientFactory>(),
            relayClient);
    }

    private static IOptionsMonitor<Options> CreateOptionsMonitor(Options options)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<Options>>();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(options);
        optionsMonitor.Setup(x => x.OnChange(It.IsAny<Action<Options, string?>>())).Returns(Mock.Of<IDisposable>());
        return optionsMonitor.Object;
    }

    private static Options CreateOptions(bool enabled, RelayMode mode)
    {
        return new Options
        {
            Relay = new Options.RelayOptions
            {
                Enabled = enabled,
                Mode = mode.ToString(),
                Controller = new Options.RelayOptions.RelayControllerConfigurationOptions
                {
                    Address = "https://relay.example",
                    ApiKey = "api-key",
                    Secret = "shared-secret",
                },
            },
        };
    }

    private static void InvokeConfigure(RelayService service, Options options)
    {
        var method = typeof(RelayService).GetMethod("Configure", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, [options]);
    }

    private static void InvokeAttachClientStateMonitor(RelayService service, IRelayClient client)
    {
        var method = typeof(RelayService).GetMethod("AttachClientStateMonitor", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, [client]);
    }

    private static void InvokeReplaceClient(RelayService service, IRelayClient client)
    {
        var method = typeof(RelayService).GetMethod("ReplaceClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(service, [client]);
    }

    private sealed class TestRelayClient : IRelayClient, IDisposable
    {
        private ManagedState<RelayClientState> State { get; } = new();

        public IStateMonitor<RelayClientState> StateMonitor => State;

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }

        public void SetState(RelayClientState state)
        {
            State.SetValue(_ => state);
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SynchronizeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
