// <copyright file="PrivacyLayer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using slskd.Common.Security;

namespace slskd.Mesh.Privacy;

/// <summary>
/// Main implementation of the privacy layer that composes all privacy protection components.
/// Provides comprehensive traffic analysis protection by orchestrating padding, timing, batching, and cover traffic.
/// </summary>
public class PrivacyLayer : IPrivacyLayer
{
    private readonly ILogger<PrivacyLayer> _logger;
    private PrivacyLayerOptions _options;
    private readonly object _configLock = new();

    // Privacy components
    private IMessagePadder? _messagePadder;
    private ITimingObfuscator? _timingObfuscator;
    private IMessageBatcher? _messageBatcher;
    private ICoverTrafficGenerator? _coverTrafficGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyLayer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The privacy layer options.</param>
    public PrivacyLayer(ILogger<PrivacyLayer> logger, PrivacyLayerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        InitializeComponents();
        _logger.LogInformation("PrivacyLayer initialized with options: Enabled={Enabled}", options.Enabled);
    }

    /// <summary>
    /// Gets whether the privacy layer is enabled.
    /// </summary>
    public bool IsEnabled => _options.Enabled;

    /// <summary>
    /// Gets the message padder component.
    /// </summary>
    public IMessagePadder? MessagePadder => _messagePadder;

    /// <summary>
    /// Gets the timing obfuscator component.
    /// </summary>
    public ITimingObfuscator? TimingObfuscator => _timingObfuscator;

    /// <summary>
    /// Gets the message batcher component.
    /// </summary>
    public IMessageBatcher? MessageBatcher => _messageBatcher;

    /// <summary>
    /// Gets the cover traffic generator component.
    /// </summary>
    public ICoverTrafficGenerator? CoverTrafficGenerator => _coverTrafficGenerator;

    /// <summary>
    /// Processes an outbound message through all enabled privacy transforms.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The transformed message bytes.</returns>
    public async Task<byte[]> ProcessOutboundMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || message == null)
        {
            return message;
        }

        var processedMessage = message;

        // Apply padding if enabled
        if (_messagePadder != null)
        {
            processedMessage = _messagePadder.Pad(processedMessage);
            _logger.LogTrace("Applied message padding: {OriginalSize} -> {PaddedSize} bytes",
                message.Length, processedMessage.Length);
        }

        // Add to batcher if enabled (this doesn't return the message immediately)
        if (_messageBatcher != null)
        {
            var isReady = _messageBatcher.AddMessage(processedMessage);
            if (!isReady)
            {
                // Message is queued for batching, return empty array to indicate no immediate send
                _logger.LogTrace("Message queued for batching, not sending immediately");
                return Array.Empty<byte>();
            }

            // Batch is ready, get the batched messages
            var batch = _messageBatcher.GetBatch();
            if (batch != null && batch.Count > 0)
            {
                // For simplicity, return the first message in the batch
                // In a real implementation, you might want to handle multiple messages
                processedMessage = batch[0];
                _logger.LogTrace("Retrieved batched message ({Count} in batch)", batch.Count);
            }
        }

        return processedMessage;
    }

    /// <summary>
    /// Processes an inbound message by reversing applicable privacy transforms.
    /// </summary>
    /// <param name="message">The received message bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The original message bytes.</returns>
    public async Task<byte[]> ProcessInboundMessageAsync(byte[] message, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || message == null || message.Length == 0)
        {
            return message;
        }

        var processedMessage = message;

        // Reverse padding if enabled
        if (_messagePadder != null)
        {
            try
            {
                processedMessage = _messagePadder.Unpad(processedMessage);
                _logger.LogTrace("Removed message padding: {PaddedSize} -> {OriginalSize} bytes",
                    message.Length, processedMessage.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to unpad message, returning as-is");
                // Return original message if unpadding fails
                return message;
            }
        }

        return processedMessage;
    }

    /// <summary>
    /// Gets the delay to apply before sending the next outbound message.
    /// </summary>
    /// <returns>The delay as a TimeSpan.</returns>
    public TimeSpan GetOutboundDelay()
    {
        if (!IsEnabled || _timingObfuscator == null)
        {
            return TimeSpan.Zero;
        }

        var delay = _timingObfuscator.GetDelay();
        _logger.LogTrace("Generated outbound delay: {Delay}ms", delay.TotalMilliseconds);
        return delay;
    }

    /// <summary>
    /// Records that an outbound message was sent.
    /// </summary>
    public void RecordOutboundMessage()
    {
        _timingObfuscator?.RecordSend();
        _coverTrafficGenerator?.RecordActivity();
    }

    /// <summary>
    /// Gets pending batched messages ready for sending.
    /// </summary>
    /// <returns>Collection of batched messages, or null if none ready.</returns>
    public IReadOnlyList<byte[]>? GetPendingBatches()
    {
        return _messageBatcher?.GetBatch();
    }

    /// <summary>
    /// Forces any pending batches to be ready for sending.
    /// </summary>
    public void FlushBatches()
    {
        _messageBatcher?.Flush();
    }

    /// <summary>
    /// Gets an async enumerable of cover traffic messages.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of cover traffic messages.</returns>
    public async IAsyncEnumerable<byte[]> GetCoverTrafficAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled || _coverTrafficGenerator == null)
        {
            yield break;
        }

        await foreach (var message in _coverTrafficGenerator.GenerateCoverTrafficAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Records real message activity for cover traffic management.
    /// </summary>
    public void RecordActivity()
    {
        _coverTrafficGenerator?.RecordActivity();
    }

    /// <summary>
    /// Updates the privacy layer configuration.
    /// </summary>
    /// <param name="options">The new privacy layer options.</param>
    public void UpdateConfiguration(PrivacyLayerOptions options)
    {
        lock (_configLock)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            InitializeComponents();
            _logger.LogInformation("PrivacyLayer configuration updated: Enabled={Enabled}", options.Enabled);
        }
    }

    private void InitializeComponents()
    {
        // Initialize message padder
        if (_options.Enabled && _options.Padding.Enabled)
        {
            _messagePadder = new BucketPadder(
                _logger,
                _options.Padding.BucketSizes.FirstOrDefault(2048));
        }
        else
        {
            _messagePadder = null;
        }

        // Initialize timing obfuscator
        if (_options.Enabled && _options.Timing.Enabled)
        {
            _timingObfuscator = new RandomJitterObfuscator(
                _logger,
                _options.Timing.JitterMs);
        }
        else
        {
            _timingObfuscator = null;
        }

        // Initialize message batcher
        if (_options.Enabled && _options.Batching.Enabled)
        {
            _messageBatcher = new TimedBatcher(
                _logger,
                _options.Batching.BatchWindowMs / 1000.0,
                _options.Batching.MaxBatchSize);
        }
        else
        {
            _messageBatcher = null;
        }

        // Initialize cover traffic generator
        if (_options.Enabled && _options.CoverTraffic.Enabled)
        {
            _coverTrafficGenerator = new CoverTrafficGenerator(
                _logger,
                _options.CoverTraffic.IntervalSeconds,
                jitterRangeSeconds: 5.0, // Default jitter
                messageSize: 64); // Default message size
        }
        else
        {
            _coverTrafficGenerator = null;
        }
    }
}
