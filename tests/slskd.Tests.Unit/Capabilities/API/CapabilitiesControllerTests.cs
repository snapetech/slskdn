// <copyright file="CapabilitiesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Capabilities.API;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Capabilities;
using slskd.Capabilities.API;
using Xunit;

public class CapabilitiesControllerTests
{
    [Fact]
    public void GetPeer_WithBlankUsername_ReturnsBadRequest()
    {
        var controller = new CapabilitiesController(Mock.Of<ICapabilityService>());

        var result = controller.GetPeer("   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetPeer_WhenPeerIsUnknown_DoesNotEchoUsername()
    {
        var capabilityService = new Mock<ICapabilityService>();
        capabilityService.Setup(service => service.GetPeerCapabilities("alice")).Returns((PeerCapabilities?)null);

        var controller = new CapabilitiesController(capabilityService.Object);

        var result = controller.GetPeer(" alice ");

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.DoesNotContain("alice", notFound.Value?.ToString() ?? string.Empty);
        Assert.Contains("No capabilities known for peer", notFound.Value?.ToString() ?? string.Empty);
    }
}
