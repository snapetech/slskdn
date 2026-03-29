// <copyright file="WarmCacheControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.API.Native;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.API.Native;
using slskd.Core;
using slskd.Transfers.MultiSource.Caching;
using Xunit;
using TestOptionsMonitor = slskd.Tests.Unit.TestOptionsMonitor<slskd.Options>;

public class WarmCacheControllerTests
{
    [Fact]
    public async Task SubmitHints_DeduplicatesIdentifiersCaseInsensitively()
    {
        var popularity = new Mock<IWarmCachePopularityService>();
        var controller = new WarmCacheController(
            popularity.Object,
            new TestOptionsMonitor(new slskd.Options
            {
                WarmCache = new WarmCacheOptions { Enabled = true }
            }),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<WarmCacheController>>());

        var result = await controller.SubmitHints(
            new WarmCacheHintsRequest(
                MbReleaseIds: new List<string> { " mbid-1 ", "MBID-1" },
                MbArtistIds: new List<string> { " artist-1 ", "ARTIST-1" },
                MbLabelIds: new List<string> { " label-1 ", "LABEL-1" }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        popularity.Verify(service => service.RecordAccessAsync("mb:release:mbid-1", It.IsAny<CancellationToken>()), Times.Once);
        popularity.Verify(service => service.RecordAccessAsync("mb:artist:artist-1", It.IsAny<CancellationToken>()), Times.Once);
        popularity.Verify(service => service.RecordAccessAsync("mb:label:label-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
