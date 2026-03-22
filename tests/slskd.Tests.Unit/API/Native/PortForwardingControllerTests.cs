// <copyright file="PortForwardingControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Native;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.Native;
using slskd.Common.Security;
using slskd.Mesh.ServiceFabric;
using Xunit;

public class PortForwardingControllerTests
{
    [Fact]
    public async Task StartForwarding_WithWhitespacePodId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.StartForwarding(new StartPortForwardingRequest
        {
            LocalPort = 12345,
            PodId = "   ",
            DestinationHost = "example.com",
            DestinationPort = 80
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task StartForwarding_WithWhitespaceDestinationHost_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.StartForwarding(new StartPortForwardingRequest
        {
            LocalPort = 12345,
            PodId = "pod-1",
            DestinationHost = "   ",
            DestinationPort = 80
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetAvailablePorts_WithSinglePortRange_ReturnsThatPortWhenUnused()
    {
        var controller = CreateController();

        var result = controller.GetAvailablePorts(12345, 12345);

        var ok = Assert.IsType<OkObjectResult>(result);
        var ports = ok.Value?.ToString() ?? string.Empty;
        Assert.Contains("12345", ports);
    }

    private static PortForwardingController CreateController()
    {
        var forwarder = new LocalPortForwarder(
            NullLogger<LocalPortForwarder>.Instance,
            Mock.Of<IMeshServiceClient>());

        return new PortForwardingController(forwarder);
    }
}
