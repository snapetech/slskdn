// <copyright file="TelemetryControllerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Telemetry;

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using slskd;
using slskd.Telemetry;
using Xunit;

public class TelemetryControllerTests
{
    [Fact]
    public async Task Get_WithParameterizedJsonAcceptHeader_ReturnsJson()
    {
        var controller = new TelemetryController(CreateTelemetryService(new StubPrometheusService()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        controller.Request.Headers.Accept = "application/json; charset=utf-8";

        var result = await controller.Get();

        Assert.IsType<OkObjectResult>(result);
    }

    private static TelemetryService CreateTelemetryService(PrometheusService prometheusService)
    {
        return new TelemetryService(
            prometheusService,
            new ReportsService(new ConnectionStringDictionary(new Dictionary<Database, ConnectionString>())));
    }

    private sealed class StubPrometheusService : PrometheusService
    {
        public override Task<Dictionary<string, PrometheusMetric>> GetMetricsAsObject(IEnumerable<System.Text.RegularExpressions.Regex>? include = null)
        {
            return Task.FromResult(new Dictionary<string, PrometheusMetric> { ["slskd_test"] = new() { Name = "slskd_test" } });
        }

        public override Task<string> GetMetricsAsString()
        {
            return Task.FromResult("slskd_test 1");
        }
    }
}
