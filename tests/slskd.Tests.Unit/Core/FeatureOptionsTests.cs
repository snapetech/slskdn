// <copyright file="FeatureOptionsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core;

using Xunit;

public class FeatureOptionsTests
{
    [Fact]
    public void Defaults_KeepScenePodBridgeOptIn()
    {
        var options = new slskd.Options();

        Assert.False(options.Feature.ScenePodBridge);
    }
}
