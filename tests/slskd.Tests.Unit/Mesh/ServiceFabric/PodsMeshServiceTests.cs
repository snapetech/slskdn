// <copyright file="PodsMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.PodCore;
using Xunit;

public class PodsMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_UnknownMethod_ReturnsSanitizedMethodNotFound()
    {
        var service = new PodsMeshService(
            Mock.Of<ILogger<PodsMeshService>>(),
            Mock.Of<IPodService>(),
            Mock.Of<IPodMessaging>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "pods",
                Method = "SensitiveCustomPodsMethod",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.MethodNotFound, reply.StatusCode);
        Assert.Equal("Unknown method", reply.ErrorMessage);
        Assert.DoesNotContain("SensitiveCustomPodsMethod", reply.ErrorMessage);
    }
}
