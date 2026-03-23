using System;
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
                Controller = new Options.RelayOptions.RelayControllerOptions
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

    private sealed class TestRelayClient : IRelayClient, IDisposable
    {
        public IStateMonitor<RelayClientState> StateMonitor { get; } = new ManagedState<RelayClientState>();

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SynchronizeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
