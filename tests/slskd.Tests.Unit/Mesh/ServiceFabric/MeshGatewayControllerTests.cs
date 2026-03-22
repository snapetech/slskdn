// <copyright file="MeshGatewayControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Mesh.ServiceFabric;

using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.Mesh;
using slskd.Mesh.ServiceFabric;
using Xunit;

public class MeshGatewayControllerTests
{
    [Fact]
    public async Task CallService_WithBlankServiceOrMethod_ReturnsBadRequest()
    {
        var controller = CreateController(new MeshGatewayOptions
        {
            Enabled = true,
            AllowedServices = new() { "pods" }
        });

        var result = await controller.CallService("   ", "  ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CallService_TrimsServiceAndMethodBeforeLookup()
    {
        var directory = new Mock<IMeshServiceDirectory>();
        directory
            .Setup(service => service.FindByNameAsync("pods", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MeshServiceDescriptor("svc-1", "pods", "peer-1", 1, DateTimeOffset.UtcNow, new Dictionary<string, string>())
            });

        var client = new Mock<IMeshServiceClient>();
        client
            .Setup(service => service.CallServiceAsync("pods", "list", It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServiceReply(ServiceStatusCodes.OK, Array.Empty<byte>(), null));

        var controller = CreateController(
            new MeshGatewayOptions
            {
                Enabled = true,
                AllowedServices = new() { "pods" }
            },
            directory,
            client);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Body = new MemoryStream();

        var result = await controller.CallService(" pods ", " list ", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        directory.Verify(service => service.FindByNameAsync("pods", It.IsAny<CancellationToken>()), Times.Once);
        client.Verify(service => service.CallServiceAsync("pods", "list", It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MeshGatewayController CreateController(
        MeshGatewayOptions options,
        Mock<IMeshServiceDirectory>? directory = null,
        Mock<IMeshServiceClient>? client = null)
    {
        return new MeshGatewayController(
            NullLogger<MeshGatewayController>.Instance,
            Options.Create(options),
            (directory ?? new Mock<IMeshServiceDirectory>()).Object,
            (client ?? new Mock<IMeshServiceClient>()).Object);
    }
}
