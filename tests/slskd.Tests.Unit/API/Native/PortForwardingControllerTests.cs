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
    public void GetForwardingStatus_WhenPortIsNotForwarded_ReturnsSanitizedNotFound()
    {
        var controller = CreateController();

        var result = controller.GetForwardingStatus(54321);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Contains("No forwarding configured for this port", notFound.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain("54321", notFound.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public async Task StartForwarding_WhenPortAlreadyForwarded_DoesNotLeakExceptionMessage()
    {
        var forwarder = new LocalPortForwarder(
            NullLogger<LocalPortForwarder>.Instance,
            Mock.Of<IMeshServiceClient>());
        var controller = new PortForwardingController(forwarder);

        try
        {
            var first = await controller.StartForwarding(new StartPortForwardingRequest
            {
                LocalPort = 12346,
                PodId = "pod-1",
                DestinationHost = "example.com",
                DestinationPort = 80
            });
            Assert.IsType<OkObjectResult>(first);

            var second = await controller.StartForwarding(new StartPortForwardingRequest
            {
                LocalPort = 12346,
                PodId = "pod-1",
                DestinationHost = "example.com",
                DestinationPort = 80
            });

            var conflict = Assert.IsType<ConflictObjectResult>(second);
            Assert.DoesNotContain("already being forwarded", conflict.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Port forwarding is already configured for this local port", conflict.Value?.ToString() ?? string.Empty);
        }
        finally
        {
            await forwarder.StopForwardingAsync(12346);
            forwarder.Dispose();
        }
    }

    [Fact]
    public async Task StartForwarding_ReturnsSanitizedSuccessMessage()
    {
        var forwarder = new LocalPortForwarder(
            NullLogger<LocalPortForwarder>.Instance,
            Mock.Of<IMeshServiceClient>());
        var controller = new PortForwardingController(forwarder);

        try
        {
            var result = await controller.StartForwarding(new StartPortForwardingRequest
            {
                LocalPort = 12347,
                PodId = "pod-1",
                DestinationHost = "example.com",
                DestinationPort = 80
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Port forwarding started", ok.Value?.ToString() ?? string.Empty);
            Assert.DoesNotContain("12347", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await forwarder.StopForwardingAsync(12347);
            forwarder.Dispose();
        }
    }

    [Fact]
    public async Task StopForwarding_ReturnsSanitizedSuccessMessage()
    {
        var forwarder = new LocalPortForwarder(
            NullLogger<LocalPortForwarder>.Instance,
            Mock.Of<IMeshServiceClient>());
        var controller = new PortForwardingController(forwarder);

        try
        {
            await forwarder.StartForwardingAsync(12348, "pod-1", "example.com", 80);

            var result = await controller.StopForwarding(12348);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Contains("Port forwarding stopped", ok.Value?.ToString() ?? string.Empty);
            Assert.DoesNotContain("12348", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            forwarder.Dispose();
        }
    }

    private static PortForwardingController CreateController()
    {
        var forwarder = new LocalPortForwarder(
            NullLogger<LocalPortForwarder>.Instance,
            Mock.Of<IMeshServiceClient>());

        return new PortForwardingController(forwarder);
    }
}
