// <copyright file="CoverTrafficGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Privacy;

/// <summary>
/// Generates cover traffic to maintain constant communication patterns and prevent traffic analysis.
/// Sends dummy messages at regular intervals when no real traffic is present.
/// </summary>
public class CoverTrafficGenerator : ICoverTrafficGenerator
{
    private readonly ILogger<CoverTrafficGenerator> _logger;
    private readonly RandomNumberGenerator _rng;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _jitterRange;
    private readonly int _messageSize;
    private DateTimeOffset _lastActivity;
    private DateTimeOffset _lastCoverTraffic;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoverTrafficGenerator"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="intervalSeconds">The base interval between cover traffic messages (default: 30).</param>
    /// <param name="jitterRangeSeconds">The random jitter range in seconds (default: 5).</param>
    /// <param name="messageSize">The size of cover traffic messages in bytes (default: 64).</param>
    public CoverTrafficGenerator(
        ILogger<CoverTrafficGenerator> logger,
        double intervalSeconds = 30.0,
        double jitterRangeSeconds = 5.0,
        int messageSize = 64)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rng = RandomNumberGenerator.Create();

        if (intervalSeconds <= 0)
        {
            throw new ArgumentException("Interval must be positive", nameof(intervalSeconds));
        }

        if (jitterRangeSeconds < 0)
        {
            throw new ArgumentException("Jitter range cannot be negative", nameof(jitterRangeSeconds));
        }

        if (messageSize <= 0)
        {
            throw new ArgumentException("Message size must be positive", nameof(messageSize));
        }

        _interval = TimeSpan.FromSeconds(intervalSeconds);
        _jitterRange = TimeSpan.FromSeconds(jitterRangeSeconds);
        _messageSize = messageSize;
        _lastActivity = DateTimeOffset.UtcNow;
        _lastCoverTraffic = DateTimeOffset.UtcNow;

        _logger.LogDebug("CoverTrafficGenerator initialized with interval {Interval}s, jitter {Jitter}s, message size {Size} bytes",
            intervalSeconds, jitterRangeSeconds, messageSize);
    }

    /// <summary>
    /// Generates cover traffic messages at appropriate intervals.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An enumerable of cover traffic messages to send.</returns>
    public async IAsyncEnumerable<byte[]> GenerateCoverTrafficAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting cover traffic generation");

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var timeSinceLastCover = now - _lastCoverTraffic;
            var timeSinceLastActivity = now - _lastActivity;

            // Generate cover traffic if enough time has passed and no recent activity
            if (timeSinceLastCover >= GetNextInterval() && timeSinceLastActivity >= _interval)
            {
                var coverMessage = GenerateCoverMessage();
                _lastCoverTraffic = now;

                _logger.LogTrace("Generated cover traffic message ({Size} bytes)", coverMessage.Length);
                yield return coverMessage;

                // Wait a bit before checking again to avoid busy looping
                await Task.Delay(100, cancellationToken);
            }
            else
            {
                // Wait until it's time to check again
                var waitTime = Math.Min(1000, (int)GetNextInterval().TotalMilliseconds / 4);
                await Task.Delay(waitTime, cancellationToken);
            }
        }

        _logger.LogDebug("Cover traffic generation stopped");
    }

    /// <summary>
    /// Records real message activity to adjust cover traffic generation.
    /// </summary>
    public void RecordActivity()
    {
        _lastActivity = DateTimeOffset.UtcNow;
        _logger.LogTrace("Recorded real message activity");
    }

    /// <summary>
    /// Gets the time until the next cover traffic message should be generated.
    /// </summary>
    public TimeSpan TimeUntilNextCoverTraffic()
    {
        var now = DateTimeOffset.UtcNow;
        var timeSinceLastActivity = now - _lastActivity;

        // Don't generate cover traffic if there was recent activity
        if (timeSinceLastActivity < _interval)
        {
            return _interval - timeSinceLastActivity;
        }

        var timeSinceLastCover = now - _lastCoverTraffic;
        var nextInterval = GetNextInterval();

        if (timeSinceLastCover >= nextInterval)
        {
            return TimeSpan.Zero;
        }

        return nextInterval - timeSinceLastCover;
    }

    /// <summary>
    /// Gets whether cover traffic should be generated now.
    /// </summary>
    public bool ShouldGenerateCoverTraffic()
    {
        var now = DateTimeOffset.UtcNow;
        var timeSinceLastActivity = now - _lastActivity;
        var timeSinceLastCover = now - _lastCoverTraffic;

        return timeSinceLastActivity >= _interval && timeSinceLastCover >= GetNextInterval();
    }

    private TimeSpan GetNextInterval()
    {
        // Add random jitter to prevent predictable patterns
        var jitterBytes = new byte[4];
        _rng.GetBytes(jitterBytes);
        var jitterValue = BitConverter.ToUInt32(jitterBytes, 0) / (double)uint.MaxValue;

        var jitterOffset = _jitterRange.TotalMilliseconds * (2 * jitterValue - 1); // -jitterRange to +jitterRange
        var totalInterval = _interval.TotalMilliseconds + jitterOffset;

        return TimeSpan.FromMilliseconds(Math.Max(1000, totalInterval)); // Minimum 1 second
    }

    private byte[] GenerateCoverMessage()
    {
        var message = new byte[_messageSize];
        _rng.GetBytes(message);

        // Mark as cover traffic (optional - for debugging/analysis)
        // First byte indicates this is cover traffic
        message[0] = 0xFF;

        return message;
    }

    /// <summary>
    /// Checks if a message is cover traffic.
    /// </summary>
    /// <param name="message">The message to check.</param>
    /// <returns>True if the message is cover traffic.</returns>
    public static bool IsCoverTraffic(byte[] message)
    {
        return message.Length > 0 && message[0] == 0xFF;
    }

    /// <summary>
    /// Creates cover traffic generators with predefined configurations for different privacy levels.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Minimal cover traffic (60s interval) - Low bandwidth overhead.
        /// </summary>
        public static CoverTrafficGenerator Minimal(ILogger<CoverTrafficGenerator> logger)
            => new(logger, 60.0, 10.0, 32);

        /// <summary>
        /// Standard cover traffic (30s interval) - Balanced privacy and overhead.
        /// </summary>
        public static CoverTrafficGenerator Standard(ILogger<CoverTrafficGenerator> logger)
            => new(logger, 30.0, 5.0, 64);

        /// <summary>
        /// High cover traffic (15s interval) - Good privacy, higher bandwidth.
        /// </summary>
        public static CoverTrafficGenerator High(ILogger<CoverTrafficGenerator> logger)
            => new(logger, 15.0, 3.0, 128);

        /// <summary>
        /// Maximum cover traffic (5s interval) - Maximum privacy, significant bandwidth.
        /// </summary>
        public static CoverTrafficGenerator Maximum(ILogger<CoverTrafficGenerator> logger)
            => new(logger, 5.0, 1.0, 256);
    }
}


