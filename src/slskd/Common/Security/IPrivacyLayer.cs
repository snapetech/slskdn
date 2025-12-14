// <copyright file="IPrivacyLayer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for privacy transformations (padding, timing, batching).
/// Composable middleware pattern for message processing.
/// </summary>
public interface IPrivacyLayer
{
    /// <summary>
    /// Transforms outbound message with privacy protections.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>Transformed message bytes.</returns>
    Task<byte[]> TransformOutboundAsync(byte[] message, IReadOnlyDictionary<string, object>? metadata = null);

    /// <summary>
    /// Transforms inbound message by removing privacy protections.
    /// </summary>
    /// <param name="message">The received message bytes.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>Original message bytes.</returns>
    Task<byte[]> TransformInboundAsync(byte[] message, IReadOnlyDictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets current privacy statistics.
    /// </summary>
    /// <returns>Privacy metrics.</returns>
    Task<PrivacyStatistics> GetStatisticsAsync();
}

/// <summary>
/// Privacy layer statistics.
/// </summary>
public class PrivacyStatistics
{
    /// <summary>
    /// Gets the number of outbound messages processed.
    /// </summary>
    public long OutboundMessagesProcessed { get; set; }

    /// <summary>
    /// Gets the number of inbound messages processed.
    /// </summary>
    public long InboundMessagesProcessed { get; set; }

    /// <summary>
    /// Gets the total padding bytes added.
    /// </summary>
    public long TotalPaddingBytes { get; set; }

    /// <summary>
    /// Gets the average processing latency in milliseconds.
    /// </summary>
    public double AverageProcessingLatencyMs { get; set; }

    /// <summary>
    /// Gets the number of batches created.
    /// </summary>
    public long BatchesCreated { get; set; }

    /// <summary>
    /// Gets the number of cover messages sent.
    /// </summary>
    public long CoverMessagesSent { get; set; }
}

