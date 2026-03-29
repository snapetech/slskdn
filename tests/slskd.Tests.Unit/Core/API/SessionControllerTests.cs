// <copyright file="SessionControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Core.API;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Authentication;
using slskd.Core.API;
using slskd.Core.Security;
using Xunit;

public class SessionControllerTests
{
    [Fact]
    public void Login_WhenRequestIsNullInHeadlessMode_ReturnsBadRequest()
    {
        var controller = CreateController(headless: true);

        var result = controller.Login(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public void Login_TrimsCredentialsBeforeAuthenticationAndJwtCreation()
    {
        var security = new Mock<ISecurityService>();
        security
            .Setup(service => service.GenerateJwt(It.IsAny<string>(), It.IsAny<Role>(), It.IsAny<int?>()))
            .Returns(new JwtSecurityToken());

        var controller = CreateController(security: security);

        var result = controller.Login(new LoginRequest
        {
            Username = " admin ",
            Password = " secret ",
        });

        Assert.IsType<OkObjectResult>(result);
        security.Verify(service => service.GenerateJwt("admin", Role.Administrator, null), Times.Once);
    }

    [Fact]
    public void Login_TrimsConfiguredCredentialsBeforeComparison()
    {
        var security = new Mock<ISecurityService>();
        security
            .Setup(service => service.GenerateJwt(It.IsAny<string>(), It.IsAny<Role>(), It.IsAny<int?>()))
            .Returns(new JwtSecurityToken());

        var controller = CreateController(
            security: security,
            configuredUsername: " admin ",
            configuredPassword: " secret ");

        var result = controller.Login(new LoginRequest
        {
            Username = "admin",
            Password = "secret",
        });

        Assert.IsType<OkObjectResult>(result);
        security.Verify(service => service.GenerateJwt("admin", Role.Administrator, null), Times.Once);
    }

    private static SessionController CreateController(
        bool headless = false,
        Mock<ISecurityService>? security = null,
        string configuredUsername = "admin",
        string configuredPassword = "secret")
    {
        security ??= new Mock<ISecurityService>();

        var options = new slskd.Options
        {
            Web = new slskd.Options.WebOptions
            {
                Authentication = new slskd.Options.WebOptions.WebAuthenticationOptions
                {
                    Disabled = false,
                    Username = configuredUsername,
                    Password = configuredPassword,
                },
            },
        };

        var optionsSnapshot = new Mock<IOptionsSnapshot<slskd.Options>>();
        optionsSnapshot.SetupGet(snapshot => snapshot.Value).Returns(options);

        return new SessionController(
            security.Object,
            optionsSnapshot.Object,
            new OptionsAtStartup { Headless = headless });
    }
}
