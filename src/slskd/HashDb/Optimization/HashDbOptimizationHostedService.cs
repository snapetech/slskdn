// <copyright file="HashDbOptimizationHostedService.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.HashDb.Optimization;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Hosted service that runs HashDb optimization tasks on startup and periodically.
/// </summary>
public class HashDbOptimizationHostedService : IHostedService
{
    private readonly IHashDbOptimizationService _optimizationService;
    private readonly ILogger<HashDbOptimizationHostedService> _logger;
    private readonly HashDbOptimizationOptions _options;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HashDbOptimizationHostedService"/> class.
    /// </summary>
    public HashDbOptimizationHostedService(
        IHashDbOptimizationService optimizationService,
        IOptions<HashDbOptimizationOptions> options,
        ILogger<HashDbOptimizationHostedService> logger)
    {
        _optimizationService = optimizationService;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoOptimizeOnStartup)
        {
            _logger.LogDebug("[HashDbOptimization] Auto-optimization on startup is disabled");
            return;
        }

        try
        {
            _logger.LogInformation("[HashDbOptimization] Running automatic index optimization on startup");

            // Run index optimization (non-blocking, fire-and-forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _optimizationService.OptimizeIndexesAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("[HashDbOptimization] Automatic index optimization completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HashDbOptimization] Automatic index optimization failed (non-critical)");
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HashDbOptimization] Failed to start automatic optimization (non-critical)");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Nothing to clean up
        return Task.CompletedTask;
    }
}

/// <summary>
///     Options for HashDb optimization.
/// </summary>
public class HashDbOptimizationOptions
{
    /// <summary>
    ///     Gets or sets whether to automatically optimize indexes on startup.
    ///     Default: false (manual optimization via API)
    /// </summary>
    public bool AutoOptimizeOnStartup { get; set; } = false;
}
