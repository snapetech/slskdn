// <copyright file="DescriptorRetrieverControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
}
