// <copyright file="PortForwardingControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Native;

using System.Net;
using System.Net.Sockets;
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
        var availablePortsProperty = ok.Value?.GetType().GetProperty("AvailablePorts");
        var availablePorts = Assert.IsAssignableFrom<IEnumerable<int>>(availablePortsProperty?.GetValue(ok.Value));
        Assert.Contains(12345, availablePorts);
    }

    [Fact]
    public async Task StartForwarding_WhenForwarderThrows_DoesNotLeakExceptionMessage()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var occupiedPort = ((IPEndPoint)listener.LocalEndpoint).Port;

        var controller = CreateController();

        var result = await controller.StartForwarding(new StartPortForwardingRequest
        {
            LocalPort = occupiedPort,
            PodId = "pod-1",
            DestinationHost = "example.com",
            DestinationPort = 80
        });

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("address", error.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed to start port forwarding", error.Value?.ToString() ?? string.Empty);
    }

    private static PortForwardingController CreateController()
    {
        var forwarder = new LocalPortForwarder(
            NullLogger<LocalPortForwarder>.Instance,
            Mock.Of<IMeshServiceClient>());

        return new PortForwardingController(forwarder);
    }
}
