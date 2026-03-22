// <copyright file="JobsControllerBoundaryTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Native;

using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.Native;
using slskd.Integrations.MusicBrainz;
using slskd.Jobs;
using Xunit;

public class JobsControllerBoundaryTests
{
    [Fact]
    public async Task CreateDiscographyJob_WithBlankArtistId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.CreateDiscographyJob(
            new DiscographyJobRequest { ArtistId = "   " },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateDiscographyJob_TrimsAndDeduplicatesReleaseIds()
    {
        var discographyService = new Mock<IDiscographyJobService>();
        discographyService
            .Setup(service => service.CreateJobAsync(It.IsAny<DiscographyJobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-123");

        var controller = CreateController(discographyService: discographyService);

        var result = await controller.CreateDiscographyJob(
            new DiscographyJobRequest
            {
                ArtistId = " artist-1 ",
                TargetDirectory = " /tmp/music ",
                ReleaseIds = new List<string> { " rel-1 ", "rel-1", " ", "rel-2" }
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        discographyService.Verify(
            service => service.CreateJobAsync(
                It.Is<DiscographyJobRequest>(request =>
                    request.ArtistId == "artist-1" &&
                    request.TargetDirectory == "/tmp/music" &&
                    request.ReleaseIds!.SequenceEqual(new[] { "rel-1", "rel-2" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateLabelCrateJob_WithNoLabelFields_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.CreateLabelCrateJob(
            new LabelCrateJobRequest { LabelId = " ", LabelName = " " },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetJob_WithBlankId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetJob("   ", CancellationToken.None);

        Assert.IsType<BadRequestResult>(result);
    }

    private static JobsController CreateController(
        Mock<IDiscographyJobService>? discographyService = null,
        Mock<ILabelCrateJobService>? labelCrateService = null,
        Mock<IMusicBrainzClient>? musicBrainzClient = null)
    {
        var controller = new JobsController(
            (discographyService ?? new Mock<IDiscographyJobService>()).Object,
            (labelCrateService ?? new Mock<ILabelCrateJobService>()).Object,
            (musicBrainzClient ?? new Mock<IMusicBrainzClient>()).Object,
            NullLogger<JobsController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "testuser")
                }, "test"))
            }
        };

        return controller;
    }
}
