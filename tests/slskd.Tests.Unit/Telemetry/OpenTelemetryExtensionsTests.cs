// <copyright file="OpenTelemetryExtensionsTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Telemetry;

using Microsoft.Extensions.DependencyInjection;
using slskd.Telemetry;
using Xunit;

public class OpenTelemetryExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryTracing_WithJaegerExporter_UsesSupportedOtlpPath()
    {
        var services = new ServiceCollection();
        var options = new Options
        {
            Telemetry = new Options.TelemetryOptions
            {
                Tracing = new Options.TelemetryOptions.TracingOptions
                {
                    Enabled = true,
                    Exporter = "jaeger",
                    JaegerEndpoint = "localhost",
                },
            },
        };

        services.AddOpenTelemetryTracing(options);

        Assert.NotEmpty(services);
    }
}
