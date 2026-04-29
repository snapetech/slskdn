// <copyright file="OpenTelemetryExtensions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Telemetry;

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using slskdOptions = slskd.Options;

/// <summary>
/// Extension methods for configuring OpenTelemetry distributed tracing.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry tracing to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The application options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        slskdOptions options)
    {
        if (options.Telemetry?.Tracing?.Enabled != true)
        {
            return services;
        }

        var tracingOptions = options.Telemetry.Tracing;

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .SetResourceBuilder(ResourceBuilder.CreateDefault()
                        .AddService(
                            serviceName: "slskdn",
                            serviceVersion: System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
                            serviceInstanceId: Environment.MachineName))
                    .AddSource("slskdn.*")
                    .AddSource("slskdn.Transfers.MultiSource")
                    .AddSource("slskdn.Mesh")
                    .AddSource("slskdn.HashDb")
                    .AddSource("slskdn.Search")
                    .AddAspNetCoreInstrumentation();

                // Add exporter based on configuration
                if (!string.IsNullOrEmpty(tracingOptions.Exporter))
                {
                    switch (tracingOptions.Exporter.ToLowerInvariant())
                    {
                        case "console":
                            builder.AddConsoleExporter();
                            break;
                        case "jaeger":
                            AddOtlpExporter(builder, GetJaegerOtlpEndpoint(tracingOptions));

                            break;
                        case "otlp":
                            AddOtlpExporter(builder, tracingOptions.OtlpEndpoint);

                            break;
                    }
                }
                else
                {
                    // Default to console if no exporter specified
                    builder.AddConsoleExporter();
                }
            });

        return services;
    }

    private static void AddOtlpExporter(TracerProviderBuilder builder, string? endpoint)
    {
        if (!string.IsNullOrEmpty(endpoint))
        {
            builder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(endpoint);
            });
        }
    }

    private static string? GetJaegerOtlpEndpoint(slskdOptions.TelemetryOptions.TracingOptions tracingOptions)
    {
        if (!string.IsNullOrEmpty(tracingOptions.OtlpEndpoint))
        {
            return tracingOptions.OtlpEndpoint;
        }

        if (string.IsNullOrEmpty(tracingOptions.JaegerEndpoint))
        {
            return null;
        }

        if (Uri.TryCreate(tracingOptions.JaegerEndpoint, UriKind.Absolute, out var uri))
        {
            return uri.ToString();
        }

        var host = tracingOptions.JaegerEndpoint;
        var port = tracingOptions.JaegerPort ?? 4317;

        if (host.Contains(':', StringComparison.Ordinal))
        {
            return $"http://{host}";
        }

        return $"http://{host}:{port}";
    }
}

/// <summary>
/// Activity source for slskdn operations.
/// </summary>
public static class SlskdnActivitySource
{
    /// <summary>
    /// Activity source name.
    /// </summary>
    public const string Name = "slskdn";

    /// <summary>
    /// Main activity source.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);
}

/// <summary>
/// Activity source for multi-source download operations.
/// </summary>
public static class MultiSourceActivitySource
{
    /// <summary>
    /// Activity source name.
    /// </summary>
    public const string Name = "slskdn.Transfers.MultiSource";

    /// <summary>
    /// Activity source for multi-source downloads.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);
}

/// <summary>
/// Activity source for mesh network operations.
/// </summary>
public static class MeshActivitySource
{
    /// <summary>
    /// Activity source name.
    /// </summary>
    public const string Name = "slskdn.Mesh";

    /// <summary>
    /// Activity source for mesh operations.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);
}

/// <summary>
/// Activity source for HashDb operations.
/// </summary>
public static class HashDbActivitySource
{
    /// <summary>
    /// Activity source name.
    /// </summary>
    public const string Name = "slskdn.HashDb";

    /// <summary>
    /// Activity source for HashDb operations.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);
}

/// <summary>
/// Activity source for search operations.
/// </summary>
public static class SearchActivitySource
{
    /// <summary>
    /// Activity source name.
    /// </summary>
    public const string Name = "slskdn.Search";

    /// <summary>
    /// Activity source for search operations.
    /// </summary>
    public static readonly ActivitySource Source = new(Name);
}
