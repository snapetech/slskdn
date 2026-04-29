// <copyright file="SignalSystemControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.Native;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.API.Native;
using slskd.Signals;
using Xunit;

public class SignalSystemControllerTests
{
    [Fact]
    public void GetStatus_ReturnsOnlyEnabledConfiguredChannels()
    {
        var signalBus = new Mock<ISignalBus>();
        signalBus
            .Setup(bus => bus.GetStatistics())
            .Returns(new SignalBusStatistics
            {
                SignalsSent = 1,
                SignalsReceived = 2,
                DuplicateSignalsDropped = 3,
                ExpiredSignalsDropped = 4,
            });

        var services = new ServiceCollection()
            .AddSingleton(signalBus.Object)
            .BuildServiceProvider();

        var controller = new SignalSystemController(
            CreateOptions(
                new SignalSystemOptions
                {
                    Enabled = true,
                    MeshChannel = new SignalChannelOptions { Enabled = false },
                    BtExtensionChannel = new SignalChannelOptions { Enabled = true },
                }),
            NullLogger<SignalSystemController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };

        var result = Assert.IsType<OkObjectResult>(controller.GetStatus());
        var json = JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"active_channels\":[\"bt_extension\"]", json);
        Assert.DoesNotContain("\"mesh\"", json);
    }

    [Fact]
    public void GetStatus_WhenSignalSystemDisabled_ReturnsNoActiveChannels()
    {
        var signalBus = new Mock<ISignalBus>();
        signalBus
            .Setup(bus => bus.GetStatistics())
            .Returns(new SignalBusStatistics
            {
                SignalsSent = 1,
                SignalsReceived = 2,
                DuplicateSignalsDropped = 3,
                ExpiredSignalsDropped = 4,
            });

        var services = new ServiceCollection()
            .AddSingleton(signalBus.Object)
            .BuildServiceProvider();

        var controller = new SignalSystemController(
            CreateOptions(
                new SignalSystemOptions
                {
                    Enabled = false,
                    MeshChannel = new SignalChannelOptions { Enabled = true },
                    BtExtensionChannel = new SignalChannelOptions { Enabled = true },
                }),
            NullLogger<SignalSystemController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services },
            },
        };

        var result = Assert.IsType<OkObjectResult>(controller.GetStatus());
        var json = JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"active_channels\":[]", json);
    }

    [Fact]
    public void GetStatus_WithoutSignalBus_ReturnsNoActiveChannels()
    {
        var controller = new SignalSystemController(
            CreateOptions(
                new SignalSystemOptions
                {
                    Enabled = true,
                    MeshChannel = new SignalChannelOptions { Enabled = true },
                    BtExtensionChannel = new SignalChannelOptions { Enabled = true },
                }),
            NullLogger<SignalSystemController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().BuildServiceProvider() },
            },
        };

        var result = Assert.IsType<OkObjectResult>(controller.GetStatus());
        var json = JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"active_channels\":[]", json);
    }

    private static IOptionsMonitor<SignalSystemOptions> CreateOptions(SignalSystemOptions options)
    {
        var monitor = new Mock<IOptionsMonitor<SignalSystemOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(options);
        return monitor.Object;
    }
}
