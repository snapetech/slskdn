// <copyright file="SearchCompatibilityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Compatibility;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.API.Compatibility;
using slskd.Search;
using Soulseek;
using Xunit;

public class SearchCompatibilityControllerTests
{
    [Fact]
    public async Task Search_WithNullRequest_ReturnsBadRequest()
    {
        var controller = new SearchCompatibilityController(
            Mock.Of<ISearchService>(),
            NullLogger<SearchCompatibilityController>.Instance);

        var result = await controller.Search(null!, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Search_TrimsQueryBeforeDispatch()
    {
        var searchService = new Mock<ISearchService>();
        searchService
            .Setup(service => service.StartAsync(
                It.IsAny<Guid>(),
                It.IsAny<SearchQuery>(),
                It.IsAny<SearchScope>(),
                It.IsAny<SearchOptions>()))
            .ReturnsAsync(new Search
            {
                Id = Guid.NewGuid(),
                SearchText = "hello world",
                Responses = new List<SearchResponse>()
            });

        var controller = new SearchCompatibilityController(
            searchService.Object,
            NullLogger<SearchCompatibilityController>.Instance);

        var result = await controller.Search(new SearchRequest("  hello world  ", null, 10), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        searchService.Verify(
            service => service.StartAsync(
                It.IsAny<Guid>(),
                It.Is<SearchQuery>(query => query.Terms == "hello world"),
                It.IsAny<SearchScope>(),
                It.IsAny<SearchOptions>()),
            Times.Once);
    }
}

