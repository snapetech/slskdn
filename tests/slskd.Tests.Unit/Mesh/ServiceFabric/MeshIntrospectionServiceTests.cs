// <copyright file="MeshIntrospectionServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using Xunit;

public class MeshIntrospectionServiceTests
{
    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var router = new MeshServiceRouter(
            Mock.Of<ILogger<MeshServiceRouter>>(),
            Options.Create(new MeshServiceFabricOptions()));
        var service = new MeshIntrospectionService(
            Mock.Of<ILogger<MeshIntrospectionService>>(),
            router,
            Mock.Of<IMeshServiceDirectory>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "mesh-introspect",
                Method = "SensitiveIntrospectionMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown method", reply.ErrorMessage);
        Assert.DoesNotContain("SensitiveIntrospectionMethod", reply.ErrorMessage);
    }
}
