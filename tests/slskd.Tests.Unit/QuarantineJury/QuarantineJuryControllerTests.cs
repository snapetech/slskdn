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
}
