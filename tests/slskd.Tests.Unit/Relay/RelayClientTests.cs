using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Files;
using slskd.Relay;
using slskd.Shares;
using Xunit;

namespace slskd.Tests.Unit.Relay;

public class RelayClientTests
{
    [Fact]
    public async Task StopAsync_CancelsStartRetryToken()
    {
        var client = CreateClient(new Options());

        var cts = new CancellationTokenSource();
        SetStartCancellationTokenSource(client, cts);

        await client.StopAsync();

        Assert.True(cts.IsCancellationRequested);
        Assert.Null(GetStartCancellationTokenSource(client));
    }

    [Fact]
    public async Task StartAsync_CancelsPreviousStartRetryTokenBeforeReplacingIt()
    {
        var options = new Options
        {
            Relay = new Options.RelayOptions
            {
                Mode = RelayMode.Agent.ToString().ToLowerInvariant(),
                Controller = new Options.RelayOptions.RelayControllerConfigurationOptions
                {
                    Address = "http://127.0.0.1:1",
                    ApiKey = "1234567890abcdef",
                    Secret = "1234567890abcdef",
                },
            },
        };

        var client = CreateClient(options);
        var previousCts = new CancellationTokenSource();
        SetStartCancellationTokenSource(client, previousCts);

        using var startTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var cancelReplacementTask = Task.Run(async () =>
        {
            while (!startTimeout.IsCancellationRequested)
            {
                var replacement = GetStartCancellationTokenSource(client);
                if (replacement != null && !ReferenceEquals(replacement, previousCts))
                {
                    replacement.Cancel();
                    return;
                }

                await Task.Delay(10, startTimeout.Token);
            }
        }, CancellationToken.None);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.StartAsync());
        await cancelReplacementTask;

        Assert.True(previousCts.IsCancellationRequested);
    }

    private static RelayClient CreateClient(Options options)
    {
        var optionsMonitor = new Mock<IOptionsMonitor<Options>>();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(options);
        optionsMonitor.Setup(x => x.OnChange(It.IsAny<Action<Options, string?>>())).Returns(Mock.Of<IDisposable>());

        return new RelayClient(
            Mock.Of<IShareService>(),
            new FileService(optionsMonitor.Object),
            optionsMonitor.Object,
            Mock.Of<IHttpClientFactory>());
    }

    private static CancellationTokenSource? GetStartCancellationTokenSource(RelayClient client)
    {
        var property = typeof(RelayClient).GetProperty("StartCancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return (CancellationTokenSource?)property!.GetValue(client);
    }

    private static void SetStartCancellationTokenSource(RelayClient client, CancellationTokenSource cts)
    {
        var property = typeof(RelayClient).GetProperty("StartCancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(client, cts);
    }
}
