// <copyright file="HashDbControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.HashDb.API;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.HashDb;
using slskd.HashDb.API;
using slskd.HashDb.Optimization;
using slskd.Search;
using Xunit;

public class HashDbControllerTests
{
    [Fact]
    public async Task OptimizeIndexes_WhenOptimizationThrows_DoesNotLeakExceptionMessage()
    {
        var optimizationService = new Mock<IHashDbOptimizationService>();
        optimizationService
            .Setup(service => service.OptimizeIndexesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new HashDbController(
            Mock.Of<IHashDbService>(),
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            optimizationService.Object);

        var result = await controller.OptimizeIndexes();

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to optimize indexes", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task GetSlowQueries_WhenOptimizationThrows_DoesNotLeakExceptionMessage()
    {
        var optimizationService = new Mock<IHashDbOptimizationService>();
        optimizationService
            .Setup(service => service.GetSlowQueryStatsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive detail"));

        var controller = new HashDbController(
            Mock.Of<IHashDbService>(),
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            optimizationService.Object);

        var result = await controller.GetSlowQueries();

        var error = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, error.StatusCode);
        Assert.DoesNotContain("sensitive detail", error.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to get slow query stats", error.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task ProfileQuery_WithDisallowedQuery_DoesNotLeakValidationMessage()
    {
        var controller = new HashDbController(
            Mock.Of<IHashDbService>(),
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            Mock.Of<IHashDbOptimizationService>());

        var result = await controller.ProfileQuery(new ProfileQueryRequest
        {
            Query = "DELETE FROM files"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain("DELETE", badRequest.Value?.ToString() ?? string.Empty);
        Assert.Contains("Query is not allowed for profiling", badRequest.Value?.ToString() ?? string.Empty);
    }
}
