// <copyright file="MeshNeighborRegistryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
using Xunit;

public class MeshNeighborRegistryTests
{
    [Fact]
    public async Task RegisterAsync_WhenOneNeighborAddedSubscriberThrows_StillInvokesRemainingSubscribers()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var connection = CreateConnection("peer-1", "127.0.0.1", 5000);

        MeshOverlayConnection? observed = null;
        registry.NeighborAdded += (_, _) => throw new InvalidOperationException("boom");
        registry.NeighborAdded += (_, args) => observed = args.Connection;

        var registered = await registry.RegisterAsync(connection);

        Assert.True(registered);
        Assert.Same(connection, observed);
        Assert.Equal(1, registry.Count);

        await registry.UnregisterAsync(connection);
    }

    [Fact]
    public async Task UnregisterAsync_WhenOneNeighborRemovedSubscriberThrows_StillInvokesRemainingSubscribers()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var connection = CreateConnection("peer-1", "127.0.0.1", 5001);

        await registry.RegisterAsync(connection);

        MeshOverlayConnection? observed = null;
        registry.NeighborRemoved += (_, _) => throw new InvalidOperationException("boom");
        registry.NeighborRemoved += (_, args) => observed = args.Connection;

        await registry.UnregisterAsync(connection);

        Assert.Same(connection, observed);
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public async Task RegisterAsync_WhenFirstNeighborSubscriberThrows_StillInvokesRemainingSubscribers()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var connection = CreateConnection("peer-1", "127.0.0.1", 5002);

        MeshOverlayConnection? observed = null;
        registry.FirstNeighborConnected += (_, _) => throw new InvalidOperationException("boom");
        registry.FirstNeighborConnected += (_, args) => observed = args.Connection;

        var registered = await registry.RegisterAsync(connection);

        Assert.True(registered);
        Assert.Same(connection, observed);

        await registry.UnregisterAsync(connection);
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
}
