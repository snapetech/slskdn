// <copyright file="MessagePadder.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
// </copyright>

namespace slskd.Privacy;

using System;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     Pads outbound messages to the next configured bucket size using random bytes.
/// </summary>
public class MessagePadder : IMessagePadder
{
    private static readonly int[] DefaultBuckets = new[] { 512, 1024, 2048, 4096, 8192, 16384 };

    private readonly ILogger<MessagePadder> logger;
    private readonly IOptionsMonitor<Options> optionsMonitor;

    public MessagePadder(ILogger<MessagePadder> logger, IOptionsMonitor<Options> optionsMonitor)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
    }

    public byte[] Pad(ReadOnlyMemory<byte> payload)
    {
        var options = optionsMonitor.CurrentValue.Adversarial.Privacy;

        if (!options.EnablePadding || payload.Length == 0)
        {
            return payload.ToArray();
        }

        var bucketSize = ResolveBucketSize(options.PaddingBucketBytes, payload.Length);
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
