// <copyright file="MeshOverlayRequestRouter.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

using System.Collections.Concurrent;
using slskd.DhtRendezvous.Messages;
using slskd.Mesh.ServiceFabric;

/// <summary>
/// Tracks request/response overlay RPCs while one message loop owns socket reads.
/// </summary>
public sealed class MeshOverlayRequestRouter
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskCompletionSource<MeshSearchResponseMessage>>> _meshSearchRequests = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TaskCompletionSource<ServiceReply>>> _meshServiceRequests = new();

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

    public Task<ServiceReply> WaitForMeshServiceReplyAsync(
        MeshOverlayConnection connection,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var requests = _meshServiceRequests.GetOrAdd(connection.ConnectionId, _ => new ConcurrentDictionary<string, TaskCompletionSource<ServiceReply>>());
        var completion = new TaskCompletionSource<ServiceReply>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!requests.TryAdd(correlationId, completion))
        {
            throw new InvalidOperationException("Duplicate mesh service correlation id.");
        }

        cancellationToken.Register(() =>
        {
            if (requests.TryRemove(correlationId, out var pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        return completion.Task;
    }

    public bool TryCompleteMeshServiceReply(MeshOverlayConnection connection, ServiceReply response)
    {
        if (!_meshServiceRequests.TryGetValue(connection.ConnectionId, out var requests))
        {
            return false;
        }

        if (!requests.TryRemove(response.CorrelationId, out var completion))
        {
            return false;
        }

        completion.TrySetResult(response);
        return true;
    }

    public void RemoveMeshServiceReply(MeshOverlayConnection connection, string correlationId)
    {
        if (_meshServiceRequests.TryGetValue(connection.ConnectionId, out var requests) &&
            requests.TryRemove(correlationId, out var completion))
        {
            completion.TrySetCanceled();
        }
    }

    public void RemoveConnection(MeshOverlayConnection connection)
    {
        if (_meshSearchRequests.TryRemove(connection.ConnectionId, out var searchRequests))
        {
            foreach (var pending in searchRequests.Values)
            {
                pending.TrySetCanceled();
            }
        }

        if (!_meshServiceRequests.TryRemove(connection.ConnectionId, out var serviceRequests))
        {
            return;
        }

        foreach (var pending in serviceRequests.Values)
        {
            pending.TrySetCanceled();
        }
    }
}
