// <copyright file="MeshNeighborPeerSyncService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using Microsoft.Extensions.Hosting;
using slskd.DhtRendezvous.Messages;
using slskd.Mesh;

/// <summary>
/// Mirrors live overlay neighbors into the mesh peer inventory used by circuit services.
/// </summary>
public sealed class MeshNeighborPeerSyncService : BackgroundService
{
    private readonly ILogger<MeshNeighborPeerSyncService> _logger;
    private readonly MeshNeighborRegistry _neighborRegistry;
    private readonly IMeshPeerManager _peerManager;
    private int _subscriptionsAttached;

    public MeshNeighborPeerSyncService(
        ILogger<MeshNeighborPeerSyncService> logger,
        MeshNeighborRegistry neighborRegistry,
        IMeshPeerManager peerManager)
    {
        _logger = logger;
        _neighborRegistry = neighborRegistry;
        _peerManager = peerManager;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        AttachNeighborSubscriptions();
        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        _logger.LogInformation("Mesh neighbor peer sync service started");

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        DetachNeighborSubscriptions();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        DetachNeighborSubscriptions();
        base.Dispose();
    }

    private void OnNeighborAdded(object? sender, MeshNeighborEventArgs e)
    {
        var peerId = GetPeerId(e);
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return;
        }

        var supportsCircuitRouting = e.Connection.Features.Contains(OverlayFeatures.Mesh, StringComparer.OrdinalIgnoreCase);

        _peerManager.AddOrUpdatePeer(new MeshPeer(peerId, new List<IPEndPoint> { e.Connection.RemoteEndPoint })
        {
            Version = e.Connection.Features.Count > 0 ? string.Join(',', e.Connection.Features) : string.Empty,
            SupportsOnionRouting = supportsCircuitRouting,
        });

        _logger.LogDebug(
            "Mirrored mesh neighbor {PeerId} from {Endpoint} into mesh peer inventory (circuit-capable: {SupportsCircuitRouting})",
            peerId,
            e.Connection.RemoteEndPoint,
            supportsCircuitRouting);
    }

    private void OnNeighborRemoved(object? sender, MeshNeighborEventArgs e)
    {
        var peerId = GetPeerId(e);
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return;
        }

        _peerManager.RemovePeer(peerId);

        _logger.LogDebug(
            "Removed mesh neighbor {PeerId} from mesh peer inventory",
            peerId);
    }

    private static string GetPeerId(MeshNeighborEventArgs e)
    {
        return !string.IsNullOrWhiteSpace(e.Username)
            ? e.Username
            : e.Connection.ConnectionId;
    }

    private void AttachNeighborSubscriptions()
    {
        if (Interlocked.Exchange(ref _subscriptionsAttached, 1) == 1)
        {
            return;
        }

        _neighborRegistry.NeighborAdded += OnNeighborAdded;
        _neighborRegistry.NeighborRemoved += OnNeighborRemoved;
    }

    private void DetachNeighborSubscriptions()
    {
        if (Interlocked.Exchange(ref _subscriptionsAttached, 0) == 0)
        {
            return;
        }

        _neighborRegistry.NeighborAdded -= OnNeighborAdded;
        _neighborRegistry.NeighborRemoved -= OnNeighborRemoved;
    }
}
