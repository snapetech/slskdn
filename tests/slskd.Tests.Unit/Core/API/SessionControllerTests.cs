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
            .Setup(service => service.GenerateJwt(It.IsAny<string>(), It.IsAny<Role>()))
            .Returns(new JwtSecurityToken());

        var controller = CreateController(security: security);

        var result = controller.Login(new LoginRequest
        {
            Username = " admin ",
            Password = " secret ",
        });

        Assert.IsType<OkObjectResult>(result);
        security.Verify(service => service.GenerateJwt("admin", Role.Administrator), Times.Once);
    }

    private static SessionController CreateController(bool headless = false, Mock<ISecurityService>? security = null)
    {
        security ??= new Mock<ISecurityService>();

        var options = new Options
        {
            Web = new WebOptions
            {
                Authentication = new WebOptions.WebAuthenticationOptions
                {
                    Disabled = false,
                    Username = "admin",
                    Password = "secret",
                },
            },
        };

        var optionsSnapshot = new Mock<IOptionsSnapshot<Options>>();
        optionsSnapshot.SetupGet(snapshot => snapshot.Value).Returns(options);

        return new SessionController(
            security.Object,
            optionsSnapshot.Object,
            new OptionsAtStartup { Headless = headless });
    }
}
