// <copyright file="MetadataPortabilityControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.MediaCore;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using slskd.MediaCore;
using slskd.MediaCore.API.Controllers;
using Xunit;

public class MetadataPortabilityControllerTests
{
    [Fact]
    public async Task Export_WithOnlyWhitespaceContentIds_ReturnsBadRequest()
    {
        var portability = new Mock<IMetadataPortability>();
        var controller = new MetadataPortabilityController(
            NullLogger<MetadataPortabilityController>.Instance,
            portability.Object);

        var result = await controller.Export(
            new MetadataExportRequest(new[] { "", "   " }),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        portability.Verify(
            x => x.ExportAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
