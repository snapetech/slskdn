// <copyright file="IMessagePadder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for message padding implementations.
/// </summary>
public interface IMessagePadder
{
    /// <summary>
    /// Pads the message to the next appropriate bucket size.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <returns>The padded message bytes.</returns>
    byte[] Pad(byte[] message);

    /// <summary>
    /// Pads the message to a target size.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <param name="targetSize">The target size to pad to.</param>
    /// <returns>The padded message bytes.</returns>
    byte[] Pad(byte[] message, int targetSize);

    /// <summary>
    /// Removes padding from a padded message.
    /// </summary>
    /// <param name="paddedMessage">The padded message bytes.</param>
    /// <returns>The original message bytes.</returns>
    byte[] Unpad(byte[] paddedMessage);

    /// <summary>
    /// Gets the next bucket size for the given message length.
    /// </summary>
    /// <param name="messageLength">The length of the original message.</param>
    /// <returns>The target bucket size.</returns>
    int GetBucketSize(int messageLength);
}