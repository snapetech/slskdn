// <copyright file="BucketPadder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Privacy;

/// <summary>
/// Implements message padding to fixed bucket sizes to prevent traffic analysis attacks.
/// Pads messages to predefined bucket sizes using random fill bytes to prevent compression analysis.
/// </summary>
public class BucketPadder : IMessagePadder
{
    private readonly ILogger<BucketPadder> _logger;
    private readonly RandomNumberGenerator _rng;
    private int _bucketSize;

    // Standard bucket sizes that provide good privacy vs overhead balance
    public static readonly int[] StandardBucketSizes = { 512, 1024, 2048, 4096, 8192, 16384 };

    /// <summary>
    /// Initializes a new instance of the <see cref="BucketPadder"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="bucketSize">The initial bucket size. Defaults to 2048 bytes.</param>
    public BucketPadder(ILogger<BucketPadder> logger, int bucketSize = 2048)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rng = RandomNumberGenerator.Create();

        if (!StandardBucketSizes.Contains(bucketSize))
        {
            _logger.LogWarning("Non-standard bucket size {BucketSize} specified. Consider using standard sizes: {StandardSizes}",
                bucketSize, string.Join(", ", StandardBucketSizes));
        }

        _bucketSize = bucketSize;

        _logger.LogDebug("BucketPadder initialized with bucket size {BucketSize} bytes", bucketSize);
    }

    /// <summary>
    /// Gets the current bucket size used for padding.
    /// </summary>
    public int BucketSize => _bucketSize;

    /// <summary>
    /// Sets the bucket size for padding.
    /// </summary>
    /// <param name="size">The bucket size in bytes. Must be one of the standard sizes.</param>
    /// <exception cref="ArgumentException">Thrown when an invalid bucket size is specified.</exception>
    public void SetBucketSize(int size)
    {
        if (!StandardBucketSizes.Contains(size))
        {
            throw new ArgumentException($"Bucket size must be one of: {string.Join(", ", StandardBucketSizes)}", nameof(size));
        }

        _bucketSize = size;
        _logger.LogInformation("Bucket size changed to {BucketSize} bytes", size);
    }

    /// <summary>
    /// Pads the given message to the current bucket size.
    /// </summary>
    /// <param name="message">The original message bytes.</param>
    /// <returns>The padded message bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when message is null.</exception>
    /// <exception cref="ArgumentException">Thrown when message is larger than bucket size.</exception>
    public byte[] Pad(byte[] message)
    {
        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        if (message.Length > _bucketSize)
        {
            throw new ArgumentException($"Message size ({message.Length}) exceeds bucket size ({_bucketSize})", nameof(message));
        }

        var paddedLength = _bucketSize;
        var paddedMessage = new byte[paddedLength];

        // Copy original message
        Array.Copy(message, paddedMessage, message.Length);

        // Add padding header (2 bytes for original length)
        var originalLengthBytes = BitConverter.GetBytes((ushort)message.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(originalLengthBytes);
        }
        Array.Copy(originalLengthBytes, 0, paddedMessage, paddedLength - 2, 2);

        // Fill padding area with random bytes (not zeros to prevent compression analysis)
        if (message.Length < paddedLength - 2)
        {
            var paddingBytes = new byte[paddedLength - 2 - message.Length];
            _rng.GetBytes(paddingBytes);
            Array.Copy(paddingBytes, 0, paddedMessage, message.Length, paddingBytes.Length);
        }

        _logger.LogTrace("Padded message from {OriginalSize} to {PaddedSize} bytes", message.Length, paddedMessage.Length);
        return paddedMessage;
    }

    /// <summary>
    /// Removes padding from a padded message.
    /// </summary>
    /// <param name="paddedMessage">The padded message bytes.</param>
    /// <returns>The original message bytes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when paddedMessage is null.</exception>
    /// <exception cref="ArgumentException">Thrown when paddedMessage has invalid format or size.</exception>
    public byte[] Unpad(byte[] paddedMessage)
    {
        if (paddedMessage == null)
        {
            throw new ArgumentNullException(nameof(paddedMessage));
        }

        if (paddedMessage.Length != _bucketSize)
        {
            throw new ArgumentException($"Padded message size ({paddedMessage.Length}) does not match bucket size ({_bucketSize})", nameof(paddedMessage));
        }

        if (paddedMessage.Length < 2)
        {
            throw new ArgumentException("Padded message too small to contain length header", nameof(paddedMessage));
        }

        // Extract original length from last 2 bytes
        var lengthBytes = new byte[2];
        Array.Copy(paddedMessage, paddedMessage.Length - 2, lengthBytes, 0, 2);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }
        var originalLength = BitConverter.ToUInt16(lengthBytes);

        if (originalLength > paddedMessage.Length - 2)
        {
            throw new ArgumentException($"Invalid original length ({originalLength}) in padded message", nameof(paddedMessage));
        }

        // Extract original message
        var originalMessage = new byte[originalLength];
        Array.Copy(paddedMessage, 0, originalMessage, 0, originalLength);

        _logger.LogTrace("Unpadded message from {PaddedSize} to {OriginalSize} bytes", paddedMessage.Length, originalLength);
        return originalMessage;
    }

    /// <summary>
    /// Calculates the optimal bucket size for a given message size.
    /// </summary>
    /// <param name="messageSize">The size of the message in bytes.</param>
    /// <returns>The optimal bucket size.</returns>
    public static int GetOptimalBucketSize(int messageSize)
    {
        // Find the smallest bucket that can fit the message
        foreach (var size in StandardBucketSizes.OrderBy(s => s))
        {
            if (size >= messageSize + 2) // +2 for length header
            {
                return size;
            }
        }

        // If message is too large, use the largest bucket
        return StandardBucketSizes.Max();
    }

    /// <summary>
    /// Gets the padding overhead for a given message size.
    /// </summary>
    /// <param name="messageSize">The size of the message in bytes.</param>
    /// <param name="bucketSize">The bucket size to use.</param>
    /// <returns>The padding overhead in bytes.</returns>
    public static int GetPaddingOverhead(int messageSize, int bucketSize)
    {
        if (messageSize > bucketSize)
        {
            throw new ArgumentException("Message size exceeds bucket size", nameof(messageSize));
        }

        return bucketSize - messageSize;
    }
}


