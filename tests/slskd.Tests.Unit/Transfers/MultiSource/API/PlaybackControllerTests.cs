// <copyright file="PlaybackControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers.MultiSource.API;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Transfers.MultiSource.API;
using slskd.Transfers.MultiSource.Playback;
using Xunit;

public class PlaybackControllerTests
{
    [Fact]
    public async Task PostFeedback_TrimsJobIdAndTrackIdBeforeDispatch()
    {
        var feedback = new Mock<IPlaybackFeedbackService>();
        feedback
            .Setup(service => service.RecordAsync(It.IsAny<PlaybackFeedback>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var priorities = new Mock<IPlaybackPriorityService>();
        priorities.Setup(service => service.GetPriority("job-1")).Returns(PriorityZone.High);

        var controller = new PlaybackController(feedback.Object, priorities.Object);

        var result = await controller.PostFeedback(new PlaybackFeedback
        {
            JobId = " job-1 ",
            TrackId = " track-1 ",
            PositionMs = 100,
            BufferAheadMs = 200,
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        feedback.Verify(
            service => service.RecordAsync(
                It.Is<PlaybackFeedback>(payload => payload.JobId == "job-1" && payload.TrackId == "track-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        priorities.Verify(service => service.GetPriority("job-1"), Times.Once);
    }

    [Fact]
    public void GetDiagnostics_TrimsJobIdBeforeLookup()
    {
        var priorities = new Mock<IPlaybackPriorityService>();
        priorities
            .Setup(service => service.GetLatest("job-1"))
            .Returns(new PlaybackFeedback
            {
                JobId = "job-1",
                TrackId = "track-1",
                PositionMs = 100,
                BufferAheadMs = 200,
            });
        priorities.Setup(service => service.GetPriority("job-1")).Returns(PriorityZone.Mid);

        var controller = new PlaybackController(Mock.Of<IPlaybackFeedbackService>(), priorities.Object);

        var result = controller.GetDiagnostics(" job-1 ");

        var ok = Assert.IsType<OkObjectResult>(result);
        var diagnostics = Assert.IsType<PlaybackDiagnostics>(ok.Value);
        Assert.Equal("job-1", diagnostics.JobId);
        priorities.Verify(service => service.GetLatest("job-1"), Times.Once);
        priorities.Verify(service => service.GetPriority("job-1"), Times.Once);
    }
}
