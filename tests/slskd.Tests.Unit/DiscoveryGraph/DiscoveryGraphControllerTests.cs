// <copyright file="DiscoveryGraphControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.DiscoveryGraph;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.DiscoveryGraph;
using slskd.DiscoveryGraph.API;
using Xunit;

public class DiscoveryGraphControllerTests
{
    [Fact]
    public async Task Build_TrimsAndNormalizesRequestBeforeDispatch()
    {
        var service = new Mock<IDiscoveryGraphService>();
        service.Setup(x => x.BuildAsync(It.IsAny<DiscoveryGraphRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryGraphResult());

        var controller = new DiscoveryGraphController(service.Object);

        var result = await controller.Build(
            new DiscoveryGraphRequest
            {
                Scope = " artist ",
                RecordingId = " rec-1 ",
                ReleaseId = " rel-1 ",
                ArtistId = " art-1 ",
                Title = " Title ",
                Artist = " Artist ",
                Album = " Album ",
                CompareNodeId = " artist:compare ",
                CompareLabel = " Compare ",
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        service.Verify(x => x.BuildAsync(
            It.Is<DiscoveryGraphRequest>(request =>
                request.Scope == "artist" &&
                request.RecordingId == "rec-1" &&
                request.ReleaseId == "rel-1" &&
                request.ArtistId == "art-1" &&
                request.Title == "Title" &&
                request.Artist == "Artist" &&
                request.Album == "Album" &&
                request.CompareNodeId == "artist:compare" &&
                request.CompareLabel == "Compare"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Build_WithBlankScope_DefaultsToSongIdRun()
    {
        var service = new Mock<IDiscoveryGraphService>();
        service.Setup(x => x.BuildAsync(It.IsAny<DiscoveryGraphRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryGraphResult());

        var controller = new DiscoveryGraphController(service.Object);

        var result = await controller.Build(
            new DiscoveryGraphRequest
            {
                Scope = "   ",
            },
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        service.Verify(x => x.BuildAsync(
            It.Is<DiscoveryGraphRequest>(request => request.Scope == "songid_run"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
