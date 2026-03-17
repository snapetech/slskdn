// <copyright file="SongIdControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SongID;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.SongID;
using slskd.SongID.API;
using Xunit;

public sealed class SongIdControllerTests
{
    [Fact]
    public async Task CreateRun_WithEmptySource_ReturnsBadRequest()
    {
        var service = new Mock<ISongIdService>(MockBehavior.Strict);
        var controller = new SongIdController(service.Object);

        var result = await controller.CreateRun(new SongIdRunRequest { Source = " " }, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("SongID source is required.", badRequest.Value);
    }

    [Fact]
    public async Task CreateRun_WithValidSource_ReturnsAcceptedRun()
    {
        var expectedRun = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "https://youtu.be/example",
            Status = "queued",
        };
        var service = new Mock<ISongIdService>();
        service
            .Setup(instance => instance.QueueAnalyzeAsync(expectedRun.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRun);

        var controller = new SongIdController(service.Object);

        var result = await controller.CreateRun(new SongIdRunRequest { Source = expectedRun.Source }, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var run = Assert.IsType<SongIdRun>(accepted.Value);
        Assert.Equal(expectedRun.Id, run.Id);
        Assert.Equal("queued", run.Status);
    }

    [Fact]
    public void ListRuns_ReturnsOkWithRequestedLimit()
    {
        var runs = new List<SongIdRun>
        {
            new() { Id = Guid.NewGuid(), Source = "first" },
            new() { Id = Guid.NewGuid(), Source = "second" },
        };
        var service = new Mock<ISongIdService>();
        service.Setup(instance => instance.List(2)).Returns(runs);

        var controller = new SongIdController(service.Object);

        var result = controller.ListRuns(2);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<SongIdRun>>(ok.Value);
        Assert.Equal(2, payload.Count);
    }

    [Fact]
    public void GetRun_WhenMissing_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        var service = new Mock<ISongIdService>();
        service.Setup(instance => instance.Get(id)).Returns((SongIdRun?)null);

        var controller = new SongIdController(service.Object);

        var result = controller.GetRun(id);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetRun_WhenFound_ReturnsOk()
    {
        var run = new SongIdRun
        {
            Id = Guid.NewGuid(),
            Source = "/srv/music/example.flac",
            Status = "completed",
        };
        var service = new Mock<ISongIdService>();
        service.Setup(instance => instance.Get(run.Id)).Returns(run);

        var controller = new SongIdController(service.Object);

        var result = controller.GetRun(run.Id);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<SongIdRun>(ok.Value);
        Assert.Equal(run.Id, payload.Id);
        Assert.Equal("completed", payload.Status);
    }
}
