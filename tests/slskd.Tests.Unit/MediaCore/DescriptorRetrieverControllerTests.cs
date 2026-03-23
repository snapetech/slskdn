// <copyright file="DescriptorRetrieverControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class DescriptorRetrieverControllerTests
{
    [Fact]
    public async Task RetrieveDescriptor_WhenDescriptorIsMissing_DoesNotLeakErrorMessage()
    {
        var retriever = new Mock<IDescriptorRetriever>();
        retriever
            .Setup(service => service.RetrieveAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescriptorRetrievalResult(
                false,
                null,
                DateTimeOffset.UtcNow,
                TimeSpan.Zero,
                false,
                null,
                "sensitive detail"));

        var controller = new DescriptorRetrieverController(
            NullLogger<DescriptorRetrieverController>.Instance,
            retriever.Object);

        var result = await controller.RetrieveDescriptor("content:mb:recording:test", false, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", notFound.Value?.ToString() ?? string.Empty);
        Assert.Contains("Descriptor not found", notFound.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task RetrieveDescriptor_TrimsContentIdBeforeLookup()
    {
        var retriever = new Mock<IDescriptorRetriever>();
        retriever
            .Setup(service => service.RetrieveAsync("content:mb:recording:test", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescriptorRetrievalResult(
                true,
                new ContentDescriptor { ContentId = "content:mb:recording:test" },
                DateTimeOffset.UtcNow,
                TimeSpan.Zero,
                false,
                null));

        var controller = new DescriptorRetrieverController(
            NullLogger<DescriptorRetrieverController>.Instance,
            retriever.Object);

        var result = await controller.RetrieveDescriptor(" content:mb:recording:test ", false, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        retriever.Verify(
            service => service.RetrieveAsync("content:mb:recording:test", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RetrieveBatch_TrimsAndDeduplicatesContentIds()
    {
        var retriever = new Mock<IDescriptorRetriever>();
        retriever
            .Setup(service => service.RetrieveBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchRetrievalResult(1, 1, 0, TimeSpan.Zero, System.Array.Empty<DescriptorRetrievalResult>()));

        var controller = new DescriptorRetrieverController(
            NullLogger<DescriptorRetrieverController>.Instance,
            retriever.Object);

        var result = await controller.RetrieveBatch(
            new BatchRetrievalRequest(new[] { " content:mb:recording:test ", "content:mb:recording:test", "   " }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        retriever.Verify(
            service => service.RetrieveBatchAsync(
                It.Is<IEnumerable<string>>(contentIds => contentIds.SequenceEqual(new[] { "content:mb:recording:test" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task QueryByDomain_TrimsDomainAndTypeBeforeDispatch()
    {
        var retriever = new Mock<IDescriptorRetriever>();
        retriever
            .Setup(service => service.QueryByDomainAsync("mb", "recording", 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescriptorQueryResult("mb", "recording", 0, TimeSpan.Zero, System.Array.Empty<ContentDescriptor>(), false));

        var controller = new DescriptorRetrieverController(
            NullLogger<DescriptorRetrieverController>.Instance,
            retriever.Object);

        var result = await controller.QueryByDomain(" mb ", " recording ", 50, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        retriever.Verify(
            service => service.QueryByDomainAsync("mb", "recording", 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
