// <copyright file="IMessageBatcher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for message batching implementations.
/// </summary>
public interface IMessageBatcher
{
    /// <summary>
    /// Adds a message to the current batch.
    /// </summary>
    /// <param name="message">The message to add.</param>
    /// <param name="metadata">Optional metadata about the message.</param>
    /// <returns>A task that completes when the message is queued.</returns>
    Task AddMessageAsync(byte[] message, IReadOnlyDictionary<string, object>? metadata = null);

    /// <summary>
    /// Gets the next batch of messages, waiting if necessary for the batch window.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The batch of messages.</returns>
    Task<IReadOnlyList<BatchedMessage>> GetNextBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces immediate sending of the current batch.
    /// </summary>
    /// <returns>The current batch of messages.</returns>
    Task<IReadOnlyList<BatchedMessage>> FlushAsync();
}

/// <summary>
/// Represents a message in a batch.
/// </summary>
public class BatchedMessage
{
    /// <summary>
    /// Gets the message data.
    /// </summary>
    public byte[] Data { get; }

    /// <summary>
    /// Gets the metadata associated with the message.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets the timestamp when the message was added to the batch.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchedMessage"/> class.
    /// </summary>
    /// <param name="data">The message data.</param>
    /// <param name="metadata">The metadata.</param>
    /// <param name="timestamp">The timestamp.</param>
    public BatchedMessage(byte[] data, IReadOnlyDictionary<string, object>? metadata, DateTimeOffset timestamp)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Metadata = metadata ?? new Dictionary<string, object>();
        Timestamp = timestamp;
    }
}

