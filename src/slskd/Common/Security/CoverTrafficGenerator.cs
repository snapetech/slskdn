// <copyright file="CoverTrafficGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Cover traffic generator that sends dummy messages when idle to maintain constant traffic patterns.
/// </summary>
public class CoverTrafficGenerator : ICoverTrafficGenerator
{
    private readonly CoverTrafficOptions _options;
    private readonly Func<byte[]> _coverMessageFactory;
    private readonly Func<Task> _sendCoverMessageAsync;
    private readonly ILogger<CoverTrafficGenerator> _logger;

    private readonly CancellationTokenSource _cts = new();
    private Task? _generationTask;
    private DateTimeOffset _lastTrafficTime = DateTimeOffset.UtcNow;
    private long _coverMessagesSent;
    private readonly object _statsLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverTrafficGenerator"/> class.
    /// </summary>
    /// <param name="options">The cover traffic options.</param>
    /// <param name="coverMessageFactory">Factory function to create cover messages.</param>
    /// <param name="sendCoverMessageAsync">Function to send a cover message.</param>
    /// <param name="logger">The logger.</param>
    public CoverTrafficGenerator(
        CoverTrafficOptions options,
        Func<byte[]> coverMessageFactory,
        Func<Task> sendCoverMessageAsync,
        ILogger<CoverTrafficGenerator> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _coverMessageFactory = coverMessageFactory ?? throw new ArgumentNullException(nameof(coverMessageFactory));
        _sendCoverMessageAsync = sendCoverMessageAsync ?? throw new ArgumentNullException(nameof(sendCoverMessageAsync));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts the cover traffic generation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when generation starts.</returns>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_generationTask != null)
            return Task.CompletedTask;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        _generationTask = Task.Run(() => GenerateCoverTrafficAsync(linkedCts.Token), linkedCts.Token);

        _logger.LogInformation("Cover traffic generation started with interval {Interval}s", _options.IntervalSeconds);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the cover traffic generation.
    /// </summary>
    /// <returns>A task that completes when generation stops.</returns>
    public async Task StopAsync()
    {
        _cts.Cancel();

        if (_generationTask != null)
        {
            try
            {
                await _generationTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Cover traffic generation did not stop cleanly within timeout");
            }
            finally
            {
                _generationTask = null;
            }
        }

        _logger.LogInformation("Cover traffic generation stopped");
    }

    /// <summary>
    /// Notifies the generator that real traffic was sent, resetting the idle timer.
    /// </summary>
    public void NotifyTraffic()
    {
        lock (_statsLock)
        {
            _lastTrafficTime = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Gets statistics about cover traffic generation.
    /// </summary>
    public CoverTrafficStats GetStats()
    {
        lock (_statsLock)
        {
            return new CoverTrafficStats
            {
                CoverMessagesSent = _coverMessagesSent,
                TimeSinceLastTraffic = DateTimeOffset.UtcNow - _lastTrafficTime,
                IsActive = _generationTask != null && !_cts.IsCancellationRequested,
            };
        }
    }

    private async Task GenerateCoverTrafficAsync(CancellationToken cancellationToken)
    {
        var intervalMs = _options.IntervalSeconds * 1000;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Wait for the configured interval
                await Task.Delay(intervalMs, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    break;

                // Check if we should send cover traffic
                bool shouldSend = false;
                TimeSpan timeSinceLastTraffic;

                lock (_statsLock)
                {
                    timeSinceLastTraffic = DateTimeOffset.UtcNow - _lastTrafficTime;
                    shouldSend = timeSinceLastTraffic.TotalSeconds >= _options.IntervalSeconds;
                }

                if (shouldSend && (_options.OnlyWhenIdle || timeSinceLastTraffic.TotalSeconds >= _options.IntervalSeconds))
                {
                    // Generate and send cover message
                    try
                    {
                        await _sendCoverMessageAsync();

                        lock (_statsLock)
                        {
                            _coverMessagesSent++;
                        }

                        _logger.LogDebug("Sent cover traffic message ({Total} total)", _coverMessagesSent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to send cover traffic message");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancelled
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in cover traffic generation loop");
                // Continue the loop despite errors
            }
        }
    }
}

