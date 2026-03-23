// <copyright file="VirtualSoulfindMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.VirtualSoulfind.ShadowIndex;
using Xunit;

public class VirtualSoulfindMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var service = new VirtualSoulfindMeshService(
            Mock.Of<ILogger<VirtualSoulfindMeshService>>(),
            Mock.Of<IShadowIndexQuery>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "shadow-index",
                Method = "TotallyCustomSensitiveMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown method", reply.ErrorMessage);
        Assert.DoesNotContain("Sensitive", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleCallAsync_QueryBatch_InvalidMbids_ReturnsSanitizedError()
    {
        var service = new VirtualSoulfindMeshService(
            Mock.Of<ILogger<VirtualSoulfindMeshService>>(),
            Mock.Of<IShadowIndexQuery>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "shadow-index",
                Method = "QueryBatch",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
                {
                    MBIDs = new[] { "valid-mbid", "../etc/passwd", "another-bad\\value" }
                })
            },
            new MeshServiceContext { RemotePeerId = "peer-origin" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.InvalidPayload, reply.StatusCode);
        Assert.Equal("Invalid MBID list", reply.ErrorMessage);
        Assert.DoesNotContain("passwd", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
