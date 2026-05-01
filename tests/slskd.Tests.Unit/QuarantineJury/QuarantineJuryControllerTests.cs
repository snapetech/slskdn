// <copyright file="QuarantineJuryControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.QuarantineJury;

using Microsoft.AspNetCore.Mvc;
using Moq;
using slskd.QuarantineJury;
using slskd.QuarantineJury.API;

public sealed class QuarantineJuryControllerTests
{
    [Fact]
    public async Task CreateRequest_ReturnsBadRequestForInvalidRequest()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.CreateRequestAsync(It.IsAny<QuarantineJuryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryValidationResult
            {
                Errors = new List<string> { "invalid" },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.CreateRequest(new QuarantineJuryRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetRequests_ReturnsServiceResult()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetRequestsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuarantineJuryRequest>
            {
                new() { Id = "request-1" },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetRequests(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var requests = Assert.IsAssignableFrom<IReadOnlyList<QuarantineJuryRequest>>(ok.Value);
        Assert.Single(requests);
    }

    [Fact]
    public async Task SubmitVerdict_ReturnsBadRequestForInvalidVerdict()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.SubmitVerdictAsync(It.IsAny<QuarantineJuryVerdictRecord>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryValidationResult
            {
                Errors = new List<string> { "invalid" },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.SubmitVerdict(new QuarantineJuryVerdictRecord(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAggregate_ReturnsAggregate()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetAggregateAsync("request-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryAggregate
            {
                RequestId = "request-1",
                Recommendation = QuarantineJuryVerdict.NeedsManualReview,
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetAggregate("request-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var aggregate = Assert.IsType<QuarantineJuryAggregate>(ok.Value);
        Assert.Equal("request-1", aggregate.RequestId);
    }

    [Fact]
    public async Task GetReview_ReturnsServiceResult()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetReviewAsync("request-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryReview
            {
                Request = new QuarantineJuryRequest { Id = "request-1" },
                CanAcceptReleaseCandidate = true,
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetReview("request-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var review = Assert.IsType<QuarantineJuryReview>(ok.Value);
        Assert.Equal("request-1", review.Request.Id);
    }

    [Fact]
    public async Task GetReview_ReturnsNotFoundForMissingRequest()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetReviewAsync("missing-request", It.IsAny<CancellationToken>()))
            .ReturnsAsync((QuarantineJuryReview)null);
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetReview("missing-request", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetAuditReport_ReturnsServiceResult()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetAuditReportAsync(24, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryAuditReport
            {
                RequestCount = 2,
                PendingReleaseCandidateCount = 1,
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetAuditReport(24, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var report = Assert.IsType<QuarantineJuryAuditReport>(ok.Value);
        Assert.Equal(2, report.RequestCount);
        Assert.Equal(1, report.PendingReleaseCandidateCount);
    }

    [Fact]
    public async Task AcceptReleaseCandidate_ReturnsBadRequestForRejectedAcceptance()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.AcceptReleaseCandidateAsync("request-1", It.IsAny<QuarantineJuryAcceptanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryAcceptanceResult
            {
                Errors = new List<string> { "Only a release-candidate supermajority can be accepted." },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.AcceptReleaseCandidate(
            "request-1",
            new QuarantineJuryAcceptanceRequest(),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AcceptReleaseCandidate_ReturnsNotFoundForMissingRequest()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.AcceptReleaseCandidateAsync("missing-request", It.IsAny<QuarantineJuryAcceptanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryAcceptanceResult
            {
                Errors = new List<string> { "Request not found." },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.AcceptReleaseCandidate(
            "missing-request",
            new QuarantineJuryAcceptanceRequest(),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AcceptReleaseCandidate_ReturnsAcceptedDecision()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.AcceptReleaseCandidateAsync("request-1", It.IsAny<QuarantineJuryAcceptanceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryAcceptanceResult
            {
                Decision = new QuarantineJuryReviewDecision
                {
                    RequestId = "request-1",
                    AcceptedRecommendation = QuarantineJuryVerdict.ReleaseCandidate,
                },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.AcceptReleaseCandidate(
            "request-1",
            new QuarantineJuryAcceptanceRequest(),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var acceptance = Assert.IsType<QuarantineJuryAcceptanceResult>(ok.Value);
        Assert.True(acceptance.IsAccepted);
    }

    [Fact]
    public async Task GetReleasePackage_ReturnsAcceptedPackage()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetReleasePackageAsync("request-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryReleasePackageResult
            {
                Package = new QuarantineJuryReleasePackage
                {
                    RequestId = "request-1",
                    MutatesLocalQuarantineState = false,
                },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetReleasePackage("request-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var packageResult = Assert.IsType<QuarantineJuryReleasePackageResult>(ok.Value);
        Assert.True(packageResult.IsReady);
        Assert.NotNull(packageResult.Package);
        Assert.False(packageResult.Package.MutatesLocalQuarantineState);
    }

    [Fact]
    public async Task GetReleasePackage_ReturnsBadRequestBeforeAcceptance()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetReleasePackageAsync("request-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryReleasePackageResult
            {
                Errors = new List<string> { "Release candidate has not been accepted locally." },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetReleasePackage("request-1", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetReleasePackage_ReturnsNotFoundForMissingRequest()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetReleasePackageAsync("missing-request", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryReleasePackageResult
            {
                Errors = new List<string> { "Request not found." },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetReleasePackage("missing-request", CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task RouteRequest_ReturnsBadRequestForFailedRouteAttempt()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.RouteRequestAsync("request-1", It.IsAny<QuarantineJuryRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryRouteAttempt
            {
                RequestId = "request-1",
                Success = false,
                ErrorMessage = "Routing backend is not available.",
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.RouteRequest("request-1", new QuarantineJuryRouteRequest(), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RouteRequest_ReturnsNotFoundForMissingRequest()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.RouteRequestAsync("missing-request", It.IsAny<QuarantineJuryRouteRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QuarantineJuryRouteAttempt
            {
                RequestId = "missing-request",
                Success = false,
                ErrorMessage = "Request not found.",
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.RouteRequest("missing-request", new QuarantineJuryRouteRequest(), CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetRouteAttempts_ReturnsServiceResult()
    {
        var service = new Mock<IQuarantineJuryService>();
        service
            .Setup(s => s.GetRouteAttemptsAsync("request-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<QuarantineJuryRouteAttempt>
            {
                new() { RequestId = "request-1", Success = true },
            });
        var controller = new QuarantineJuryController(service.Object);

        var result = await controller.GetRouteAttempts("request-1", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var attempts = Assert.IsAssignableFrom<IReadOnlyList<QuarantineJuryRouteAttempt>>(ok.Value);
        Assert.Single(attempts);
    }
}
