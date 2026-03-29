// <copyright file="ReportsControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Telemetry;

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using slskd;
using slskd.Telemetry;
using Xunit;

public class ReportsControllerTests
{
    [Fact]
    public void GetTransferSummary_TrimsDirectionBeforeValidation()
    {
        var controller = CreateController();
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(-1);

        var result = controller.GetTransferSummary(start: start, end: end, direction: " Download ", username: "   ");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("End time must be later than start time", badRequest.Value);
    }

    [Fact]
    public void GetTransferLeaderboard_TrimsSortInputsBeforeValidation()
    {
        var controller = CreateController();

        var result = controller.GetTransferLeaderboard(direction: " Upload ", sortBy: " Count ", sortOrder: " DESC ", limit: 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Limit must be greater than zero", badRequest.Value);
    }

    [Fact]
    public void GetTransferExceptions_TrimsSortOrderBeforeValidation()
    {
        var controller = CreateController();

        var result = controller.GetTransferExceptions(direction: " Download ", sortOrder: " DESC ", limit: 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Limit must be greater than zero", badRequest.Value);
    }

    [Fact]
    public void GetTransferSummary_WithInvalidDirection_ReturnsSanitizedBadRequest()
    {
        var controller = CreateController();

        var result = controller.GetTransferSummary(direction: "sideways");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid direction", badRequest.Value);
    }

    [Fact]
    public void GetTransferLeaderboard_WithInvalidSortField_ReturnsSanitizedBadRequest()
    {
        var controller = CreateController();

        var result = controller.GetTransferLeaderboard(direction: "upload", sortBy: "nope");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid sort field", badRequest.Value);
    }

    [Fact]
    public void GetTransferExceptions_WithInvalidSortOrder_ReturnsSanitizedBadRequest()
    {
        var controller = CreateController();

        var result = controller.GetTransferExceptions(direction: "download", sortOrder: "sideways");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid sort order", badRequest.Value);
    }

    private static ReportsController CreateController()
    {
        return new ReportsController(
            new TelemetryService(
                new PrometheusService(),
                new ReportsService(new ConnectionStringDictionary(new Dictionary<Database, ConnectionString>
                {
                    [Database.Transfers] = "Data Source=:memory:",
                }))));
    }
}
