// <copyright file="DhtPeerGreetingServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.DhtRendezvous;
using Soulseek;
using Xunit;

public class DhtPeerGreetingServiceTests
{
    [Fact]
    public async Task StartAsync_CalledTwice_DoesNotDuplicateNeighborSubscriptions()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var service = new DhtPeerGreetingService(
            NullLogger<DhtPeerGreetingService>.Instance,
            registry,
            Mock.Of<ISoulseekClient>());

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        Assert.Equal(1, GetEventInvocationCount(registry, "NeighborAdded"));
        Assert.Equal(1, GetEventInvocationCount(registry, "FirstNeighborConnected"));

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Dispose_DetachesNeighborSubscriptions()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var service = new DhtPeerGreetingService(
            NullLogger<DhtPeerGreetingService>.Instance,
            registry,
            Mock.Of<ISoulseekClient>());

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(1, GetEventInvocationCount(registry, "NeighborAdded"));
        Assert.Equal(1, GetEventInvocationCount(registry, "FirstNeighborConnected"));

        service.Dispose();

        Assert.Equal(0, GetEventInvocationCount(registry, "NeighborAdded"));
        Assert.Equal(0, GetEventInvocationCount(registry, "FirstNeighborConnected"));
    }

    [Fact]
    public async Task OnNeighborAdded_WhenGreetingIsAlreadyInFlight_DoesNotSendDuplicateGreeting()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var soulseekClient = new Mock<ISoulseekClient>();
        var sendCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sendCount = 0;

        soulseekClient.SetupGet(client => client.State).Returns(SoulseekClientStates.Connected);
        soulseekClient
            .Setup(client => client.SendPrivateMessageAsync("peer-1", It.IsAny<string>(), It.IsAny<CancellationToken?>()))
            .Returns(async () =>
            {
                Interlocked.Increment(ref sendCount);
                await sendCompletion.Task.ConfigureAwait(false);
            });

        var service = new DhtPeerGreetingService(
            NullLogger<DhtPeerGreetingService>.Instance,
            registry,
            soulseekClient.Object);

        var handler = typeof(DhtPeerGreetingService).GetMethod(
            "OnNeighborAdded",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("OnNeighborAdded was not found.");

        var eventArgs = new MeshNeighborEventArgs(CreateConnection("peer-1", "127.0.0.1", 5100));

        handler.Invoke(service, [null, eventArgs]);
        handler.Invoke(service, [null, eventArgs]);

        await Task.Delay(100);

        Assert.Equal(1, Volatile.Read(ref sendCount));

        sendCompletion.TrySetResult(true);
    }

    private static MeshOverlayConnection CreateConnection(string username, string address, int port)
    {
        var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));

        SetBackingField(connection, "<ConnectionId>k__BackingField", $"conn-{port}");
        SetBackingField(connection, "<RemoteEndPoint>k__BackingField", new IPEndPoint(IPAddress.Parse(address), port));
        SetBackingField(connection, "<Username>k__BackingField", username);

        return connection;
    }

    private static void SetBackingField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }

    private static int GetEventInvocationCount(object target, string eventName)
    {
        var field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Event backing field '{eventName}' was not found.");

        return (field.GetValue(target) as MulticastDelegate)?.GetInvocationList().Length ?? 0;
    }
}
