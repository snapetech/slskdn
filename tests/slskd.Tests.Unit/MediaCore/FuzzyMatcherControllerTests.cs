// <copyright file="FuzzyMatcherControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class FuzzyMatcherControllerTests
{
    [Fact]
    public async Task ComputePerceptualSimilarity_RejectsOutOfRangeThreshold()
    {
        var controller = CreateController();

        var result = await controller.ComputePerceptualSimilarity(
            new PerceptualSimilarityRequest("content:a", "content:b", 1.5),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ComputePerceptualSimilarity_TrimsContentIdsBeforeDispatch()
    {
        var matcher = new Mock<IFuzzyMatcher>();
        matcher
            .Setup(service => service.ScorePerceptualAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IContentIdRegistry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.8);

        var controller = CreateController(matcher);

        var result = await controller.ComputePerceptualSimilarity(
            new PerceptualSimilarityRequest(" content:a ", " content:b "),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        matcher.Verify(
            service => service.ScorePerceptualAsync("content:a", "content:b", It.IsAny<IContentIdRegistry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FindSimilarContent_RejectsNonPositiveLimits()
    {
        var controller = CreateController();

        var result = await controller.FindSimilarContent(
            "content:a",
            new FindSimilarRequest(0.7, 0, 10),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ComputeTextSimilarity_TrimsTextInputs()
    {
        var matcher = new Mock<IFuzzyMatcher>();
        matcher.Setup(service => service.ScoreLevenshtein("hello", "world")).Returns(0.5);
        matcher.Setup(service => service.ScorePhonetic("hello", "world")).Returns(0.25);

        var controller = CreateController(matcher);

        var result = controller.ComputeTextSimilarity(new TextSimilarityRequest(" hello ", " world "));

        Assert.IsType<OkObjectResult>(result);
        matcher.Verify(service => service.ScoreLevenshtein("hello", "world"), Times.Once);
        matcher.Verify(service => service.ScorePhonetic("hello", "world"), Times.Once);
    }

    private static FuzzyMatcherController CreateController(Mock<IFuzzyMatcher>? matcher = null)
    {
        return new FuzzyMatcherController(
            NullLogger<FuzzyMatcherController>.Instance,
            (matcher ?? new Mock<IFuzzyMatcher>()).Object,
            Mock.Of<IContentIdRegistry>());
    }
}
