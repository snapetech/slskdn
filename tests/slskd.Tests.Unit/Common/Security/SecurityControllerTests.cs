// <copyright file="SecurityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Common.Security;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Common.Security;
using slskd.Common.Security.API;
using Xunit;

public class SecurityControllerTests
{
    [Fact]
    public async Task BuildCircuit_DoesNotLeak_Exception_Message()
    {
        var circuitBuilder = new Mock<slskd.Mesh.IMeshCircuitBuilder>();
        circuitBuilder
            .Setup(x => x.BuildCircuitAsync("peer-1", It.IsAny<int?>()))
            .ThrowsAsync(new InvalidOperationException("secret failure"));

        var controller = CreateController(circuitBuilder: circuitBuilder.Object);

        var result = await controller.BuildCircuit(new BuildCircuitRequest
        {
            TargetPeerId = " peer-1 ",
        });

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, objectResult.StatusCode);
        Assert.DoesNotContain("secret failure", objectResult.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public void GetAdversarialSettings_Has_Route_And_Returns_Ok_When_Configured()
    {
        var settings = new AdversarialOptions { Enabled = true };
        var controller = CreateController(adversarialOptions: settings);

        var result = controller.GetAdversarialSettings();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(settings, ok.Value);
    }

    [Fact]
    public void GetEvents_With_NonPositive_Count_Returns_BadRequest()
    {
        var controller = CreateController();

        var result = controller.GetEvents(0, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void BanUsername_Trims_Request_Before_Banning()
    {
        var services = new SecurityServices
        {
            ViolationTracker = new ViolationTracker(NullLogger<ViolationTracker>.Instance),
        };

        var controller = CreateController(security: services);

        var result = controller.BanUsername(new BanUsernameRequest
        {
            Username = " user-1 ",
            Reason = " noisy ",
        });

        Assert.IsType<OkResult>(result);
        Assert.Single(services.ViolationTracker.GetActiveBans());
    }

    [Fact]
    public void DestroyCircuit_With_Blank_CircuitId_Returns_BadRequest()
    {
        var controller = CreateController(circuitBuilder: Mock.Of<slskd.Mesh.IMeshCircuitBuilder>());

        var result = controller.DestroyCircuit("   ");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static SecurityController CreateController(
        SecurityServices? security = null,
        AdversarialOptions? adversarialOptions = null,
        slskd.Mesh.IMeshCircuitBuilder? circuitBuilder = null)
    {
        var optionsSnapshot = new Mock<IOptionsSnapshot<slskd.Options>>();
        optionsSnapshot.Setup(x => x.Value).Returns(new slskd.Options());

        return new SecurityController(
            security,
            security?.EventSink,
            adversarialOptions,
            null,
            circuitBuilder,
            null,
            optionsSnapshot.Object,
            NullLogger<SecurityController>.Instance);
    }
}
