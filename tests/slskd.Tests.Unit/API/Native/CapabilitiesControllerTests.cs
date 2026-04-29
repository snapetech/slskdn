// <copyright file="CapabilitiesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.Native;

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using slskd.API.Native;
using Xunit;

public class CapabilitiesControllerTests
{
    [Fact]
    public void GetCapabilities_WhenScenePodBridgeDefaultsOff_DoesNotAdvertiseBridge()
    {
        var controller = new CapabilitiesController(
            new TestOptionsMonitor<slskd.Options>(new slskd.Options()),
            NullLogger<CapabilitiesController>.Instance);

        var result = Assert.IsType<OkObjectResult>(controller.GetCapabilities());
        var features = GetProperty<IReadOnlyCollection<string>>(result.Value!, "features");
        var feature = GetProperty<object>(result.Value!, "feature");

        Assert.DoesNotContain("scene_pod_bridge", features);
        Assert.False(GetProperty<bool>(feature, "scenePodBridge"));
    }

    [Fact]
    public void GetCapabilities_WhenScenePodBridgeEnabled_AdvertisesBridge()
    {
        var controller = new CapabilitiesController(
            new TestOptionsMonitor<slskd.Options>(new slskd.Options
            {
                Feature = new slskd.Options.FeatureOptions
                {
                    ScenePodBridge = true,
                },
            }),
            NullLogger<CapabilitiesController>.Instance);

        var result = Assert.IsType<OkObjectResult>(controller.GetCapabilities());
        var features = GetProperty<IReadOnlyCollection<string>>(result.Value!, "features");
        var feature = GetProperty<object>(result.Value!, "feature");

        Assert.Contains("scene_pod_bridge", features);
        Assert.True(GetProperty<bool>(feature, "scenePodBridge"));
    }

    private static T GetProperty<T>(object value, string name)
    {
        var property = value.GetType().GetProperty(name);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<T>(property.GetValue(value));
    }
}
