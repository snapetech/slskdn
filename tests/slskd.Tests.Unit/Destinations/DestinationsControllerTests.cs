// <copyright file="DestinationsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Destinations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Destinations.API;
using Xunit;

public class DestinationsControllerTests
{
    [Fact]
    public void Validate_WithNullRequest_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.Validate(null!);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Path is required", bad.Value);
    }

    [Fact]
    public void Validate_WithBlankPath_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = controller.Validate(new ValidateDestinationRequest { Path = "   " });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Path is required", bad.Value);
    }

    [Fact]
    public void Validate_TrimsPathBeforeNormalization()
    {
        var root = Path.Combine(Path.GetTempPath(), "slskdn-dest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var controller = CreateController(root);

            var result = controller.Validate(new ValidateDestinationRequest
            {
                Path = $" {root} ",
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ValidateDestinationResponse>(ok.Value);
            Assert.Equal(root, response.Path);
            Assert.True(response.Exists);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static DestinationsController CreateController(string? downloadsRoot = null)
    {
        var options = new slskd.Options
        {
            Directories = new slskd.Options.DirectoriesOptions
            {
                Downloads = downloadsRoot ?? Path.GetTempPath(),
            },
            Destinations = new slskd.Options.DestinationsOptions(),
        };

        var snapshot = new Mock<IOptionsSnapshot<slskd.Options>>();
        snapshot.Setup(x => x.Value).Returns(options);

        return new DestinationsController(snapshot.Object);
    }
}
