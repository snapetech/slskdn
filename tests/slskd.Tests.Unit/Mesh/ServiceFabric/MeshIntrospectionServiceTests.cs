// <copyright file="MeshIntrospectionServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System;
using System.Linq;
using System.Text.Json;
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

    [Fact]
    public async Task HandleCallAsync_GetCapabilities_UsesRegisteredServiceNames()
    {
        var router = new MeshServiceRouter(
            Mock.Of<ILogger<MeshServiceRouter>>(),
            Options.Create(new MeshServiceFabricOptions()));
        router.RegisterService(new TestMeshService("pods"));
        router.RegisterService(new TestMeshService("shadow-index"));

        var service = new MeshIntrospectionService(
            Mock.Of<ILogger<MeshIntrospectionService>>(),
            router,
            Mock.Of<IMeshServiceDirectory>());

        var reply = await service.HandleCallAsync(
            new ServiceCall
            {
                ServiceName = "mesh-introspect",
                Method = "GetCapabilities",
                CorrelationId = Guid.NewGuid().ToString(),
                Payload = Array.Empty<byte>()
            },
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.OK, reply.StatusCode);
        using var document = JsonDocument.Parse(reply.Payload);
        var services = document.RootElement.GetProperty("Services").EnumerateArray().Select(x => x.GetString()).ToArray();
        Assert.Contains("pods", services);
        Assert.Contains("shadow-index", services);
    }

    private sealed class TestMeshService : IMeshService
    {
        public TestMeshService(string serviceName)
        {
            ServiceName = serviceName;
        }

        public string ServiceName { get; }

        public Task<ServiceReply> HandleCallAsync(ServiceCall call, MeshServiceContext context, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task HandleStreamAsync(MeshServiceStream stream, MeshServiceContext context, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
