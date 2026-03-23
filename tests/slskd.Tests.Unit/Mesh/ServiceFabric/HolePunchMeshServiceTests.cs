// <copyright file="HolePunchMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Nat;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using Xunit;

public class HolePunchMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var service = new HolePunchMeshService(
            Mock.Of<ILogger<HolePunchMeshService>>(),
            Mock.Of<IUdpHolePuncher>(),
            Mock.Of<IMeshServiceClient>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "hole-punch",
                Method = "RequestPunchButActuallySensitive",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown hole punch method", reply.ErrorMessage);
        Assert.DoesNotContain("Sensitive", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCallAsync_RequestPunch_WhenTargetReplyFails_ReturnsSanitizedError()
    {
        var meshClient = new Mock<IMeshServiceClient>();
        meshClient
            .Setup(client => client.CallAsync("peer-target", It.IsAny<ServiceCall>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceReply
            {
                StatusCode = ServiceStatusCodes.UnknownError,
                ErrorMessage = "sensitive detail",
                Payload = Array.Empty<byte>()
            });

        var service = new HolePunchMeshService(
            Mock.Of<ILogger<HolePunchMeshService>>(),
            Mock.Of<IUdpHolePuncher>(),
            meshClient.Object);

        var call = new ServiceCall
        {
            ServiceName = "hole-punch",
            Method = "RequestPunch",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new slskd.Mesh.ServiceFabric.Services.HolePunchRequest
            {
                TargetPeerId = "peer-target",
                LocalEndpoints = new[] { "127.0.0.1:5000" }
            })
        };

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.UnknownError, reply.StatusCode);
        Assert.Equal("Failed to contact target peer", reply.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", reply.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_RequestPunch_WhenMeshClientThrows_ReturnsSanitizedError()
    {
        var meshClient = new Mock<IMeshServiceClient>();
        meshClient
            .Setup(client => client.CallAsync("peer-target", It.IsAny<ServiceCall>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var service = new HolePunchMeshService(
            Mock.Of<ILogger<HolePunchMeshService>>(),
            Mock.Of<IUdpHolePuncher>(),
            meshClient.Object);

        var call = new ServiceCall
        {
            ServiceName = "hole-punch",
            Method = "RequestPunch",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new slskd.Mesh.ServiceFabric.Services.HolePunchRequest
            {
                TargetPeerId = "peer-target",
                LocalEndpoints = new[] { "127.0.0.1:5000" }
            })
        };

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.UnknownError, reply.StatusCode);
        Assert.Equal("Failed to contact target peer", reply.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", reply.ErrorMessage);
    }

    [Fact]
    public async Task HandleCallAsync_RequestPunch_WithInvalidPayload_ReturnsSanitizedError()
    {
        var service = new HolePunchMeshService(
            Mock.Of<ILogger<HolePunchMeshService>>(),
            Mock.Of<IUdpHolePuncher>(),
            Mock.Of<IMeshServiceClient>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "hole-punch",
                Method = "RequestPunch",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    targetPeerId = "",
                    localEndpoints = Array.Empty<string>()
                })
            },
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, reply.StatusCode);
        Assert.Equal("Invalid request payload", reply.ErrorMessage);
        Assert.DoesNotContain("targetPeerId", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
