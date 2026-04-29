// <copyright file="MeshStatsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.Native;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.API.Native;
using slskd.Mesh;
using Xunit;

public class MeshStatsControllerTests
{
    [Fact]
    public async Task GetStats_OnFailure_DoesNotLeakExceptionMessage()
    {
        var meshAdvanced = new Mock<IMeshAdvanced>();
        meshAdvanced
            .Setup(service => service.GetTransportStatsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new MeshStatsController(meshAdvanced.Object);

        var result = await controller.GetStats(CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
    }
}
