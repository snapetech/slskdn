// <copyright file="DhtMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Dht;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.Mesh.Transport;
using slskd.VirtualSoulfind.ShadowIndex;
using Xunit;

public class DhtMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_Ping_WithPreCancelledToken_StillTouchesRoutingTable()
    {
        var routingTable = new KademliaRoutingTable(CreateNodeId(0x01));
        var service = new DhtMeshService(
            Mock.Of<ILogger<DhtMeshService>>(),
            routingTable,
            Mock.Of<IDhtClient>(),
            Mock.Of<IMeshMessageSigner>());

        var requesterId = CreateNodeId(0x02);
        var call = new ServiceCall
        {
            ServiceName = "dht",
            Method = "Ping",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new PingRequest
            {
                RequesterId = requesterId,
            }),
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-1" },
            cts.Token).ConfigureAwait(false);

        Assert.Equal(ServiceStatusCodes.OK, reply.StatusCode);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline && routingTable.Count == 0)
        {
            await Task.Delay(20).ConfigureAwait(false);
        }

        Assert.Single(routingTable.GetAllNodes());
        Assert.Equal("peer-1", routingTable.GetAllNodes()[0].Address);
    }

    [Fact]
    public async Task HandleCallAsync_FindValue_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var dhtClient = new Mock<IDhtClient>();
        dhtClient
            .Setup(client => client.GetAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = new DhtMeshService(
            Mock.Of<ILogger<DhtMeshService>>(),
            new KademliaRoutingTable(CreateNodeId(0x01)),
            dhtClient.Object,
            Mock.Of<IMeshMessageSigner>());

        var call = new ServiceCall
        {
            ServiceName = "dht",
            Method = "FindValue",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new FindValueRequest
            {
                Key = CreateNodeId(0x03),
                RequesterId = CreateNodeId(0x02)
            }),
        };

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.UnknownError, reply.StatusCode);
        Assert.Equal("FindValue failed", reply.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", reply.ErrorMessage);
    }

    private static byte[] CreateNodeId(byte value)
    {
        var nodeId = new byte[20];
        Array.Fill(nodeId, value);
        return nodeId;
    }
}
