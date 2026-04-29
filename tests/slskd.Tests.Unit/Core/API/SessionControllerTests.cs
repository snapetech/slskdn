// <copyright file="SessionControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Core.API;

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http;
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
        ResetLoginState();
        var controller = CreateController(headless: true);

        var result = controller.Login(null!);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public void Login_TrimsCredentialsBeforeAuthenticationAndJwtCreation()
    {
        ResetLoginState();
        var security = new Mock<ISecurityService>();
        security
            .Setup(service => service.AuthenticateAdminCredentials("admin", "secret"))
            .Returns(true);
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
        security.Verify(service => service.AuthenticateAdminCredentials("admin", "secret"), Times.Once);
        security.Verify(service => service.GenerateJwt("admin", Role.Administrator, null), Times.Once);
    }

    [Fact]
    public void Login_TrimsConfiguredCredentialsBeforeComparison()
    {
        ResetLoginState();
        var security = new Mock<ISecurityService>();
        security
            .Setup(service => service.AuthenticateAdminCredentials("admin", "secret"))
            .Returns(true);
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
        security.Verify(service => service.AuthenticateAdminCredentials("admin", "secret"), Times.Once);
        security.Verify(service => service.GenerateJwt("admin", Role.Administrator, null), Times.Once);
    }

    [Fact]
    public void Login_LocksOutUsernameAcrossDifferentIps()
    {
        ResetLoginState();
        var security = new Mock<ISecurityService>();
        security.Setup(service => service.AuthenticateAdminCredentials("admin", "wrong")).Returns(false);
        var controller = CreateController(security: security);
        const int maxFailures = 5;

        for (var attempt = 0; attempt < maxFailures; attempt++)
        {
            controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse($"203.0.113.{attempt + 1}");
            var result = controller.Login(new LoginRequest
            {
                Username = "admin",
                Password = "wrong",
            });

            Assert.IsType<UnauthorizedResult>(result);
        }

        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.250");
        var lockout = controller.Login(new LoginRequest
        {
            Username = "admin",
            Password = "wrong",
        });

        var objectResult = Assert.IsType<ObjectResult>(lockout);
        Assert.Equal(429, objectResult.StatusCode);
        Assert.Equal("Too many failed login attempts. Try again later.", objectResult.Value);
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

        var controller = new SessionController(
            security.Object,
            optionsSnapshot.Object,
            new OptionsAtStartup { Headless = headless });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.1");
        return controller;
    }

    private static void ResetLoginState()
    {
        ClearConcurrentDictionary("_loginAttempts");
        ClearConcurrentDictionary("_userLoginAttempts");
    }

    private static void ClearConcurrentDictionary(string fieldName)
    {
        var field = typeof(SessionController).GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var dictionary = field!.GetValue(null) as IDictionary;
        Assert.NotNull(dictionary);
        dictionary!.Clear();
    }
}
