// <copyright file="MetadataPortabilityControllerTests.cs" company="slskdN Team">
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

public class MetadataPortabilityControllerTests
{
    [Fact]
    public async Task Export_TrimsAndDeduplicatesContentIdsBeforeDispatch()
    {
        var portability = new Mock<IMetadataPortability>();
        portability
            .Setup(service => service.ExportAsync(It.IsAny<IEnumerable<string>>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MetadataPackage(
                "1.0",
                DateTimeOffset.UtcNow,
                "slskdN",
                System.Array.Empty<MetadataEntry>(),
                System.Array.Empty<IpldLink>(),
                new MetadataPackageMetadata(0, 0, new Dictionary<string, int>(), "checksum")));

        var controller = new MetadataPortabilityController(
            NullLogger<MetadataPortabilityController>.Instance,
            portability.Object);

        var result = await controller.Export(
            new MetadataExportRequest(new[] { " content:mb:recording:test ", "content:mb:recording:test", "   " }, true),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        portability.Verify(
            service => service.ExportAsync(
                It.Is<IEnumerable<string>>(contentIds => contentIds.SequenceEqual(new[] { "content:mb:recording:test" })),
                true,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
