// <copyright file="RandomJitterObfuscator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Privacy;

/// <summary>
/// Implements random timing jitter to prevent traffic analysis by obscuring message send timing patterns.
/// Adds configurable random delays to outbound messages to break timing correlation attacks.
/// </summary>
public class RandomJitterObfuscator : ITimingObfuscator
{
    private readonly ILogger<RandomJitterObfuscator> _logger;
    private readonly RandomNumberGenerator _rng;
    private readonly TimeSpan _minDelay;
    private readonly TimeSpan _maxDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomJitterObfuscator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="minDelayMs">The minimum delay in milliseconds (default: 0).</param>
    /// <param name="maxDelayMs">The maximum delay in milliseconds (default: 500).</param>
    public RandomJitterObfuscator(ILogger<RandomJitterObfuscator> logger, int minDelayMs = 0, int maxDelayMs = 500)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rng = RandomNumberGenerator.Create();

        if (minDelayMs < 0)
        {
            throw new ArgumentException("Minimum delay cannot be negative", nameof(minDelayMs));
        }

        if (maxDelayMs < minDelayMs)
        {
            throw new ArgumentException("Maximum delay cannot be less than minimum delay", nameof(maxDelayMs));
        }

        _minDelay = TimeSpan.FromMilliseconds(minDelayMs);
        _maxDelay = TimeSpan.FromMilliseconds(maxDelayMs);

        _logger.LogDebug("RandomJitterObfuscator initialized with delay range {MinDelay}ms - {MaxDelay}ms",
            minDelayMs, maxDelayMs);
    }

    /// <summary>
    /// Gets the delay to apply before sending the next message.
    /// </summary>
    /// <returns>The delay as a TimeSpan.</returns>
    public TimeSpan GetDelay()
    {
        var delayRange = _maxDelay - _minDelay;
        if (delayRange.TotalMilliseconds <= 0)
        {
            return _minDelay;
        }

        // Generate random delay within the range
        var randomBytes = new byte[4];
        _rng.GetBytes(randomBytes);
        var randomValue = BitConverter.ToUInt32(randomBytes, 0);
        var normalizedValue = randomValue / (double)uint.MaxValue;

        var delayMs = _minDelay.TotalMilliseconds + (normalizedValue * delayRange.TotalMilliseconds);
        var delay = TimeSpan.FromMilliseconds(delayMs);

        _logger.LogTrace("Generated timing jitter delay: {Delay}ms", delay.TotalMilliseconds);
        return delay;
    }

    /// <summary>
    /// Records that a message was sent (for adaptive timing if needed).
    /// This implementation does not use adaptive timing, but the method is provided
    /// for interface compliance and potential future enhancements.
    /// </summary>
    public void RecordSend()
    {
        // No-op for basic random jitter implementation
        // Future versions could implement adaptive timing based on send patterns
    }

    /// <summary>
    /// Gets the minimum delay configured for this obfuscator.
    /// </summary>
    public TimeSpan MinDelay => _minDelay;

    /// <summary>
    /// Gets the maximum delay configured for this obfuscator.
    /// </summary>
    public TimeSpan MaxDelay => _maxDelay;

    /// <summary>
    /// Gets the average delay for this obfuscator.
    /// </summary>
    public TimeSpan AverageDelay => TimeSpan.FromMilliseconds((_minDelay.TotalMilliseconds + _maxDelay.TotalMilliseconds) / 2);

    /// <summary>
    /// Creates a timing obfuscator with predefined delay ranges for different privacy levels.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Minimal timing obfuscation (10-50ms) - Low latency, minimal privacy.
        /// </summary>
        public static RandomJitterObfuscator Low(ILogger<RandomJitterObfuscator> logger)
            => new(logger, 10, 50);

        /// <summary>
        /// Standard timing obfuscation (50-200ms) - Balanced latency and privacy.
        /// </summary>
        public static RandomJitterObfuscator Standard(ILogger<RandomJitterObfuscator> logger)
            => new(logger, 50, 200);

        /// <summary>
        /// High timing obfuscation (100-500ms) - Good privacy, higher latency.
        /// </summary>
        public static RandomJitterObfuscator High(ILogger<RandomJitterObfuscator> logger)
            => new(logger, 100, 500);

        /// <summary>
        /// Maximum timing obfuscation (200-1000ms) - Maximum privacy, significant latency.
        /// </summary>
        public static RandomJitterObfuscator Maximum(ILogger<RandomJitterObfuscator> logger)
            => new(logger, 200, 1000);
    }
}

