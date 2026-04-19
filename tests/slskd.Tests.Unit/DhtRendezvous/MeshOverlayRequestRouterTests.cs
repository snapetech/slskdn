// <copyright file="MeshOverlayRequestRouterTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DhtRendezvous;

using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using slskd.DhtRendezvous;
using slskd.DhtRendezvous.Messages;
using Xunit;

public class MeshOverlayRequestRouterTests
{
    [Fact]
    public async Task RemoveMeshSearchResponse_WhenRequestWasRegistered_CancelsPendingTask()
    {
        var router = new MeshOverlayRequestRouter();
        var connection = CreateConnection("conn-1");

        var pending = router.WaitForMeshSearchResponseAsync(connection, "req-1", CancellationToken.None);

        router.RemoveMeshSearchResponse(connection, "req-1");

        await Assert.ThrowsAsync<TaskCanceledException>(() => pending);
        Assert.False(router.TryCompleteMeshSearchResponse(connection, new MeshSearchResponseMessage { RequestId = "req-1" }));
    }

    [Fact]
    public async Task RemoveConnection_WhenRequestsArePending_CancelsAllPendingTasks()
    {
        var router = new MeshOverlayRequestRouter();
        var connection = CreateConnection("conn-1");

        var first = router.WaitForMeshSearchResponseAsync(connection, "req-1", CancellationToken.None);
        var second = router.WaitForMeshSearchResponseAsync(connection, "req-2", CancellationToken.None);

        router.RemoveConnection(connection);

        await Assert.ThrowsAsync<TaskCanceledException>(() => first);
        await Assert.ThrowsAsync<TaskCanceledException>(() => second);
    }

    private static MeshOverlayConnection CreateConnection(string connectionId)
    {
        var connection = (MeshOverlayConnection)RuntimeHelpers.GetUninitializedObject(typeof(MeshOverlayConnection));

        SetField(connection, "_cts", new CancellationTokenSource());
        SetField(connection, "_tcpClient", new TcpClient());
        SetField(connection, "_sslStream", new SslStream(new MemoryStream()));
        SetBackingField(connection, "<ConnectionId>k__BackingField", connectionId);
        SetBackingField(connection, "<RemoteEndPoint>k__BackingField", new IPEndPoint(IPAddress.Loopback, 5000));
        SetBackingField(connection, "<Username>k__BackingField", "peer-1");

        return connection;
    }

    private static void SetBackingField(object target, string fieldName, object? value) => SetField(target, fieldName, value);

    private static void SetField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found.");

        field.SetValue(target, value);
    }
}
