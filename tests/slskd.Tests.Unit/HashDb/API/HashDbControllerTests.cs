// <copyright file="HashDbControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.HashDb.API;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.HashDb;
using slskd.HashDb.API;
using slskd.HashDb.Models;
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

    [Fact]
    public async Task LookupHash_TrimsFlacKeyBeforeLookup()
    {
        var hashDb = new Mock<IHashDbService>();
        hashDb
            .Setup(service => service.LookupHashAsync("flac-key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashDbEntry { FlacKey = "flac-key-1", ByteHash = "hash", Size = 1 });

        var controller = new HashDbController(
            hashDb.Object,
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            Mock.Of<IHashDbOptimizationService>());

        var result = await controller.LookupHash(" flac-key-1 ");

        var ok = Assert.IsType<OkObjectResult>(result);
        var entry = Assert.IsType<HashDbEntry>(ok.Value);
        Assert.Equal("flac-key-1", entry.FlacKey);
        hashDb.Verify(service => service.LookupHashAsync("flac-key-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GenerateKey_TrimsFilenameBeforeGeneratingKey()
    {
        var controller = new HashDbController(
            Mock.Of<IHashDbService>(),
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            Mock.Of<IHashDbOptimizationService>());

        var result = controller.GenerateKey(" song.flac ", 123L);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("song.flac", ok.Value?.ToString() ?? string.Empty);
        Assert.DoesNotContain(" song.flac ", ok.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task StoreHash_TrimsFilenameAndByteHashBeforeDispatch()
    {
        var hashDb = new Mock<IHashDbService>();
        hashDb
            .Setup(service => service.StoreHashFromVerificationAsync("song.flac", 123L, "bytehash", null, null, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new HashDbController(
            hashDb.Object,
            Mock.Of<IDbContextFactory<SearchDbContext>>(),
            Mock.Of<IHashDbOptimizationService>());

        var result = await controller.StoreHash(new StoreHashRequest
        {
            Filename = " song.flac ",
            Size = 123L,
            ByteHash = " bytehash ",
        });

        Assert.IsType<OkObjectResult>(result);
        hashDb.Verify(
            service => service.StoreHashFromVerificationAsync("song.flac", 123L, "bytehash", null, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
