// <copyright file="MeshOverlayRequestRouter.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous;

using System.Collections.Concurrent;
using slskd.DhtRendezvous.Messages;

/// <summary>
/// Tracks request/response overlay RPCs while one message loop owns socket reads.
/// </summary>
public sealed class MeshOverlayRequestRouter
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskCompletionSource<MeshSearchResponseMessage>>> _meshSearchRequests = new();

    public Task<MeshSearchResponseMessage> WaitForMeshSearchResponseAsync(
        MeshOverlayConnection connection,
        string requestId,
        CancellationToken cancellationToken)
    {
        var requests = _meshSearchRequests.GetOrAdd(connection.ConnectionId, _ => new ConcurrentDictionary<string, TaskCompletionSource<MeshSearchResponseMessage>>());
        var completion = new TaskCompletionSource<MeshSearchResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!requests.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException("Duplicate mesh search request id.");
        }

        cancellationToken.Register(() =>
        {
            if (requests.TryRemove(requestId, out var pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        return completion.Task;
    }

    public bool TryCompleteMeshSearchResponse(MeshOverlayConnection connection, MeshSearchResponseMessage response)
    {
        if (!_meshSearchRequests.TryGetValue(connection.ConnectionId, out var requests))
        {
            return false;
        }

        if (!requests.TryRemove(response.RequestId, out var completion))
        {
            return false;
        }

        completion.TrySetResult(response);
        return true;
    }

    public void RemoveMeshSearchResponse(MeshOverlayConnection connection, string requestId)
    {
        if (_meshSearchRequests.TryGetValue(connection.ConnectionId, out var requests) &&
            requests.TryRemove(requestId, out var completion))
        {
            completion.TrySetCanceled();
        }
    }

    public void RemoveConnection(MeshOverlayConnection connection)
    {
        if (!_meshSearchRequests.TryRemove(connection.ConnectionId, out var requests))
        {
            return;
        }

        foreach (var pending in requests.Values)
        {
            pending.TrySetCanceled();
        }
    }
}
