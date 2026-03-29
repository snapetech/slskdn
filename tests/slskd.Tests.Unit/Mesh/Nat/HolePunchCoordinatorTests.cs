// <copyright file="HolePunchCoordinatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh;
using slskd.Mesh.Nat;
using slskd.Mesh.ServiceFabric;

namespace slskd.Tests.Unit.Mesh.Nat;

public class HolePunchCoordinatorTests
{
    [Fact]
    public async Task RequestHolePunchAsync_WhenMeshReplyFails_ReturnsSanitizedError()
    {
        var meshClient = new Mock<IMeshServiceClient>();
        meshClient
            .Setup(x => x.CallAsync(
                "peer-bob",
                It.IsAny<ServiceCall>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceReply
            {
                StatusCode = 502,
                ErrorMessage = "downstream socket exploded"
            });

        var coordinator = new HolePunchCoordinator(
            Mock.Of<ILogger<HolePunchCoordinator>>(),
            meshClient.Object,
            Mock.Of<INatDetector>());

        var result = await coordinator.RequestHolePunchAsync("peer-bob", ["udp://127.0.0.1:1234"]);

        Assert.False(result.Success);
        Assert.Equal("Hole punch request failed", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestHolePunchAsync_WhenMeshCallThrows_ReturnsSanitizedError()
    {
        var meshClient = new Mock<IMeshServiceClient>();
        meshClient
            .Setup(x => x.CallAsync(
                "peer-bob",
                It.IsAny<ServiceCall>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("peer-bob transport secret"));

        var coordinator = new HolePunchCoordinator(
            Mock.Of<ILogger<HolePunchCoordinator>>(),
            meshClient.Object,
            Mock.Of<INatDetector>());

        var result = await coordinator.RequestHolePunchAsync("peer-bob", ["udp://127.0.0.1:1234"]);

        Assert.False(result.Success);
        Assert.Equal("Hole punch request failed", result.ErrorMessage);
    }
}
