// <copyright file="MeshContentMeshServiceTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.ServiceFabric;
using slskd.Mesh.ServiceFabric.Services;
using slskd.Shares;
using Xunit;

public class MeshContentMeshServiceTests
{
    [Fact]
    public async Task HandleCallAsync_GetByContentId_WhenDependencyThrows_ReturnsSanitizedError()
    {
        var shareService = new Mock<IShareService>();
        shareService
            .Setup(service => service.GetLocalRepository())
            .Throws(new InvalidOperationException("sensitive detail"));

        var service = new MeshContentMeshService(
            Mock.Of<ILogger<MeshContentMeshService>>(),
            shareService.Object);

        var call = new ServiceCall
        {
            ServiceName = "MeshContent",
            Method = "GetByContentId",
            CorrelationId = Guid.NewGuid().ToString(),
            Payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                contentId = "content:audio:track:mb-12345"
            })
        };

        var reply = await service.HandleCallAsync(
            call,
            new MeshServiceContext { RemotePeerId = "peer-1" },
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.UnknownError, reply.StatusCode);
        Assert.Equal("Mesh content service error", reply.ErrorMessage);
        Assert.DoesNotContain("sensitive detail", reply.ErrorMessage);
    }
}
