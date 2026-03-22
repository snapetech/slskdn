using System.Net.Http;
using System.Reflection;
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
        var options = new Options();
        var optionsMonitor = new Mock<IOptionsMonitor<Options>>();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(options);
        optionsMonitor.Setup(x => x.OnChange(It.IsAny<Action<Options, string?>>())).Returns(Mock.Of<IDisposable>());

        var client = new RelayClient(
            Mock.Of<IShareService>(),
            new FileService(optionsMonitor.Object),
            optionsMonitor.Object,
            Mock.Of<IHttpClientFactory>());

        var cts = new CancellationTokenSource();
        var property = typeof(RelayClient).GetProperty("StartCancellationTokenSource", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(client, cts);

        await client.StopAsync();

        Assert.True(cts.IsCancellationRequested);
        Assert.Null(property.GetValue(client));
    }
}
