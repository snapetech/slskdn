// <copyright file="TracingControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Transfers.MultiSource.API;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Transfers.MultiSource.API;
using slskd.Transfers.MultiSource.Tracing;
using Xunit;

public class TracingControllerTests
{
    [Fact]
    public async Task GetSummary_TrimsJobIdBeforeDispatch()
    {
        var summarizer = new Mock<ISwarmTraceSummarizer>();
        summarizer
            .Setup(service => service.SummarizeAsync("job-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SwarmTraceSummary { JobId = "job-1" });

        var controller = new TracingController(summarizer.Object);

        var result = await controller.GetSummary(" job-1 ", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var summary = Assert.IsType<SwarmTraceSummary>(ok.Value);
        Assert.Equal("job-1", summary.JobId);
        summarizer.Verify(service => service.SummarizeAsync("job-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
