// <copyright file="PodMembershipControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.PodCore;

using System.Linq;
using Microsoft.AspNetCore.Authorization;
using slskd.Core.Security;
using slskd.PodCore.API.Controllers;
using Xunit;

public class PodMembershipControllerTests
{
    [Fact]
    public void Controller_RequiresAuthenticatedAccess()
    {
        var authorize = typeof(PodMembershipController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(AuthPolicy.Any, authorize.Policy);
    }
}
