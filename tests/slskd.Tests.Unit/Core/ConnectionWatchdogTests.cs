// <copyright file="ConnectionWatchdogTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core;

using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using slskd.Integrations.VPN;
using Soulseek;
using Xunit;

public class ConnectionWatchdogTests
{
    [Fact]
    public async Task Start_WhenReconnectRetriesReplaceCancellationTokenSource_DisposesPreviousTokenSource()
    {
        var soulseekClient = new Mock<ISoulseekClient>();
        soulseekClient
            .Setup(client => client.ConnectAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("connect failed"));

        var optionsAtStartup = new OptionsAtStartup();
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var watchdog = new ConnectionWatchdog(
            soulseekClient.Object,
            new VPNService(
                optionsAtStartup,
                optionsMonitor,
                Mock.Of<IStateMutator<State>>(),
                soulseekClient.Object,
                Mock.Of<IHttpClientFactory>()),
            optionsMonitor,
            optionsAtStartup,
            new ManagedState<State>());

        try
        {
            watchdog.Start();

            var firstCancellationTokenSource = await WaitForCancellationTokenSourceAsync(
                watchdog,
                candidate => candidate is not null);
            var secondCancellationTokenSource = await WaitForCancellationTokenSourceAsync(
                watchdog,
                candidate => candidate is not null && !ReferenceEquals(candidate, firstCancellationTokenSource));

            Assert.NotNull(firstCancellationTokenSource);
            Assert.NotNull(secondCancellationTokenSource);
            Assert.Throws<ObjectDisposedException>(() => _ = firstCancellationTokenSource.Token);
        }
        finally
        {
            watchdog.Stop(abortReconnect: true);
            watchdog.Dispose();
        }
    }

    [Fact]
    public void Dispose_UnsubscribesOptionsMonitor()
    {
        var optionsAtStartup = new OptionsAtStartup();
        var optionsMonitor = new TestOptionsMonitor<Options>(new Options());
        var watchdog = new ConnectionWatchdog(
            Mock.Of<ISoulseekClient>(),
            new VPNService(
                optionsAtStartup,
                optionsMonitor,
                Mock.Of<IStateMutator<State>>(),
                Mock.Of<ISoulseekClient>(),
                Mock.Of<IHttpClientFactory>()),
            optionsMonitor,
            optionsAtStartup,
            new ManagedState<State>());

        Assert.Equal(1, optionsMonitor.ListenerCount);

        watchdog.Dispose();

        Assert.Equal(0, optionsMonitor.ListenerCount);
    }

    private static async Task<CancellationTokenSource> WaitForCancellationTokenSourceAsync(
        ConnectionWatchdog watchdog,
        Func<CancellationTokenSource, bool> predicate)
    {
        var cancellationTokenSourceProperty = typeof(ConnectionWatchdog).GetProperty(
            "CancellationTokenSource",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ConnectionWatchdog.CancellationTokenSource property was not found.");

        for (var attempt = 0; attempt < 200; attempt++)
        {
            var value = (CancellationTokenSource?)cancellationTokenSourceProperty.GetValue(watchdog);

            if (value != null && predicate(value))
            {
                return value;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Timed out waiting for the watchdog reconnect cancellation token source.");
    }
}
