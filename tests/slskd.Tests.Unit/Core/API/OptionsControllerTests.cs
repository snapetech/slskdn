// <copyright file="OptionsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Core.API;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Core.API;
using Xunit;

public class OptionsControllerTests
{
    [Fact]
    public void ApplyOverlay_WithNullBody_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.ApplyOverlay(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Options overlay is required", badRequest.Value);
    }

    [Fact]
    public void ValidateYamlFile_WithNullBody_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.ValidateYamlFile(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("YAML is required", badRequest.Value);
    }

    [Fact]
    public void ValidateYamlFile_WithMalformedYaml_DoesNotLeakParserExceptionMessage()
    {
        var controller = CreateController();

        var result = controller.ValidateYamlFile(":\n  : bad");

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.DoesNotContain("Yaml", ok.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Invalid YAML configuration", ok.Value);
    }

    private static OptionsController CreateController(bool remoteConfiguration = true)
    {
        var options = new slskd.Options
        {
            RemoteConfiguration = remoteConfiguration,
        };

        var optionsSnapshot = new Mock<IOptionsSnapshot<slskd.Options>>();
        optionsSnapshot.SetupGet(snapshot => snapshot.Value).Returns(options);

        var stateMutator = new Mock<IStateMutator<State>>();

        return new OptionsController(
            new OptionsAtStartup(),
            optionsSnapshot.Object,
            stateMutator.Object);
    }
}
