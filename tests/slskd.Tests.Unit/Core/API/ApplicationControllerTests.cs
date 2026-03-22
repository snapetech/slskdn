// <copyright file="ApplicationControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Core.API;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Core.API;
using Xunit;

public class ApplicationControllerTests
{
    [Fact]
    public void Loopback_WithNullBody_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.Loopback(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Body is required", badRequest.Value);
    }

    private static ApplicationController CreateController()
    {
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.SetupGet(m => m.CurrentValue).Returns(new slskd.Options());

        var application = new Mock<IApplication>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var stateMonitor = new Mock<IStateMonitor<State>>();
        stateMonitor.SetupGet(m => m.CurrentValue).Returns(new State());

        return new ApplicationController(
            lifetime.Object,
            application.Object,
            optionsMonitor.Object,
            stateMonitor.Object);
    }
}
