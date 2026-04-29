// <copyright file="ApplicationHostedServiceWrapper.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace slskd;

/// <summary>
/// Wrapper to register Application as a hosted service without factory function blocking.
/// </summary>
public class ApplicationHostedServiceWrapper : IHostedService
{
    private readonly IApplication _application;
    private readonly ILogger<ApplicationHostedServiceWrapper>? _logger;

    public ApplicationHostedServiceWrapper(
        IApplication application,
        ILogger<ApplicationHostedServiceWrapper>? logger = null)
    {
        logger?.LogDebug("[ApplicationHostedServiceWrapper] Constructor called - Application already resolved");
        _application = application;
        _logger = logger;
        logger?.LogDebug("[ApplicationHostedServiceWrapper] Constructor completed");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (System.Environment.GetEnvironmentVariable("SLSKDN_E2E_TRACE_HOSTED") == "1")
        {
            System.Console.Error.WriteLine("[ApplicationHostedServiceWrapper] StartAsync called");
        }

        _logger?.LogDebug("[ApplicationHostedServiceWrapper] StartAsync called - about to call Application.StartAsync");

        // Application.StartAsync returns immediately (runs initialization in background)
        // This should not block the web server from starting
        var result = _application.StartAsync(cancellationToken);
        if (System.Environment.GetEnvironmentVariable("SLSKDN_E2E_TRACE_HOSTED") == "1")
        {
            System.Console.Error.WriteLine("[ApplicationHostedServiceWrapper] Application.StartAsync returned");
        }

        _logger?.LogDebug("[ApplicationHostedServiceWrapper] Application.StartAsync returned (non-blocking)");
        return result;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[ApplicationHostedServiceWrapper] Stopping Application via wrapper");
        return _application.StopAsync(cancellationToken);
    }
}
