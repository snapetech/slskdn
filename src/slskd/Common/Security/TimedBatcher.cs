// <copyright file="TimedBatcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.Security;

/// <summary>
/// Timed message batcher that holds messages for a configurable window and sends them as batches.
/// </summary>
public class TimedBatcher : IMessageBatcher, IDisposable
{
    private readonly MessageBatchingOptions _options;
    private readonly List<BatchedMessage> _currentBatch = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private readonly ILogger<TimedBatcher> _logger;
    private static readonly TimeSpan BatchPollInterval = TimeSpan.FromMilliseconds(50);

    private CancellationTokenSource? _currentBatchTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimedBatcher"/> class.
    /// </summary>
    /// <param name="options">The batching options.</param>
    /// <param name="logger">The logger.</param>
    public TimedBatcher(MessageBatchingOptions options, ILogger<TimedBatcher> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds a message to the current batch.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>A task that completes when the message is queued.</returns>
    public async Task AddMessageAsync(byte[] message, IReadOnlyDictionary<string, object>? metadata = null)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var batchedMessage = new BatchedMessage(message, metadata, DateTimeOffset.UtcNow);

        await _batchLock.WaitAsync();
        try
        {
            _currentBatch.Add(batchedMessage);

            // If we've reached the maximum batch size, cancel the current timer
            if (_currentBatch.Count >= _options.MaxBatchSize)
            {
                CancelAndDisposeCurrentBatchTimer();
            }
            else if (_currentBatch.Count == 1)
            {
                // This is the first message in the batch, start the timer
                StartBatchTimer();
            }
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Gets the next batch of messages, waiting if necessary for the batch window.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The batch of messages.</returns>
    public async Task<IReadOnlyList<BatchedMessage>> GetNextBatchAsync(CancellationToken cancellationToken = default)
    {
        // Wait for either the batch timer to expire or the batch to reach max size
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var waitTask = WaitForBatchAsync(linkedCts.Token);

        try
        {
            await waitTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // External cancellation, return current batch
            _logger.LogDebug("Batch retrieval cancelled, returning current batch");
        }

        // Extract the current batch.
        // Use CancellationToken.None: if cancellationToken was already cancelled (the reason we
        // fell through the catch above), passing it here would throw immediately and lose the batch.
        await _batchLock.WaitAsync(CancellationToken.None);
        try
        {
            var batch = _currentBatch.ToList();
            _currentBatch.Clear();

            // Cancel any existing timer
            CancelAndDisposeCurrentBatchTimer();

            _logger.LogDebug("Returning batch with {Count} messages", batch.Count);
            return batch;
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Forces immediate sending of the current batch.
    /// </summary>
    /// <returns>The current batch of messages.</returns>
    public async Task<IReadOnlyList<BatchedMessage>> FlushAsync()
    {
        await _batchLock.WaitAsync();
        try
        {
            var batch = _currentBatch.ToList();
            _currentBatch.Clear();

            // Cancel any existing timer
            CancelAndDisposeCurrentBatchTimer();

            _logger.LogDebug("Flushed batch with {Count} messages", batch.Count);
            return batch;
        }
        finally
        {
            _batchLock.Release();
        }
    }

    private void StartBatchTimer()
    {
        CancelAndDisposeCurrentBatchTimer();
        _currentBatchTimer = new CancellationTokenSource();

        _ = LogBatchTimerExpiryAsync(_currentBatchTimer.Token);
    }

    private void CancelAndDisposeCurrentBatchTimer()
    {
        var currentBatchTimer = _currentBatchTimer;
        _currentBatchTimer = null;

        currentBatchTimer?.Cancel();
        currentBatchTimer?.Dispose();
    }

    private async Task WaitForBatchAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _batchLock.WaitAsync(cancellationToken);
            try
            {
                if (_currentBatch.Count >= _options.MaxBatchSize)
                {
                    // Batch is full
                    return;
                }

                if (_currentBatch.Count > 0
                    && DateTimeOffset.UtcNow - _currentBatch[0].Timestamp >= TimeSpan.FromMilliseconds(_options.BatchWindowMs))
                {
                    // Timer has expired
                    return;
                }
            }
            finally
            {
                _batchLock.Release();
            }

            // Wait a bit before checking again
            await Task.Delay(BatchPollInterval, cancellationToken);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        CancelAndDisposeCurrentBatchTimer();
        _batchLock.Dispose();
    }

    private async Task LogBatchTimerExpiryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_options.BatchWindowMs, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Batch timer expired, batch ready for processing");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
