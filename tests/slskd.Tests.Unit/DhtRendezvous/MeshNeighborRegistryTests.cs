// <copyright file="MeshNeighborRegistryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Security;
using Xunit;

public class MeshNeighborRegistryTests
{
    [Fact]
    public async Task RegisterAsync_WhenOneNeighborAddedSubscriberThrows_StillInvokesRemainingSubscribers()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        await using var connection = CreateConnection("peer-1", "127.0.0.1", 5000);

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
        await using var connection = CreateConnection("peer-1", "127.0.0.1", 5001);

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
        await using var connection = CreateConnection("peer-1", "127.0.0.1", 5002);

        MeshOverlayConnection? observed = null;
        registry.FirstNeighborConnected += (_, _) => throw new InvalidOperationException("boom");
        registry.FirstNeighborConnected += (_, args) => observed = args.Connection;

        var registered = await registry.RegisterAsync(connection);

        Assert.True(registered);
        Assert.Same(connection, observed);

        await registry.UnregisterAsync(connection);
    }


    [Fact]
    public async Task RegisterAsync_WhenOutboundArrivesForInboundPeer_KeepsBothDirections()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        await using var inbound = CreateConnection("peer-1", "127.0.0.1", 5100, isOutbound: false);
        await using var outbound = CreateConnection("peer-1", "127.0.0.1", 5101, isOutbound: true);

        Assert.True(await registry.RegisterAsync(inbound));
        Assert.True(await registry.RegisterAsync(outbound));

        Assert.Equal(1, registry.Count);
        Assert.Same(outbound, registry.GetConnection("peer-1"));
        Assert.Equal(2, registry.GetAllConnections().Count);

        await registry.UnregisterAsync(inbound);

        Assert.Equal(1, registry.Count);
        Assert.Single(registry.GetAllConnections());
        Assert.Same(outbound, registry.GetConnection("peer-1"));
    }

    [Fact]
    public async Task UnregisterAsync_WhenOneDirectionIsRemoved_DoesNotRemoveRemainingDirection()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        await using var inbound = CreateConnection("peer-1", "127.0.0.1", 5200, isOutbound: false);
        await using var outbound = CreateConnection("peer-1", "127.0.0.1", 5201, isOutbound: true);

        Assert.True(await registry.RegisterAsync(inbound));
        Assert.True(await registry.RegisterAsync(outbound));

        await registry.UnregisterAsync(inbound);

        Assert.Equal(1, registry.Count);
        Assert.Same(outbound, registry.GetConnection("peer-1"));

        await registry.UnregisterAsync(outbound);

        Assert.Equal(0, registry.Count);
        Assert.Null(registry.GetConnection("peer-1"));
    }

    private static MeshOverlayConnection CreateConnection(string username, string address, int port, bool isOutbound = false)
    {
        var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));

        SetField(connection, "_cts", new CancellationTokenSource());
        SetField(connection, "_tcpClient", new TcpClient());
        var sslStream = new SslStream(new MemoryStream());
        SetField(connection, "_sslStream", sslStream);
        SetField(connection, "_framer", new SecureMessageFramer(sslStream));
        SetPropertyOrField(connection, "ConnectionId", $"conn-{port}");
        SetPropertyOrField(connection, "RemoteEndPoint", new IPEndPoint(IPAddress.Parse(address), port));
        SetPropertyOrField(connection, "Username", username);
        SetPropertyOrField(connection, "IsOutbound", isOutbound);

        return connection;
    }

    private static void SetPropertyOrField(object target, string memberName, object? value)
    {
        var property = target.GetType().GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property?.GetSetMethod(true) is { } setMethod)
        {
            setMethod.Invoke(target, [value]);
            return;
        }

        var field = target.GetType().GetField($"<{memberName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Member '{memberName}' was not found.");

        field.SetValue(target, value);
    }

    private static void SetField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }
}
