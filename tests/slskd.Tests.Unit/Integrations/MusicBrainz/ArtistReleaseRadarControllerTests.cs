// <copyright file="ArtistReleaseRadarControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Integrations.MusicBrainz;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.Integrations.MusicBrainz.API;
using slskd.Integrations.MusicBrainz.Radar;

public sealed class ArtistReleaseRadarControllerTests
{
    [Fact]
    public async Task GetSubscriptions_ReturnsServiceResult()
    {
        var radarService = new Mock<IArtistReleaseRadarService>();
        radarService
            .Setup(service => service.GetSubscriptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArtistRadarSubscription>
            {
                new() { ArtistId = "artist-1", ArtistName = "Scene Artist" },
            });
        var controller = new ArtistReleaseRadarController(radarService.Object);

        var result = await controller.GetSubscriptions(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var subscriptions = Assert.IsAssignableFrom<IReadOnlyList<ArtistRadarSubscription>>(ok.Value);
        Assert.Single(subscriptions);
    }

    [Fact]
    public async Task Subscribe_ReturnsBadRequestWhenArtistIdMissing()
    {
        var radarService = new Mock<IArtistReleaseRadarService>();
        var controller = new ArtistReleaseRadarController(radarService.Object);

        var result = await controller.Subscribe(new ArtistRadarSubscription(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RecordObservation_ReturnsBadRequestWhenServiceRejectsObservation()
    {
        var radarService = new Mock<IArtistReleaseRadarService>();
        radarService
            .Setup(service => service.RecordObservationAsync(It.IsAny<ArtistRadarObservation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArtistRadarObservationResult
            {
                Accepted = false,
                RejectionReason = "invalid",
            });
        var controller = new ArtistReleaseRadarController(radarService.Object);

        var result = await controller.RecordObservation(new ArtistRadarObservation(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetNotifications_ReturnsUnreadFilterResult()
    {
        var radarService = new Mock<IArtistReleaseRadarService>();
        radarService
            .Setup(service => service.GetNotificationsAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArtistRadarNotification>
            {
                new() { ArtistId = "artist-1", RecordingId = "recording-1" },
            });
        var controller = new ArtistReleaseRadarController(radarService.Object);

        var result = await controller.GetNotifications(true, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var notifications = Assert.IsAssignableFrom<IReadOnlyList<ArtistRadarNotification>>(ok.Value);
        Assert.Single(notifications);
    }

    [Fact]
    public async Task RouteNotification_ReturnsBadRequestForFailedRouteAttempt()
    {
        var radarService = new Mock<IArtistReleaseRadarService>();
        radarService
            .Setup(service => service.RouteNotificationAsync("notification-1", It.IsAny<ArtistRadarRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ArtistRadarRouteAttempt
            {
                NotificationId = "notification-1",
                Success = false,
                ErrorMessage = "invalid",
            });
        var controller = new ArtistReleaseRadarController(radarService.Object);

        var result = await controller.RouteNotification("notification-1", new ArtistRadarRouteRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetRouteAttempts_ReturnsServiceResult()
    {
        var radarService = new Mock<IArtistReleaseRadarService>();
        radarService
            .Setup(service => service.GetRouteAttemptsAsync("notification-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ArtistRadarRouteAttempt>
            {
                new() { NotificationId = "notification-1", Success = true },
            });
        var controller = new ArtistReleaseRadarController(radarService.Object);

        var result = await controller.GetRouteAttempts("notification-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var attempts = Assert.IsAssignableFrom<IReadOnlyList<ArtistRadarRouteAttempt>>(ok.Value);
        Assert.Single(attempts);
    }
}
