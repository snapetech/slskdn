// <copyright file="IMessagePadder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Privacy;

/// <summary>
/// Interface for message padding implementations that prevent traffic analysis by obscuring message sizes.
/// </summary>
public interface IMessagePadder
{
    /// <summary>
    /// Pads the given message to a fixed bucket size.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <returns>The padded message bytes.</returns>
    byte[] Pad(byte[] message);

    /// <summary>
    /// Removes padding from a padded message.
    /// </summary>
    /// <param name="paddedMessage">The padded message bytes.</param>
    /// <returns>The original message bytes.</returns>
    byte[] Unpad(byte[] paddedMessage);

    /// <summary>
    /// Gets the current bucket size used for padding.
    /// </summary>
    int BucketSize { get; }

    /// <summary>
    /// Sets the bucket size for padding.
    /// </summary>
    /// <param name="size">The bucket size in bytes.</param>
    void SetBucketSize(int size);
}

/// <summary>
/// Interface for timing obfuscation implementations that prevent traffic analysis by obscuring message timing.
/// </summary>
public interface ITimingObfuscator
{
    /// <summary>
    /// Gets the delay to apply before sending the next message.
    /// </summary>
    /// <returns>The delay as a TimeSpan.</returns>
    TimeSpan GetDelay();

    /// <summary>
    /// Records that a message was sent (for adaptive timing if needed).
    /// </summary>
    void RecordSend();
}

/// <summary>
/// Interface for message batching implementations that prevent traffic analysis by grouping messages.
/// </summary>
public interface IMessageBatcher
{
    /// <summary>
    /// Adds a message to the current batch.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <returns>True if the batch is ready to send, false if more messages should be collected.</returns>
    bool AddMessage(byte[] message);

    /// <summary>
    /// Gets the current batch of messages.
    /// </summary>
    /// <returns>The batched messages, or null if no batch is ready.</returns>
    IReadOnlyList<byte[]>? GetBatch();

    /// <summary>
    /// Forces the current batch to be ready for sending.
    /// </summary>
    void Flush();

    /// <summary>
    /// Gets whether a batch is currently ready to send.
    /// </summary>
    bool HasBatch { get; }
}

/// <summary>
/// Interface for cover traffic generation to maintain constant traffic patterns.
/// </summary>
public interface ICoverTrafficGenerator
{
    /// <summary>
    /// Generates cover traffic messages at appropriate intervals.
    /// </summary>
    /// <returns>An enumerable of cover traffic messages to send.</returns>
    IAsyncEnumerable<byte[]> GenerateCoverTrafficAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Records real message activity to adjust cover traffic generation.
    /// </summary>
    void RecordActivity();
}

