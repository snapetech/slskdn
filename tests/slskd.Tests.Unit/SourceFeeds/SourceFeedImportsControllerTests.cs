// <copyright file="SourceFeedImportsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.SourceFeeds;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.SourceFeeds;
using slskd.SourceFeeds.API;
using Xunit;

public sealed class SourceFeedImportsControllerTests
{
    [Fact]
    public async Task GetHistory_ReturnsServiceHistory()
    {
        var service = new Mock<ISourceFeedImportService>();
        service
            .Setup(x => x.GetHistoryAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SourceFeedImportHistoryEntry>
            {
                new()
                {
                    ImportId = "import-1",
                    Provider = "local",
                    SourceKind = "csv",
                },
            });
        var controller = new SourceFeedImportsController(service.Object);

        var result = await controller.GetHistory(10);

        var ok = Assert.IsType<OkObjectResult>(result);
        var history = Assert.IsAssignableFrom<IReadOnlyList<SourceFeedImportHistoryEntry>>(ok.Value);
        var entry = Assert.Single(history);
        Assert.Equal("import-1", entry.ImportId);
    }

    [Fact]
    public async Task GetHistoryEntry_ReturnsNotFoundForMissingRun()
    {
        var service = new Mock<ISourceFeedImportService>();
        service
            .Setup(x => x.GetHistoryEntryAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SourceFeedImportHistoryEntry?)null);
        var controller = new SourceFeedImportsController(service.Object);

        var result = await controller.GetHistoryEntry("missing");

        Assert.IsType<NotFoundResult>(result);
    }
}
