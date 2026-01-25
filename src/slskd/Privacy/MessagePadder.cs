// <copyright file="MessagePadder.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

namespace slskd.Privacy;

using System;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;

/// <summary>
///     Pads outbound messages to the next configured bucket size using random bytes.
///     PR-11: versioned format v1 [1B version][4B originalLength BE][payload][random]; Unpad with size limits.
/// </summary>
public class MessagePadder : IMessagePadder
{
    private const byte FormatVersion = 0x01;
    private const int HeaderLength = 5; // 1 version + 4 originalLength
    private const int DefaultMaxUnpaddedBytes = 1024 * 1024;   // 1MB, align with typical MaxRemotePayloadSize
    private const int DefaultMaxPaddedBytes = 2 * 1024 * 1024; // 2MB

    private static readonly int[] DefaultBuckets = new[] { 512, 1024, 2048, 4096, 8192, 16384 };

    private readonly ILogger<MessagePadder> logger;
    private readonly IOptionsMonitor<slskd.Options> optionsMonitor;
    private readonly IOptionsMonitor<slskd.Common.Security.AdversarialOptions>? adversarialOptions;

    public MessagePadder(
        ILogger<MessagePadder> logger,
        IOptionsMonitor<slskd.Options> optionsMonitor,
        IOptionsMonitor<slskd.Common.Security.AdversarialOptions>? adversarialOptions = null)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
        this.adversarialOptions = adversarialOptions;
    }

    public byte[] Pad(byte[] message)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        
        return Pad(new ReadOnlyMemory<byte>(message));
    }

    public byte[] Pad(byte[] message, int targetSize)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        if (targetSize <= message.Length)
            return message;

        var maxUnpadded = GetMaxUnpaddedBytes();
        if (message.Length > maxUnpadded)
            throw new ArgumentOutOfRangeException(nameof(message), $"Message length {message.Length} exceeds MaxUnpaddedBytes {maxUnpadded}.");

        int actualSize = Math.Max(targetSize, HeaderLength + message.Length);
        var padded = new byte[actualSize];
        var span = padded.AsSpan();
        span[0] = FormatVersion;
        WriteBigEndianUInt32(span, 1, (uint)message.Length);
        message.AsSpan().CopyTo(span.Slice(HeaderLength));
        RandomNumberGenerator.Fill(span.Slice(HeaderLength + message.Length));
        return padded;
    }

    public byte[] Unpad(byte[] paddedMessage)
    {
        if (paddedMessage == null)
            throw new ArgumentNullException(nameof(paddedMessage));

        int maxPadded = GetMaxPaddedBytes();
        if (paddedMessage.Length > maxPadded)
            throw new ArgumentOutOfRangeException(nameof(paddedMessage), $"Padded length {paddedMessage.Length} exceeds MaxPaddedBytes {maxPadded}.");

        if (paddedMessage.Length < HeaderLength)
            throw new ArgumentException($"Padded message is too short (min {HeaderLength} bytes for v1 header).", nameof(paddedMessage));

        if (paddedMessage[0] != FormatVersion)
            throw new ArgumentException($"Unsupported or corrupt padding version: 0x{paddedMessage[0]:X2}.", nameof(paddedMessage));

        uint originalLength = ReadBigEndianUInt32(paddedMessage, 1);
        int orig = (int)originalLength;

        int maxUnpadded = GetMaxUnpaddedBytes();
        if (orig > maxUnpadded)
            throw new ArgumentOutOfRangeException(nameof(paddedMessage), $"originalLength {orig} exceeds MaxUnpaddedBytes {maxUnpadded}.");

        if (orig > paddedMessage.Length - HeaderLength)
            throw new ArgumentException($"Invalid originalLength {orig} for padded length {paddedMessage.Length}.", nameof(paddedMessage));

        var result = new byte[orig];
        Buffer.BlockCopy(paddedMessage, HeaderLength, result, 0, orig);
        return result;
    }

    private static void WriteBigEndianUInt32(Span<byte> buf, int offset, uint value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static uint ReadBigEndianUInt32(byte[] buf, int offset)
    {
        return ((uint)buf[offset] << 24) | ((uint)buf[offset + 1] << 16) | ((uint)buf[offset + 2] << 8) | buf[offset + 3];
    }

    private int GetMaxUnpaddedBytes()
    {
        var v = adversarialOptions?.CurrentValue?.Privacy?.Padding?.MaxUnpaddedBytes ?? 0;
        return v > 0 ? v : DefaultMaxUnpaddedBytes;
    }

    private int GetMaxPaddedBytes()
    {
        var v = adversarialOptions?.CurrentValue?.Privacy?.Padding?.MaxPaddedBytes ?? 0;
        return v > 0 ? v : DefaultMaxPaddedBytes;
    }

    public int GetBucketSize(int messageLength)
    {
        var adversarialOpts = adversarialOptions?.CurrentValue;
        var privacyOptions = adversarialOpts?.Privacy;
        var paddingOptions = privacyOptions?.Padding;
        
        if (paddingOptions == null || !paddingOptions.Enabled)
            return messageLength;
        
        var configuredBucket = paddingOptions.BucketSizes?.FirstOrDefault(b => b >= messageLength) ?? 0;
        return ResolveBucketSize(configuredBucket, messageLength);
    }

    private byte[] Pad(ReadOnlyMemory<byte> payload)
    {
        var adversarialOpts = adversarialOptions?.CurrentValue;
        var privacyOptions = adversarialOpts?.Privacy;
        var paddingOptions = privacyOptions?.Padding;

        if (paddingOptions == null || !paddingOptions.Enabled || payload.Length == 0)
        {
            return payload.ToArray();
        }

        int maxUnpadded = GetMaxUnpaddedBytes();
        if (payload.Length > maxUnpadded)
        {
            logger.LogWarning("[MessagePadder] Payload length {Length} exceeds MaxUnpaddedBytes {Max}; returning unmodified.", payload.Length, maxUnpadded);
            return payload.ToArray();
        }

        int minBucket = HeaderLength + payload.Length;
        var configuredBucket = paddingOptions.BucketSizes?.FirstOrDefault(b => b >= minBucket) ?? 0;
        var bucketSize = ResolveBucketSize(configuredBucket, payload.Length);
        if (bucketSize < minBucket)
        {
            bucketSize = minBucket;
        }

        if (bucketSize <= payload.Length)
        {
            return payload.ToArray();
        }

        var padded = new byte[bucketSize];
        var span = padded.AsSpan();
        span[0] = FormatVersion;
        WriteBigEndianUInt32(span, 1, (uint)payload.Length);
        payload.Span.CopyTo(span.Slice(HeaderLength));
        RandomNumberGenerator.Fill(span.Slice(HeaderLength + payload.Length));

        return padded;
    }

    private int ResolveBucketSize(int configuredBucket, int payloadLength)
    {
        // Use configured bucket when valid; otherwise fall back to the next default bucket.
        if (configuredBucket > 0 && configuredBucket >= payloadLength)
        {
            return configuredBucket;
        }

        var bucket = DefaultBuckets.FirstOrDefault(b => b >= payloadLength);
        if (bucket == 0 && configuredBucket > 0)
        {
            logger.LogWarning(
                "Configured padding bucket {Bucket} is too small for payload length {Length}; using payload length (no padding applied).",
                configuredBucket,
                payloadLength);
            return payloadLength;
        }

        return bucket;
    }
}
