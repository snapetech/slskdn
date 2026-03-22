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
}
