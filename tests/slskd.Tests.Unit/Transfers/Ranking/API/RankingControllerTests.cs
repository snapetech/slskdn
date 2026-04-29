// <copyright file="RankingControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.Ranking.API;

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Transfers.Ranking;
using slskd.Transfers.Ranking.API;
using Xunit;

public class RankingControllerTests
{
    [Fact]
    public async Task GetHistories_WithOnlyWhitespaceUsernames_ReturnsBadRequest()
    {
        var rankingService = new Mock<ISourceRankingService>();
        var controller = new RankingController(rankingService.Object);

        var result = await controller.GetHistories(new List<string> { "   ", "\t" });

        Assert.IsType<BadRequestObjectResult>(result);
        rankingService.Verify(
            service => service.GetHistoriesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetHistories_TrimsUsernamesBeforeLookup()
    {
        var rankingService = new Mock<ISourceRankingService>();
        rankingService
            .Setup(service => service.GetHistoriesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, UserDownloadHistory>());

        var controller = new RankingController(rankingService.Object);

        var result = await controller.GetHistories(new List<string> { " alice ", "bob" });

        Assert.IsType<OkObjectResult>(result);
        rankingService.Verify(
            service => service.GetHistoriesAsync(
                It.Is<IEnumerable<string>>(usernames => usernames.SequenceEqual(new[] { "alice", "bob" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetHistories_FiltersDuplicateAndBlankUsernamesBeforeLookup()
    {
        var rankingService = new Mock<ISourceRankingService>();
        rankingService
            .Setup(service => service.GetHistoriesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, UserDownloadHistory>());

        var controller = new RankingController(rankingService.Object);

        var result = await controller.GetHistories(new List<string> { " alice ", "alice", "   ", "bob" });

        Assert.IsType<OkObjectResult>(result);
        rankingService.Verify(
            service => service.GetHistoriesAsync(
                It.Is<IEnumerable<string>>(usernames => usernames.SequenceEqual(new[] { "alice", "bob" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RankSources_WithWhitespaceCandidateFields_ReturnsBadRequest()
    {
        var rankingService = new Mock<ISourceRankingService>();
        var controller = new RankingController(rankingService.Object);

        var result = await controller.RankSources(new List<SourceCandidate>
        {
            new() { Username = "alice", Filename = "song.flac" },
            new() { Username = "   ", Filename = "song2.flac" }
        });

        Assert.IsType<BadRequestObjectResult>(result);
        rankingService.Verify(
            service => service.RankSourcesAsync(It.IsAny<IEnumerable<SourceCandidate>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RankSources_TrimsCandidateFieldsBeforeRanking()
    {
        var rankingService = new Mock<ISourceRankingService>();
        rankingService
            .Setup(service => service.RankSourcesAsync(It.IsAny<IEnumerable<SourceCandidate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RankedSource>());

        var controller = new RankingController(rankingService.Object);

        var result = await controller.RankSources(new List<SourceCandidate>
        {
            new() { Username = " alice ", Filename = " song.flac ", Size = 123 }
        });

        Assert.IsType<OkObjectResult>(result);
        rankingService.Verify(
            service => service.RankSourcesAsync(
                It.Is<IEnumerable<SourceCandidate>>(candidates =>
                    candidates.Single().Username == "alice" &&
                    candidates.Single().Filename == "song.flac"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RankSources_FiltersDuplicateCandidatesBeforeRanking()
    {
        var rankingService = new Mock<ISourceRankingService>();
        rankingService
            .Setup(service => service.RankSourcesAsync(It.IsAny<IEnumerable<SourceCandidate>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RankedSource>());

        var controller = new RankingController(rankingService.Object);

        var result = await controller.RankSources(new List<SourceCandidate>
        {
            new() { Username = " alice ", Filename = " song.flac ", Size = 123 },
            new() { Username = "alice", Filename = "song.flac", Size = 123 }
        });

        Assert.IsType<OkObjectResult>(result);
        rankingService.Verify(
            service => service.RankSourcesAsync(
                It.Is<IEnumerable<SourceCandidate>>(candidates => candidates.Count() == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
