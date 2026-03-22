// <copyright file="MeshServiceClientTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Overlay;
using slskd.Mesh.ServiceFabric;
using Xunit;

public class MeshServiceClientTests
{
    [Fact]
    public async Task CallServiceAsync_WhenNoProvidersExist_ReturnsSanitizedNotFoundMessage()
    {
        var directory = new Mock<IMeshServiceDirectory>();
        directory
            .Setup(d => d.FindByNameAsync("shadow-index/private", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MeshServiceDescriptor>());

        var client = new MeshServiceClient(
            Mock.Of<ILogger<MeshServiceClient>>(),
            directory.Object,
            Mock.Of<IControlSigner>());

        var reply = await client.CallServiceAsync(
            "shadow-index/private",
            "QueryByMbid",
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.ServiceNotFound, reply.StatusCode);
        Assert.Equal("No providers available for requested service", reply.ErrorMessage);
        Assert.DoesNotContain("shadow-index/private", reply.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
