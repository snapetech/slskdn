// <copyright file="MusicBrainzOverlayControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Integrations.MusicBrainz;
using slskd.Integrations.MusicBrainz.API;
using slskd.Integrations.MusicBrainz.Models;
using slskd.Integrations.MusicBrainz.Overlay;

public sealed class MusicBrainzOverlayControllerTests
{
    [Fact]
    public async Task GetEffectiveReleaseGraph_ReturnsOverlayResponse()
    {
        var graph = new ArtistReleaseGraph { ArtistId = "artist-1", Name = "Original Artist" };
        var effective = new ArtistReleaseGraph { ArtistId = "artist-1", Name = "Corrected Artist" };
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        releaseGraphService
            .Setup(service => service.GetArtistReleaseGraphAsync("artist-1", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(graph);
        overlayService
            .Setup(service => service.ApplyToArtistReleaseGraphAsync(graph, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayApplication<ArtistReleaseGraph>
            {
                Original = graph,
                Effective = effective,
                Provenance = new List<MusicBrainzOverlayProvenance>
                {
                    new() { EditId = "edit-1" },
                },
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.GetEffectiveReleaseGraph(" artist-1 ", false, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MusicBrainzOverlayReleaseGraphResponse>(ok.Value);
        Assert.Equal("Original Artist", response.Original.Name);
        Assert.Equal("Corrected Artist", response.Effective.Name);
        Assert.Single(response.Provenance);
    }

    [Fact]
    public async Task StoreEdit_ReturnsBadRequestForInvalidOverlay()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.StoreAsync(It.IsAny<MusicBrainzOverlayEdit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayValidationResult
            {
                Errors = new List<string> { "invalid" },
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.StoreEdit(new MusicBrainzOverlayEdit(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetExportReview_ReturnsServiceReview()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.GetExportReviewAsync("edit-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayExportReview
            {
                UpstreamTarget = "ReleaseGroup:release-group-1",
                CanApproveExport = true,
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.GetExportReview("edit-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var review = Assert.IsType<MusicBrainzOverlayExportReview>(ok.Value);
        Assert.True(review.CanApproveExport);
    }

    [Fact]
    public async Task GetExportReview_ReturnsNotFoundForMissingEdit()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.GetExportReviewAsync("missing-edit", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MusicBrainzOverlayExportReview)null);
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.GetExportReview("missing-edit", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ApproveExport_ReturnsBadRequestForRejectedApproval()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.ApproveExportAsync("edit-1", It.IsAny<MusicBrainzOverlayExportApprovalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayExportApprovalResult
            {
                Errors = new List<string> { "Approved-by identifier must be opaque and safe." },
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.ApproveExport("edit-1", new MusicBrainzOverlayExportApprovalRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ApproveExport_ReturnsNotFoundForMissingEdit()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.ApproveExportAsync("missing-edit", It.IsAny<MusicBrainzOverlayExportApprovalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayExportApprovalResult
            {
                Errors = new List<string> { "Edit not found." },
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.ApproveExport("missing-edit", new MusicBrainzOverlayExportApprovalRequest(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ApproveExport_ReturnsApprovedDecision()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.ApproveExportAsync("edit-1", It.IsAny<MusicBrainzOverlayExportApprovalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayExportApprovalResult
            {
                Decision = new MusicBrainzOverlayExportDecision
                {
                    EditId = "edit-1",
                    ApprovedBy = "actor:operator",
                },
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.ApproveExport("edit-1", new MusicBrainzOverlayExportApprovalRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var approval = Assert.IsType<MusicBrainzOverlayExportApprovalResult>(ok.Value);
        Assert.True(approval.IsApproved);
    }

    [Fact]
    public async Task RouteEdit_ReturnsBadRequestForFailedRouteAttempt()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.RouteEditAsync("edit-1", It.IsAny<MusicBrainzOverlayRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayRouteAttempt
            {
                EditId = "edit-1",
                Success = false,
                ErrorMessage = "Routing backend is not available.",
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.RouteEdit("edit-1", new MusicBrainzOverlayRouteRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RouteEdit_ReturnsNotFoundForMissingEdit()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.RouteEditAsync("missing-edit", It.IsAny<MusicBrainzOverlayRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MusicBrainzOverlayRouteAttempt
            {
                EditId = "missing-edit",
                Success = false,
                ErrorMessage = "Edit not found.",
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.RouteEdit("missing-edit", new MusicBrainzOverlayRouteRequest(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetRouteAttempts_ReturnsServiceResult()
    {
        var releaseGraphService = new Mock<IArtistReleaseGraphService>();
        var overlayService = new Mock<IMusicBrainzOverlayService>();
        overlayService
            .Setup(service => service.GetRouteAttemptsAsync("edit-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MusicBrainzOverlayRouteAttempt>
            {
                new() { EditId = "edit-1", Success = true },
            });
        var controller = new MusicBrainzOverlayController(releaseGraphService.Object, overlayService.Object);

        var result = await controller.GetRouteAttempts("edit-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var attempts = Assert.IsAssignableFrom<IReadOnlyList<MusicBrainzOverlayRouteAttempt>>(ok.Value);
        Assert.Single(attempts);
    }
}
