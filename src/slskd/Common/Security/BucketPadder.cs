// <copyright file="BucketPadder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Bucket-based message padder that pads messages to fixed size buckets.
/// </summary>
public class BucketPadder : IMessagePadder
{
    private readonly MessagePaddingOptions _options;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="BucketPadder"/> class.
    /// </summary>
    /// <param name="options">The padding options.</param>
    public BucketPadder(MessagePaddingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _random = new Random();
    }

    /// <summary>
    /// Pads the message to the next appropriate bucket size.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <returns>The padded message bytes.</returns>
    public byte[] Pad(byte[] message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        int targetSize = GetBucketSize(message.Length);
        return Pad(message, targetSize);
    }

    /// <summary>
    /// Pads the message to a specific target size.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <param name="targetSize">The target size to pad to.</param>
    /// <returns>The padded message bytes.</returns>
    public byte[] Pad(byte[] message, int targetSize)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        if (targetSize < message.Length)
            throw new ArgumentException("Target size must be at least as large as the message", nameof(targetSize));

        // Check if padding would exceed overhead limit
        double overheadPercent = ((double)(targetSize - message.Length) / message.Length) * 100.0;
        if (overheadPercent > _options.MaxOverheadPercent)
        {
            // If we would exceed the limit, don't pad (return original message)
            // This prevents excessive padding for very small messages
            return message;
        }

        // Calculate how much padding we need
        int paddingLength = targetSize - message.Length;

        // Create the padded message
        byte[] paddedMessage = new byte[targetSize];

        // Copy original message
        Array.Copy(message, paddedMessage, message.Length);

        // Add padding (either random bytes or zeros)
        if (_options.UseRandomFill)
        {
            // Use random fill to prevent compression analysis
            for (int i = message.Length; i < targetSize; i++)
            {
                paddedMessage[i] = (byte)_random.Next(0, 256);
            }
        }
        else
        {
            // Use zero fill (less secure but more predictable)
            // The message already has zeros from the Array.Copy, so we're done
        }

        // Store the original length in the last 4 bytes (big-endian)
        // This allows us to unpad correctly
        int originalLength = message.Length;
        paddedMessage[targetSize - 4] = (byte)(originalLength >> 24);
        paddedMessage[targetSize - 3] = (byte)(originalLength >> 16);
        paddedMessage[targetSize - 2] = (byte)(originalLength >> 8);
        paddedMessage[targetSize - 1] = (byte)originalLength;

        return paddedMessage;
    }

    /// <summary>
    /// Removes padding from a padded message.
    /// </summary>
    /// <param name="paddedMessage">The padded message bytes.</param>
    /// <returns>The original message bytes.</returns>
    public byte[] Unpad(byte[] paddedMessage)
    {
        if (paddedMessage == null)
            throw new ArgumentNullException(nameof(paddedMessage));

        if (paddedMessage.Length < 4)
            throw new ArgumentException("Padded message is too short to contain length header", nameof(paddedMessage));

        // Read the original length from the last 4 bytes
        int originalLength = (paddedMessage[paddedMessage.Length - 4] << 24) |
                            (paddedMessage[paddedMessage.Length - 3] << 16) |
                            (paddedMessage[paddedMessage.Length - 2] << 8) |
                             paddedMessage[paddedMessage.Length - 1];

        if (originalLength < 0 || originalLength > paddedMessage.Length - 4)
            throw new ArgumentException("Invalid original length in padded message", nameof(paddedMessage));

        // Extract the original message
        byte[] originalMessage = new byte[originalLength];
        Array.Copy(paddedMessage, originalMessage, originalLength);

        return originalMessage;
    }

    /// <summary>
    /// Gets the next bucket size for the given message length.
    /// </summary>
    /// <param name="messageLength">The length of the original message.</param>
    /// <returns>The target bucket size.</returns>
    public int GetBucketSize(int messageLength)
    {
        if (_options.BucketSizes == null || _options.BucketSizes.Count == 0)
        {
            // Default buckets if none specified
            int[] defaultBuckets = { 512, 1024, 2048, 4096, 8192, 16384 };
            foreach (int bucket in defaultBuckets)
            {
                if (messageLength <= bucket)
                    return bucket;
            }
            return defaultBuckets[^1]; // Return last bucket if message is larger than all
        }

        // Find the smallest bucket that can contain the message
        foreach (int bucket in _options.BucketSizes.OrderBy(b => b))
        {
            if (messageLength <= bucket)
                return bucket;
        }

        // If message is larger than all buckets, use the largest bucket
        return _options.BucketSizes.Max();
    }
}
