// <copyright file="CoverTrafficGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.Security;

/// <summary>
/// Cover traffic generator that sends dummy messages when idle to maintain constant traffic patterns.
/// </summary>
public sealed class CoverTrafficGenerator : ICoverTrafficGenerator, IDisposable
{
    private readonly CoverTrafficOptions _options;
    private readonly Func<byte[]> _coverMessageFactory;
    private readonly Func<Task> _sendCoverMessageAsync;
    private readonly ILogger<CoverTrafficGenerator> _logger;

    private CancellationTokenSource? _generationCts;
    private Task? _generationTask;
    private DateTimeOffset _lastTrafficTime = DateTimeOffset.UtcNow;
    private long _coverMessagesSent;
    private readonly object _statsLock = new();
    private bool _disposed;

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
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_generationTask != null && !_generationTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _generationCts?.Cancel();
        _generationCts?.Dispose();
        var generationCts = new CancellationTokenSource();
        _generationCts = generationCts;
        _generationTask = Task.Factory.StartNew(() => GenerateCoverTraffic(generationCts.Token), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        _logger.LogInformation("Cover traffic generation started with interval {Interval}s", _options.IntervalSeconds);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the cover traffic generation.
    /// </summary>
    /// <returns>A task that completes when generation stops.</returns>
    public Task StopAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        _generationCts?.Cancel();

        if (_generationTask != null)
        {
            try
            {
                if (!_generationTask.Wait(TimeSpan.FromSeconds(5)))
                    _logger.LogWarning("Cover traffic generation did not stop cleanly within timeout");
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
            {
                // Expected on cancellation
            }
            finally
            {
                _generationTask = null;
                _generationCts?.Dispose();
                _generationCts = null;
            }
        }

        _logger.LogInformation("Cover traffic generation stopped");
        return Task.CompletedTask;
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
                IsActive = _generationTask != null && !_generationTask.IsCompleted && _generationCts?.IsCancellationRequested == false,
            };
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _generationCts?.Cancel();
        _generationCts?.Dispose();
        _generationTask = null;
        _generationCts = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Runs the cover traffic generation loop on a dedicated LongRunning OS thread.
    /// Uses WaitHandle.WaitOne for the interval so that Cancel() wakes the thread
    /// synchronously — no thread-pool continuation needed, making Stop() reliably fast
    /// under thread-pool saturation.
    /// </summary>
    private void GenerateCoverTraffic(CancellationToken cancellationToken)
    {
        var intervalMs = _options.IntervalSeconds * 1000;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Block the dedicated thread until the interval elapses or cancelled.
            cancellationToken.WaitHandle.WaitOne(intervalMs);
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
                    _sendCoverMessageAsync().GetAwaiter().GetResult();

                    lock (_statsLock)
                    {
                        _coverMessagesSent++;
                    }

                    _logger.LogDebug("Sent cover traffic message ({Total} total)", _coverMessagesSent);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send cover traffic message");
                }
            }
        }
    }
}
