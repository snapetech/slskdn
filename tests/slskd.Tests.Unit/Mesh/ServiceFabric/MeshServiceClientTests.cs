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

    [Fact]
    public async Task CallServiceAsync_TrimsInputsAndSkipsBlankProviderIds()
    {
        var directory = new Mock<IMeshServiceDirectory>();
        directory
            .Setup(d => d.FindByNameAsync("shadow-index/private", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MeshServiceDescriptor
                {
                    ServiceId = "svc-1",
                    ServiceName = "shadow-index/private",
                    OwnerPeerId = " ",
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
                },
                new MeshServiceDescriptor
                {
                    ServiceId = "svc-2",
                    ServiceName = "shadow-index/private",
                    OwnerPeerId = " peer-2 ",
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20)
                }
            });

        var client = new MeshServiceClient(
            Mock.Of<ILogger<MeshServiceClient>>(),
            directory.Object,
            Mock.Of<IControlSigner>());

        var reply = await client.CallServiceAsync(
            " shadow-index/private ",
            " QueryByMbid ",
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None);

        Assert.Equal(ServiceStatusCodes.ServiceUnavailable, reply.StatusCode);
        Assert.Equal("Mesh service transport is unavailable.", reply.ErrorMessage);
    }
}
