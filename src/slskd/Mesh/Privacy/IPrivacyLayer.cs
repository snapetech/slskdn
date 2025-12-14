// <copyright file="IPrivacyLayer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using slskd.Common.Security;

namespace slskd.Mesh.Privacy;

/// <summary>
/// Main interface for the privacy layer that composes all privacy protection components.
/// Provides comprehensive traffic analysis protection for adversarial environments.
/// </summary>
public interface IPrivacyLayer
{
    /// <summary>
    /// Gets whether the privacy layer is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the message padder component.
    /// </summary>
    IMessagePadder? MessagePadder { get; }

    /// <summary>
    /// Gets the timing obfuscator component.
    /// </summary>
    ITimingObfuscator? TimingObfuscator { get; }

    /// <summary>
    /// Gets the message batcher component.
    /// </summary>
    IMessageBatcher? MessageBatcher { get; }

    /// <summary>
    /// Gets the cover traffic generator component.
    /// </summary>
    ICoverTrafficGenerator? CoverTrafficGenerator { get; }

    /// <summary>
    /// Processes an outbound message through all enabled privacy transforms.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The transformed message bytes.</returns>
    Task<byte[]> ProcessOutboundMessageAsync(byte[] message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an inbound message by reversing applicable privacy transforms.
    /// </summary>
    /// <param name="message">The received message bytes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The original message bytes.</returns>
    Task<byte[]> ProcessInboundMessageAsync(byte[] message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the delay to apply before sending the next outbound message.
    /// </summary>
    /// <returns>The delay as a TimeSpan.</returns>
    TimeSpan GetOutboundDelay();

    /// <summary>
    /// Records that an outbound message was sent.
    /// </summary>
    void RecordOutboundMessage();

    /// <summary>
    /// Gets pending batched messages ready for sending.
    /// </summary>
    /// <returns>Collection of batched messages, or null if none ready.</returns>
    IReadOnlyList<byte[]>? GetPendingBatches();

    /// <summary>
    /// Forces any pending batches to be ready for sending.
    /// </summary>
    void FlushBatches();

    /// <summary>
    /// Gets an async enumerable of cover traffic messages.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An async enumerable of cover traffic messages.</returns>
    IAsyncEnumerable<byte[]> GetCoverTrafficAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records real message activity for cover traffic management.
    /// </summary>
    void RecordActivity();

    /// <summary>
    /// Updates the privacy layer configuration.
    /// </summary>
    /// <param name="options">The new privacy layer options.</param>
    void UpdateConfiguration(PrivacyLayerOptions options);
}


