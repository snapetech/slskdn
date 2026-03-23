// <copyright file="JobApiControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Jobs;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Jobs;
using slskd.Jobs.API;
using Xunit;

public class JobApiControllerTests
{
    [Fact]
    public async Task DiscographyCreate_TrimsAndDeduplicatesRequestBeforeDispatch()
    {
        var service = new Mock<IDiscographyJobService>();
        service.Setup(x => x.CreateJobAsync(It.IsAny<DiscographyJobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-1");
        service.Setup(x => x.GetJobAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscographyJob { JobId = "job-1" });

        var controller = new DiscographyJobsController(service.Object);

        var result = await controller.Create(new DiscographyJobRequest
        {
            ArtistId = " artist-1 ",
            TargetDirectory = " /tmp/music ",
            ReleaseIds = new List<string> { " rel-1 ", "rel-1", " ", "rel-2" },
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(
            x => x.CreateJobAsync(
                It.Is<DiscographyJobRequest>(request =>
                    request.ArtistId == "artist-1" &&
                    request.TargetDirectory == "/tmp/music" &&
                    request.ReleaseIds!.SequenceEqual(new[] { "rel-1", "rel-2" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DiscographyGet_WithBlankJobId_ReturnsBadRequest()
    {
        var controller = new DiscographyJobsController(Mock.Of<IDiscographyJobService>());

        var result = await controller.Get("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task LabelCrateCreate_TrimsFieldsBeforeDispatch()
    {
        var service = new Mock<ILabelCrateJobService>();
        service.Setup(x => x.CreateJobAsync(It.IsAny<LabelCrateJobRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("job-1");
        service.Setup(x => x.GetJobAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LabelCrateJob { JobId = "job-1" });

        var controller = new LabelCrateJobsController(service.Object);

        var result = await controller.Create(new LabelCrateJobRequest
        {
            LabelId = " label-1 ",
            LabelName = " Warp ",
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        service.Verify(
            x => x.CreateJobAsync(
                It.Is<LabelCrateJobRequest>(request =>
                    request.LabelId == "label-1" &&
                    request.LabelName == "Warp"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LabelCrateGet_WithBlankJobId_ReturnsBadRequest()
    {
        var controller = new LabelCrateJobsController(Mock.Of<ILabelCrateJobService>());

        var result = await controller.Get("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
