// <copyright file="PodsControllerContractTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.API.Native;

using System.Linq;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using slskd.API.Native;
using Xunit;

public class PodsControllerContractTests
{
    [Fact]
    public void Controller_UsesVersionedApiRoute()
    {
        var route = typeof(PodsController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: true)
            .Cast<RouteAttribute>()
            .Single();

        Assert.Equal("api/v{version:apiVersion}/pods", route.Template);
    }

    [Fact]
    public void Controller_DeclaresApiVersionZero()
    {
        var apiVersion = typeof(PodsController)
            .GetCustomAttributes(typeof(ApiVersionAttribute), inherit: true)
            .Cast<ApiVersionAttribute>()
            .Single();

        Assert.Contains(apiVersion.Versions, version => version.MajorVersion == 0);
    }
}
