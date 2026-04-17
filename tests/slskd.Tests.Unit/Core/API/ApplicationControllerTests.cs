// <copyright file="ApplicationControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Core.API;

using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using slskd;
using slskd.Core.API;
using Xunit;

public class ApplicationControllerTests
{

    [Fact]
    public void State_ExposesRuntimeIdentityForTheRunningProcess()
    {
        var originalAppDirectory = Program.AppDirectory;
        var originalConfigurationFile = Program.ConfigurationFile;

        try
        {
            SetProgramValue(nameof(Program.AppDirectory), "/tmp/slskdn-app");
            SetProgramValue(nameof(Program.ConfigurationFile), "/tmp/slskdn-app/slskd.yml");

            var state = new State();

            Assert.Equal(Program.SemanticVersion, state.Version.Current);
            Assert.Equal(Program.ExecutablePath, state.Runtime.ExecutablePath);
            Assert.Equal(Program.BaseDirectory, state.Runtime.BaseDirectory);
            Assert.Equal("/tmp/slskdn-app", state.Runtime.AppDirectory);
            Assert.Equal("/tmp/slskdn-app/slskd.yml", state.Runtime.ConfigurationFile);
            Assert.Equal(Program.ProcessId, state.Runtime.ProcessId);
        }
        finally
        {
            SetProgramValue(nameof(Program.AppDirectory), originalAppDirectory);
            SetProgramValue(nameof(Program.ConfigurationFile), originalConfigurationFile);
        }
    }

    [Fact]
    public void Loopback_WithNullBody_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.Loopback(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Body is required", badRequest.Value);
    }


    private static void SetProgramValue(string propertyName, string value)
    {
        var field = typeof(Program).GetField($"<{propertyName}>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(null, value);
    }

    private static ApplicationController CreateController()
    {
        var optionsMonitor = new Mock<IOptionsMonitor<slskd.Options>>();
        optionsMonitor.SetupGet(m => m.CurrentValue).Returns(new slskd.Options());

        var application = new Mock<IApplication>();
        var lifetime = new Mock<IHostApplicationLifetime>();
        var stateMonitor = new Mock<IStateMonitor<State>>();
        stateMonitor.SetupGet(m => m.CurrentValue).Returns(new State());

        return new ApplicationController(
            lifetime.Object,
            application.Object,
            optionsMonitor.Object,
            stateMonitor.Object);
    }
}
