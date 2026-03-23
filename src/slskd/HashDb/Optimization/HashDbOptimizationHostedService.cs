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
public sealed class HashDbOptimizationHostedService : IHostedService, IDisposable
{
    private readonly IHashDbOptimizationService _optimizationService;
    private readonly ILogger<HashDbOptimizationHostedService> _logger;
    private readonly HashDbOptimizationOptions _options;
    private CancellationTokenSource? _startupOptimizationCts;
    private Task? _startupOptimizationTask;

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
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoOptimizeOnStartup)
        {
            _logger.LogDebug("[HashDbOptimization] Auto-optimization on startup is disabled");
            return Task.CompletedTask;
        }

        try
        {
            _logger.LogInformation("[HashDbOptimization] Running automatic index optimization on startup");
            _startupOptimizationCts?.Cancel();
            _startupOptimizationCts?.Dispose();
            var startupOptimizationCts = new CancellationTokenSource();
            _startupOptimizationCts = startupOptimizationCts;

            // Run index optimization (non-blocking, fire-and-forget)
            _startupOptimizationTask = Task.Run(async () =>
            {
                try
                {
                    await _optimizationService.OptimizeIndexesAsync(startupOptimizationCts.Token).ConfigureAwait(false);
                    _logger.LogInformation("[HashDbOptimization] Automatic index optimization completed");
                }
                catch (OperationCanceledException) when (startupOptimizationCts.IsCancellationRequested)
                {
                    _logger.LogInformation("[HashDbOptimization] Automatic index optimization cancelled");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HashDbOptimization] Automatic index optimization failed (non-critical)");
                }
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[HashDbOptimization] Failed to start automatic optimization (non-critical)");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _startupOptimizationCts?.Cancel();

        if (_startupOptimizationTask != null)
        {
            try
            {
                await _startupOptimizationTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        _startupOptimizationTask = null;
        _startupOptimizationCts?.Dispose();
        _startupOptimizationCts = null;
    }

    public void Dispose()
    {
        _startupOptimizationCts?.Cancel();
        _startupOptimizationCts?.Dispose();
        _startupOptimizationCts = null;
        GC.SuppressFinalize(this);
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
