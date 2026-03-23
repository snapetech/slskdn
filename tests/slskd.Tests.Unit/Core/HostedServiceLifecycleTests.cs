namespace slskd.Tests.Unit.Core;

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.DhtRendezvous;
using slskd.HashDb.Optimization;
using slskd.Mesh.Realm;
using Xunit;

public class HostedServiceLifecycleTests
{
    [Fact]
    public async Task HashDbOptimizationHostedService_Dispose_CancelsInFlightOptimization()
    {
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var optimizationService = new Mock<IHashDbOptimizationService>();
        optimizationService
            .Setup(service => service.OptimizeIndexesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async cancellationToken =>
            {
                started.TrySetResult(cancellationToken);
                await Task.Delay(Timeout.Infinite, cancellationToken);
            });

        var service = new HashDbOptimizationHostedService(
            optimizationService.Object,
            Options.Create(new HashDbOptimizationOptions
            {
                AutoOptimizeOnStartup = true,
            }),
            Mock.Of<ILogger<HashDbOptimizationHostedService>>());

        await service.StartAsync(CancellationToken.None);
        var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        service.Dispose();

        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void RealmHostedService_Dispose_CancelsInitializationTokenSource()
    {
        var service = new RealmHostedService(
            new RealmService(
                new TestOptionsMonitor<RealmConfig>(new RealmConfig()),
                Mock.Of<ILogger<RealmService>>()),
            new TestOptionsMonitor<MultiRealmConfig>(new MultiRealmConfig()),
            Mock.Of<ILogger<RealmHostedService>>());

        var initializationCts = new CancellationTokenSource();
        SetPrivateField(service, "_initializationCts", initializationCts);
        SetPrivateField(service, "_initializationTask", Task.Delay(Timeout.Infinite, initializationCts.Token));

        service.Dispose();

        Assert.True(initializationCts.IsCancellationRequested);
    }

    [Fact]
    public void MultiRealmHostedService_Dispose_CancelsInitializationTokenSource()
    {
        var service = new MultiRealmHostedService(
            new MultiRealmService(
                new TestOptionsMonitor<MultiRealmConfig>(new MultiRealmConfig()),
                Mock.Of<ILogger<MultiRealmService>>()),
            new TestOptionsMonitor<MultiRealmConfig>(new MultiRealmConfig()),
            Mock.Of<ILogger<MultiRealmHostedService>>());

        var initializationCts = new CancellationTokenSource();
        SetPrivateField(service, "_initializationCts", initializationCts);
        SetPrivateField(service, "_initializationTask", Task.Delay(Timeout.Infinite, initializationCts.Token));

        service.Dispose();

        Assert.True(initializationCts.IsCancellationRequested);
    }

    [Fact]
    public async Task DhtRendezvousService_StartAsync_CancelsPreviousInitializationTokenSource()
    {
        var service = new DhtRendezvousService(
            Mock.Of<ILogger<DhtRendezvousService>>(),
            Mock.Of<IMeshOverlayServer>(),
            Mock.Of<IMeshOverlayConnector>(),
            new MeshNeighborRegistry(Mock.Of<ILogger<MeshNeighborRegistry>>()),
            new DhtRendezvousOptions
            {
                Enabled = true,
            });

        var previousInitializationCts = new CancellationTokenSource();
        SetPrivateField(service, "_backgroundInitializationCts", previousInitializationCts);

        await service.StartAsync(CancellationToken.None);

        Assert.True(previousInitializationCts.IsCancellationRequested);

        await service.StopAsync(CancellationToken.None);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on {instance.GetType().Name}.");

        field.SetValue(instance, value);
    }
}
