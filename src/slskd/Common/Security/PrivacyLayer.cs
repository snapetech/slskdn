// <copyright file="PrivacyLayer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Privacy layer implementation that applies multiple privacy transformations.
/// </summary>
public class PrivacyLayer : IPrivacyLayer
{
    private readonly PrivacyLayerOptions _options;
    private readonly IMessagePadder? _padder;
    private readonly ITimingObfuscator? _timingObfuscator;
    private readonly IMessageBatcher? _batcher;
    private readonly ICoverTrafficGenerator? _coverTrafficGenerator;
    private readonly ILogger<PrivacyLayer> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // Statistics
    private long _outboundMessagesProcessed;
    private long _inboundMessagesProcessed;
    private long _totalPaddingBytes;
    private TimeSpan _averageProcessingLatency = TimeSpan.Zero;
    private readonly object _statsLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PrivacyLayer"/> class.
    /// </summary>
    /// <param name="options">The privacy layer options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public PrivacyLayer(PrivacyLayerOptions options, ILogger<PrivacyLayer> logger, ILoggerFactory loggerFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        // Initialize components if enabled
        if (_options.Padding?.Enabled == true)
        {
            _padder = new BucketPadder(_options.Padding);
        }

        if (_options.Timing?.Enabled == true)
        {
            _timingObfuscator = new RandomJitterObfuscator(_options.Timing);
        }

        if (_options.Batching?.Enabled == true)
        {
            _batcher = new TimedBatcher(_options.Batching, _loggerFactory.CreateLogger<TimedBatcher>());
        }

        if (_options.CoverTraffic?.Enabled == true)
        {
            // Create a dummy cover message factory and sender for now
            // In real implementation, these would be provided by the messaging layer
            _coverTrafficGenerator = new CoverTrafficGenerator(
                _options.CoverTraffic,
                () => new byte[] { 0x00 }, // Dummy cover message
                () => Task.CompletedTask, // Dummy sender
                _loggerFactory.CreateLogger<CoverTrafficGenerator>());
        }
    }

    /// <summary>
    /// Transforms outbound message with privacy protections.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>Transformed message bytes.</returns>
    public async Task<byte[]> TransformOutboundAsync(byte[] message, IReadOnlyDictionary<string, object>? metadata = null)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var startTime = DateTimeOffset.UtcNow;
        byte[] transformedMessage = message;

        try
        {
            // Apply padding if enabled
            if (_padder != null)
            {
                transformedMessage = _padder.Pad(transformedMessage);
                lock (_statsLock)
                {
                    _totalPaddingBytes += transformedMessage.Length - message.Length;
                }
            }

            // Apply timing obfuscation if enabled
            if (_timingObfuscator != null && _options.Timing?.JitterAllMessages == true)
            {
                var delay = await _timingObfuscator.GetNextDelayAsync();
                if (delay > 0)
                {
                    await Task.Delay(delay);
                }
            }

            // Add to batch if batching is enabled
            if (_batcher != null)
            {
                await _batcher.AddMessageAsync(transformedMessage, metadata);
                // For batched messages, we return empty array as they're not sent immediately
                transformedMessage = Array.Empty<byte>();
            }

            // Notify cover traffic generator of real traffic
            _coverTrafficGenerator?.NotifyTraffic();

            lock (_statsLock)
            {
                _outboundMessagesProcessed++;
                var processingTime = DateTimeOffset.UtcNow - startTime;
                _averageProcessingLatency = TimeSpan.FromTicks(
                    (_averageProcessingLatency.Ticks + processingTime.Ticks) / 2);
            }

            return transformedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming outbound message");
            // Return original message on error to avoid breaking communication
            return message;
        }
    }

    /// <summary>
    /// Transforms inbound message by removing privacy protections.
    /// </summary>
    /// <param name="message">The received message bytes.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>Original message bytes.</returns>
    public async Task<byte[]> TransformInboundAsync(byte[] message, IReadOnlyDictionary<string, object>? metadata = null)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        var startTime = DateTimeOffset.UtcNow;
        byte[] transformedMessage = message;

        try
        {
            // Remove padding if enabled
            if (_padder != null)
            {
                transformedMessage = _padder.Unpad(transformedMessage);
            }

            lock (_statsLock)
            {
                _inboundMessagesProcessed++;
                var processingTime = DateTimeOffset.UtcNow - startTime;
                _averageProcessingLatency = TimeSpan.FromTicks(
                    (_averageProcessingLatency.Ticks + processingTime.Ticks) / 2);
            }

            return transformedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming inbound message");
            // Return original message on error
            return message;
        }
    }

    /// <summary>
    /// Gets current privacy statistics.
    /// </summary>
    /// <returns>Privacy metrics.</returns>
    public Task<PrivacyStatistics> GetStatisticsAsync()
    {
        lock (_statsLock)
        {
            return Task.FromResult(new PrivacyStatistics
            {
                OutboundMessagesProcessed = _outboundMessagesProcessed,
                InboundMessagesProcessed = _inboundMessagesProcessed,
                TotalPaddingBytes = _totalPaddingBytes,
                AverageProcessingLatencyMs = _averageProcessingLatency.TotalMilliseconds,
                BatchesCreated = 0, // TODO: Track batching stats
                CoverMessagesSent = _coverTrafficGenerator?.GetStats().CoverMessagesSent ?? 0,
            });
        }
    }

    /// <summary>
    /// Gets the next batch of messages if batching is enabled.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The batch of messages, or empty list if batching is disabled.</returns>
    public async Task<IReadOnlyList<BatchedMessage>> GetNextBatchAsync(CancellationToken cancellationToken = default)
    {
        if (_batcher == null)
            return Array.Empty<BatchedMessage>();

        return await _batcher.GetNextBatchAsync(cancellationToken);
    }

    /// <summary>
    /// Forces immediate sending of the current batch.
    /// </summary>
    /// <returns>The current batch of messages.</returns>
    public async Task<IReadOnlyList<BatchedMessage>> FlushBatchAsync()
    {
        if (_batcher == null)
            return Array.Empty<BatchedMessage>();

        return await _batcher.FlushAsync();
    }

    /// <summary>
    /// Starts the privacy layer components (like cover traffic generation).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_coverTrafficGenerator != null)
        {
            await _coverTrafficGenerator.StartAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Stops the privacy layer components.
    /// </summary>
    public async Task StopAsync()
    {
        if (_coverTrafficGenerator != null)
        {
            await _coverTrafficGenerator.StopAsync();
        }
    }
}
