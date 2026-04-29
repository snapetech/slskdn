// <copyright file="ContentDescriptorPublisherControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.MediaCore;

using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class ContentDescriptorPublisherControllerTests
{
    [Fact]
    public async Task PublishDescriptor_WithBlankContentId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.PublishDescriptor(
            new PublishDescriptorRequest(new ContentDescriptor { ContentId = "   " }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task PublishDescriptor_TrimsDescriptorFieldsBeforeDispatch()
    {
        var publisher = new Mock<IContentDescriptorPublisher>();
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescriptorPublishResult(true, "content:a", "v1", DateTimeOffset.UtcNow, TimeSpan.FromHours(1)));

        var controller = CreateController(publisher);

        var result = await controller.PublishDescriptor(
            new PublishDescriptorRequest(new ContentDescriptor { ContentId = " content:a ", Codec = " flac " }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        publisher.Verify(
            service => service.PublishAsync(
                It.Is<ContentDescriptor>(descriptor => descriptor.ContentId == "content:a" && descriptor.Codec == "flac"),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RepublishExpiring_WithOnlyBlankContentIds_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.RepublishExpiring(
            new RepublishRequest(new[] { " ", "\t" }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RepublishExpiring_TrimsAndDeduplicatesContentIds()
    {
        var publisher = new Mock<IContentDescriptorPublisher>();
        publisher
            .Setup(service => service.RepublishExpiringAsync(It.IsAny<IEnumerable<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepublishResult(2, 2, 0, 0, TimeSpan.Zero));

        var controller = CreateController(publisher);

        var result = await controller.RepublishExpiring(
            new RepublishRequest(new[] { " content:a ", "content:a", " content:b " }),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        publisher.Verify(
            service => service.RepublishExpiringAsync(
                It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { "content:a", "content:b" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishDescriptor_WhenPublisherReturnsFailure_DoesNotLeakErrorMessage()
    {
        var publisher = new Mock<IContentDescriptorPublisher>();
        publisher
            .Setup(service => service.PublishAsync(It.IsAny<ContentDescriptor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescriptorPublishResult(false, "content:a", "v1", DateTimeOffset.UtcNow, TimeSpan.FromHours(1), "sensitive detail"));

        var controller = CreateController(publisher);

        var result = await controller.PublishDescriptor(
            new PublishDescriptorRequest(new ContentDescriptor { ContentId = "content:a" }),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", badRequest.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to publish descriptor", badRequest.Value?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task UpdateDescriptor_WhenPublisherReturnsFailure_DoesNotLeakErrorMessage()
    {
        var publisher = new Mock<IContentDescriptorPublisher>();
        publisher
            .Setup(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<DescriptorUpdates>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DescriptorUpdateResult(false, "content:a", "v2", "v1", Array.Empty<string>(), "sensitive detail"));

        var controller = CreateController(publisher);

        var result = await controller.UpdateDescriptor(
            "content:a",
            new UpdateDescriptorRequest(new DescriptorUpdates(NewCodec: "opus")),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.DoesNotContain("sensitive detail", badRequest.Value?.ToString() ?? string.Empty);
        Assert.Contains("Failed to update descriptor", badRequest.Value?.ToString() ?? string.Empty);
    }

    private static ContentDescriptorPublisherController CreateController(Mock<IContentDescriptorPublisher>? publisher = null)
    {
        return new ContentDescriptorPublisherController(
            NullLogger<ContentDescriptorPublisherController>.Instance,
            (publisher ?? new Mock<IContentDescriptorPublisher>()).Object);
    }
}
