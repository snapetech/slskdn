// <copyright file="MeshNeighborPeerSyncServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Messages;
using slskd.Mesh;
using Xunit;

public class MeshNeighborPeerSyncServiceTests
{
    [Fact]
    public async Task RegisterAsync_WithoutSyncService_LeavesCircuitPeerInventoryEmpty()
    {
        await using var registry = new MeshNeighborRegistry(NullLogger<MeshNeighborRegistry>.Instance);
        var peerManager = new MeshPeerManager(NullLogger<MeshPeerManager>.Instance);

        await registry.RegisterAsync(CreateConnection("peer-0", "127.0.0.10", 50305, new[] { OverlayFeatures.Mesh }));

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

        await service.StartAsync(CancellationToken.None);
        await registry.RegisterAsync(CreateConnection("peer-1", "127.0.0.1", 50305, new[] { OverlayFeatures.Mesh, OverlayFeatures.MeshSearch }));

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

        var connection = CreateConnection("peer-2", "127.0.0.2", 50305, new[] { OverlayFeatures.Mesh });

        await service.StartAsync(CancellationToken.None);
        await registry.RegisterAsync(connection);
        await registry.UnregisterAsync(connection);

        Assert.Null(peerManager.GetPeer("peer-2"));
        Assert.Equal(0, peerManager.GetStatistics().TotalPeers);

        await service.StopAsync(CancellationToken.None);
    }

    private static MeshOverlayConnection CreateConnection(string username, string address, int port, IReadOnlyList<string> features)
    {
        var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));

        SetBackingField(connection, "<ConnectionId>k__BackingField", $"conn-{port}-{username}");
        SetBackingField(connection, "<RemoteEndPoint>k__BackingField", new IPEndPoint(IPAddress.Parse(address), port));
        SetBackingField(connection, "<Username>k__BackingField", username);
        SetBackingField(connection, "<Features>k__BackingField", features);
        SetBackingField(connection, "<ConnectedAt>k__BackingField", DateTimeOffset.UtcNow);

        return connection;
    }

    private static void SetBackingField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }
}
