// <copyright file="BridgeAdminControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.VirtualSoulfind;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.VirtualSoulfind;
using slskd.VirtualSoulfind.Bridge;
using Xunit;

public class BridgeAdminControllerTests
{
    [Fact]
    public async Task GetDashboard_WhenDashboardThrows_DoesNotLeakExceptionMessage()
    {
        var dashboard = new Mock<IBridgeDashboard>();
        dashboard
            .Setup(service => service.GetDashboardDataAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new BridgeAdminController(
            NullLogger<BridgeAdminController>.Instance,
            Mock.Of<ISoulfindBridgeService>(),
            dashboard.Object,
            Mock.Of<IOptionsMonitor<slskd.Options>>());

        var result = await controller.GetDashboard(CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetStats_WhenDashboardThrows_DoesNotLeakExceptionMessage()
    {
        var dashboard = new Mock<IBridgeDashboard>();
        dashboard
            .Setup(service => service.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new BridgeAdminController(
            NullLogger<BridgeAdminController>.Instance,
            Mock.Of<ISoulfindBridgeService>(),
            dashboard.Object,
            Mock.Of<IOptionsMonitor<slskd.Options>>());

        var result = await controller.GetStats(CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
    }
}
