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
/// </summary>
public class MessagePadder : IMessagePadder
{
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
        
        var padded = new byte[targetSize];
        var span = padded.AsSpan();
        message.AsSpan().CopyTo(span);
        
        var paddingSpan = span.Slice(message.Length);
        RandomNumberGenerator.Fill(paddingSpan);
        
        return padded;
    }

    public byte[] Unpad(byte[] paddedMessage)
    {
        // This implementation doesn't track original size, so we can't unpad
        // In a real implementation, you'd need to store the original size
        throw new NotImplementedException("Unpad is not implemented for this padder");
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

        // Use first bucket size from BucketSizes list, or default
        var configuredBucket = paddingOptions.BucketSizes?.FirstOrDefault(b => b >= payload.Length) ?? 0;
        var bucketSize = ResolveBucketSize(configuredBucket, payload.Length);
        if (bucketSize <= payload.Length)
        {
            return payload.ToArray();
        }

        var padded = new byte[bucketSize];
        var span = padded.AsSpan();
        payload.Span.CopyTo(span);

        var paddingSpan = span.Slice(payload.Length);
        RandomNumberGenerator.Fill(paddingSpan);

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
