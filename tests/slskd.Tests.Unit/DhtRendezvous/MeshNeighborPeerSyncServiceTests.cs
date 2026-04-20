// <copyright file="MeshNeighborPeerSyncServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Messages;
using slskd.Mesh;
using slskd.DhtRendezvous.Security;
using Xunit;

public class MeshNeighborPeerSyncServiceTests
{
    [Fact]
    public async Task RegisterAsync_WithoutSyncService_LeavesCircuitPeerInventoryEmpty()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        await using var connection = CreateConnection("peer-0", "127.0.0.10", 50305, new[] { OverlayFeatures.Mesh });

        await registry.RegisterAsync(connection);

        Assert.Null(peerManager.GetPeer("peer-0"));
        Assert.Equal(0, peerManager.GetStatistics().TotalPeers);
        Assert.Equal(0, peerManager.GetStatistics().OnionRoutingPeers);
    }

    [Fact]
    public async Task StartAsync_WhenNeighborRegisters_PopulatesPeerInventory()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var service = new MeshNeighborPeerSyncService(
            NullLogger<MeshNeighborPeerSyncService>.Instance,
            registry,
            peerManager);
        await using var connection = CreateConnection("peer-1", "127.0.0.1", 50305, new[] { OverlayFeatures.Mesh, OverlayFeatures.MeshSearch });

        await service.StartAsync(CancellationToken.None);
        await registry.RegisterAsync(connection);

        var stats = peerManager.GetStatistics();
        var peer = peerManager.GetPeer("peer-1");

        Assert.NotNull(peer);
        Assert.Equal(1, stats.TotalPeers);
        Assert.Equal(1, stats.ActivePeers);
        Assert.Equal(1, stats.OnionRoutingPeers);
        Assert.Equal(IPAddress.Parse("127.0.0.1"), peer!.GetBestAddress().Address);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WhenNeighborUnregisters_RemovesPeerFromInventory()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var service = new MeshNeighborPeerSyncService(
            NullLogger<MeshNeighborPeerSyncService>.Instance,
            registry,
            peerManager);

        await using var connection = CreateConnection("peer-2", "127.0.0.2", 50305, new[] { OverlayFeatures.Mesh });

        await service.StartAsync(CancellationToken.None);
        await registry.RegisterAsync(connection);
        await registry.UnregisterAsync(connection);

        Assert.Null(peerManager.GetPeer("peer-2"));
        Assert.Equal(0, peerManager.GetStatistics().TotalPeers);

        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task UnregisterAsync_WhenOneDirectionRemains_KeepsPeerInventory()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);
        var service = new MeshNeighborPeerSyncService(
            NullLogger<MeshNeighborPeerSyncService>.Instance,
            registry,
            peerManager);

        await using var inbound = CreateConnection("peer-3", "127.0.0.3", 50305, new[] { OverlayFeatures.Mesh }, isOutbound: false);
        await using var outbound = CreateConnection("peer-3", "127.0.0.3", 50306, new[] { OverlayFeatures.Mesh }, isOutbound: true);

        await service.StartAsync(CancellationToken.None);
        await registry.RegisterAsync(inbound);
        await registry.RegisterAsync(outbound);
        await registry.UnregisterAsync(inbound);

        Assert.NotNull(peerManager.GetPeer("peer-3"));
        Assert.Equal(1, peerManager.GetStatistics().TotalPeers);
        Assert.Equal(1, peerManager.GetStatistics().OnionRoutingPeers);

        await registry.UnregisterAsync(outbound);

        Assert.Null(peerManager.GetPeer("peer-3"));
        Assert.Equal(0, peerManager.GetStatistics().TotalPeers);

        await service.StopAsync(CancellationToken.None);
    }

    private static MeshOverlayConnection CreateConnection(string username, string address, int port, IReadOnlyList<string> features, bool isOutbound = false)
    {
        var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));

        var sslStream = new SslStream(new MemoryStream());
        SetField(connection, "_cts", new CancellationTokenSource());
        SetField(connection, "_tcpClient", new TcpClient());
        SetField(connection, "_sslStream", sslStream);
        SetField(connection, "_framer", new SecureMessageFramer(sslStream));

        SetPropertyOrField(connection, "ConnectionId", $"conn-{port}-{username}");
        SetPropertyOrField(connection, "RemoteEndPoint", new IPEndPoint(IPAddress.Parse(address), port));
        SetPropertyOrField(connection, "Username", username);
        SetPropertyOrField(connection, "Features", features);
        SetPropertyOrField(connection, "ConnectedAt", DateTimeOffset.UtcNow);
        SetPropertyOrField(connection, "IsOutbound", isOutbound);

        return connection;
    }

    private static void SetField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
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
}
