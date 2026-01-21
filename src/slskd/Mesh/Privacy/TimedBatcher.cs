// <copyright file="TimedBatcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace slskd.Mesh.Privacy;

/// <summary>
/// Implements message batching with time-based windows to prevent traffic analysis.
/// Collects messages for a configurable time window and sends them as a batch.
/// </summary>
public class TimedBatcher : IMessageBatcher
{
    private readonly ILogger<TimedBatcher> _logger;
    private readonly TimeSpan _batchWindow;
    private readonly int _maxBatchSize;
    private readonly ConcurrentQueue<byte[]> _messageQueue;
    private readonly SemaphoreSlim _batchLock;
    private DateTimeOffset _batchStartTime;
    private bool _isBatchActive;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimedBatcher"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="batchWindowSeconds">The batch window in seconds (default: 2).</param>
    /// <param name="maxBatchSize">The maximum number of messages per batch (default: 10).</param>
    public TimedBatcher(ILogger<TimedBatcher> logger, double batchWindowSeconds = 2.0, int maxBatchSize = 10)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (batchWindowSeconds <= 0)
        {
            throw new ArgumentException("Batch window must be positive", nameof(batchWindowSeconds));
        }

        if (maxBatchSize <= 0)
        {
            throw new ArgumentException("Max batch size must be positive", nameof(maxBatchSize));
        }

        _batchWindow = TimeSpan.FromSeconds(batchWindowSeconds);
        _maxBatchSize = maxBatchSize;
        _messageQueue = new ConcurrentQueue<byte[]>();
        _batchLock = new SemaphoreSlim(1, 1);

        _logger.LogDebug("TimedBatcher initialized with window {Window}s, max batch size {MaxSize}",
            batchWindowSeconds, maxBatchSize);
    }

    /// <summary>
    /// Gets whether a batch is currently ready to send.
    /// </summary>
    public bool HasBatch => _isBatchActive && IsBatchReady();

    /// <summary>
    /// Adds a message to the current batch.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <returns>True if the batch is ready to send, false if more messages should be collected.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    public bool AddMessage(byte[] message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _batchLock.Wait();
        try
        {
            // Start new batch if needed
            if (!_isBatchActive)
            {
                StartNewBatch();
            }

            _messageQueue.Enqueue(message);
            _logger.LogTrace("Added message to batch (queue size: {Size})", _messageQueue.Count);

            // Check if batch is ready
            if (IsBatchReady())
            {
                _logger.LogDebug("Batch ready for sending (size: {Size})", _messageQueue.Count);
                return true;
            }

            return false;
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Gets the current batch of messages.
    /// </summary>
    /// <returns>The batched messages, or null if no batch is ready.</returns>
    public IReadOnlyList<byte[]>? GetBatch()
    {
        _batchLock.Wait();
        try
        {
            if (!HasBatch)
            {
                return null;
            }

            var messages = new List<byte[]>();
            while (_messageQueue.TryDequeue(out var message))
            {
                messages.Add(message);
            }

            _isBatchActive = false;

            _logger.LogDebug("Retrieved batch with {Count} messages", messages.Count);
            return messages;
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Forces the current batch to be ready for sending.
    /// </summary>
    public void Flush()
    {
        _batchLock.Wait();
        try
        {
            if (_isBatchActive)
            {
                _logger.LogDebug("Flushing batch with {Count} messages", _messageQueue.Count);
                _isBatchActive = false;
            }
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Gets the current batch size.
    /// </summary>
    public int CurrentBatchSize
    {
        get
        {
            _batchLock.Wait();
            try
            {
                return _messageQueue.Count;
            }
            finally
            {
                _batchLock.Release();
            }
        }
    }

    /// <summary>
    /// Gets the time remaining in the current batch window.
    /// </summary>
    public TimeSpan TimeRemainingInBatch
    {
        get
        {
            _batchLock.Wait();
            try
            {
                if (!_isBatchActive)
                {
                    return TimeSpan.Zero;
                }

                var elapsed = DateTimeOffset.UtcNow - _batchStartTime;
                var remaining = _batchWindow - elapsed;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
            finally
            {
                _batchLock.Release();
            }
        }
    }

    private void StartNewBatch()
    {
        _batchStartTime = DateTimeOffset.UtcNow;
        _isBatchActive = true;
        _logger.LogTrace("Started new batch at {Time}", _batchStartTime);
    }

    private bool IsBatchReady()
    {
        if (!_isBatchActive)
        {
            return false;
        }

        // Check time window
        var elapsed = DateTimeOffset.UtcNow - _batchStartTime;
        if (elapsed >= _batchWindow)
        {
            _logger.LogTrace("Batch ready due to time window ({Elapsed}s >= {Window}s)",
                elapsed.TotalSeconds, _batchWindow.TotalSeconds);
            return true;
        }

        // Check batch size
        if (_messageQueue.Count >= _maxBatchSize)
        {
            _logger.LogTrace("Batch ready due to max size ({Size} >= {MaxSize})",
                _messageQueue.Count, _maxBatchSize);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates batchers with predefined configurations for different privacy levels.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Low latency batching (0.5s window, max 5 messages) - Minimal privacy impact.
        /// </summary>
        public static TimedBatcher LowLatency(ILogger<TimedBatcher> logger)
            => new(logger, 0.5, 5);

        /// <summary>
        /// Standard batching (2s window, max 10 messages) - Balanced privacy and latency.
        /// </summary>
        public static TimedBatcher Standard(ILogger<TimedBatcher> logger)
            => new(logger, 2.0, 10);

        /// <summary>
        /// High privacy batching (5s window, max 20 messages) - Good privacy, higher latency.
        /// </summary>
        public static TimedBatcher HighPrivacy(ILogger<TimedBatcher> logger)
            => new(logger, 5.0, 20);

        /// <summary>
        /// Maximum privacy batching (10s window, max 50 messages) - Maximum privacy, significant latency.
        /// </summary>
        public static TimedBatcher MaximumPrivacy(ILogger<TimedBatcher> logger)
            => new(logger, 10.0, 50);
    }
}


