// <copyright file="SearchesControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Search.API;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Search;
using slskd.Search.API;
using Soulseek;
using Xunit;

public class SearchesControllerTests
{
    [Fact]
    public async Task Post_WithNullRequest_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.Post(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Post_TrimsSearchTextAndProvidersBeforeDispatch()
    {
        var searchService = new Mock<ISearchService>();
        searchService
            .Setup(service => service.StartAsync(
                It.IsAny<Guid>(),
                It.IsAny<SearchQuery>(),
                It.IsAny<SearchScope>(),
                It.IsAny<SearchOptions>(),
                It.IsAny<List<string>>()))
            .ReturnsAsync(new slskd.Search.Search { Id = Guid.NewGuid(), SearchText = "hello world" });

        var controller = CreateController(searchService);

        var result = await controller.Post(new SearchRequest
        {
            SearchText = "  hello world  ",
            Providers = new List<string> { " pod ", "pod", " scene ", " " }
        });

        Assert.IsType<OkObjectResult>(result);
        searchService.Verify(
            service => service.StartAsync(
                It.IsAny<Guid>(),
                It.Is<SearchQuery>(query => query.Terms.SequenceEqual(new[] { "hello", "world" })),
                It.IsAny<SearchScope>(),
                It.IsAny<SearchOptions>(),
                It.Is<List<string>>(providers => providers.SequenceEqual(new[] { "pod", "scene" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAll_WithNegativeLimit_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.GetAll(limit: -1, offset: 0);

        Assert.IsType<BadRequestResult>(result);
    }

    private static SearchesController CreateController(Mock<ISearchService>? searchService = null)
    {
        return new SearchesController(
            (searchService ?? new Mock<ISearchService>()).Object,
            Mock.Of<IOptionsSnapshot<slskd.Options>>());
    }
}
