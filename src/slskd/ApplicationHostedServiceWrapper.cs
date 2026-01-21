// <copyright file="ApplicationHostedServiceWrapper.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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
        logger?.LogInformation("[ApplicationHostedServiceWrapper] Constructor called - Application already resolved");
        _application = application;
        _logger = logger;
        logger?.LogInformation("[ApplicationHostedServiceWrapper] Constructor completed");
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[ApplicationHostedServiceWrapper] StartAsync called - about to call Application.StartAsync");
        var result = _application.StartAsync(cancellationToken);
        _logger?.LogInformation("[ApplicationHostedServiceWrapper] Application.StartAsync returned");
        return result;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("[ApplicationHostedServiceWrapper] Stopping Application via wrapper");
        return _application.StopAsync(cancellationToken);
    }
}
