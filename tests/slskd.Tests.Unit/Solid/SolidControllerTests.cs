// <copyright file="SolidControllerTests.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Solid.API;

using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Solid;
using slskd.Solid.API;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;
using Xunit;

public class SolidControllerTests
{
    private readonly Mock<ISolidWebIdResolver> _resolverMock = new();
    private IOptionsMonitor<slskd.Options> _options;

    private SolidController CreateController()
    {
        _options ??= new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { Solid = true },
            Solid = new slskd.Options.SolidOptions
            {
                ClientIdUrl = "/solid/clientid.jsonld",
                RedirectPath = "/solid/callback"
            }
        });

        var c = new SolidController(_options, _resolverMock.Object);
        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        c.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "u") }, "Test"));
        return c;
    }

    [Fact]
    public async Task Status_FeatureDisabled_ReturnsNotFound()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { Solid = false }
        });
        var c = CreateController();

        var r = await Task.FromResult(c.Status());

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task Status_FeatureEnabled_ReturnsStatus()
    {
        var c = CreateController();

        var r = await Task.FromResult(c.Status());

        var ok = Assert.IsType<OkObjectResult>(r);
        var result = ok.Value as dynamic;
        Assert.True(result.enabled);
        Assert.Equal("/solid/clientid.jsonld", result.clientId.ToString());
        Assert.Equal("/solid/callback", result.redirectPath.ToString());
    }

    [Fact]
    public async Task ResolveWebId_FeatureDisabled_ReturnsNotFound()
    {
        _options = new TestOptionsMonitor(new slskd.Options
        {
            Feature = new slskd.Options.FeatureOptions { Solid = false }
        });
        var c = CreateController();

        var r = await c.ResolveWebId(new SolidController.ResolveWebIdRequest { WebId = "https://example.com/profile#me" }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(r);
    }

    [Fact]
    public async Task ResolveWebId_InvalidUri_ReturnsBadRequest()
    {
        var c = CreateController();

        var r = await c.ResolveWebId(new SolidController.ResolveWebIdRequest { WebId = "not-a-uri" }, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(r);
        Assert.Equal(400, problem.StatusCode);
        var problemDetails = problem.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        Assert.Contains("Invalid WebID", problemDetails?.Title);
    }

    [Fact]
    public async Task ResolveWebId_SSRFBlocked_ReturnsBadRequest()
    {
        var c = CreateController();
        _resolverMock.Setup(x => x.ResolveAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Solid fetch blocked: host 'example.com' not in AllowedHosts."));

        var r = await c.ResolveWebId(new SolidController.ResolveWebIdRequest { WebId = "https://example.com/profile#me" }, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(r);
        Assert.Equal(400, problem.StatusCode);
        var problemDetails = problem.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        Assert.Contains("Solid fetch blocked", problemDetails?.Title);
    }

    [Fact]
    public async Task ResolveWebId_Success_ReturnsOidcIssuers()
    {
        var c = CreateController();
        var webId = new Uri("https://example.com/profile#me");
        var profile = new SolidWebIdProfile(webId, new[] { new Uri("https://issuer.example") });
        _resolverMock.Setup(x => x.ResolveAsync(webId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var r = await c.ResolveWebId(new SolidController.ResolveWebIdRequest { WebId = webId.ToString() }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(r);
        var result = ok.Value as dynamic;
        Assert.Equal(webId.ToString(), result.webId.ToString());
        Assert.Single((string[])result.oidcIssuers);
        // URI.ToString() may add trailing slash, so check Contains or trim
        var issuer = ((string[])result.oidcIssuers)[0];
        Assert.True(issuer == "https://issuer.example" || issuer == "https://issuer.example/", $"Expected 'https://issuer.example' or 'https://issuer.example/', got '{issuer}'");
    }

    [Fact]
    public async Task ResolveWebId_GeneralException_ReturnsInternalServerError()
    {
        var c = CreateController();
        _resolverMock.Setup(x => x.ResolveAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        var r = await c.ResolveWebId(new SolidController.ResolveWebIdRequest { WebId = "https://example.com/profile#me" }, CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(r);
        Assert.Equal(500, problem.StatusCode);
        var problemDetails = problem.Value as Microsoft.AspNetCore.Mvc.ProblemDetails;
        Assert.Contains("Failed to resolve WebID", problemDetails?.Title);
    }
}
