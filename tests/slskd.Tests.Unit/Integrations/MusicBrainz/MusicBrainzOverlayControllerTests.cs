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
}
