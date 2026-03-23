// <copyright file="AudioBoundaryControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Audio;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Audio;
using slskd.Audio.API;
using Xunit;

public class AudioBoundaryControllerTests
{
    [Fact]
    public async Task DedupeGet_TrimsRecordingIdBeforeDispatch()
    {
        var dedupe = new Mock<IDedupeService>();
        dedupe
            .Setup(service => service.GetDedupeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DedupeResult());

        var controller = new DedupeController(dedupe.Object);

        await controller.Get(" mbid-1 ", CancellationToken.None);

        dedupe.Verify(service => service.GetDedupeAsync("mbid-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DedupeGet_WithBlankRecordingId_ReturnsBadRequest()
    {
        var controller = new DedupeController(Mock.Of<IDedupeService>());

        var result = await controller.Get("   ", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CanonicalGet_TrimsRecordingIdBeforeDispatch()
    {
        var canonicalStats = new Mock<ICanonicalStatsService>();
        canonicalStats
            .Setup(service => service.GetCanonicalVariantCandidatesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AudioVariant>());

        var controller = new CanonicalController(canonicalStats.Object);

        await controller.Get(" rec-1 ", CancellationToken.None);

        canonicalStats.Verify(service => service.GetCanonicalVariantCandidatesAsync("rec-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzerMigration_WithBlankTargetVersion_ReturnsBadRequest()
    {
        var controller = new AnalyzerMigrationController(Mock.Of<IAnalyzerMigrationService>());

        var result = await controller.Migrate("   ", false, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnalyzerMigration_TrimsTargetVersionBeforeDispatch()
    {
        var migration = new Mock<IAnalyzerMigrationService>();
        migration
            .Setup(service => service.MigrateAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var controller = new AnalyzerMigrationController(migration.Object);

        await controller.Migrate(" audioqa-2 ", true, CancellationToken.None);

        migration.Verify(service => service.MigrateAsync("audioqa-2", true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
