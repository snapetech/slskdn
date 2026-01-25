// <copyright file="MessagePadderTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Common.Security;
using slskd.Privacy;
using Xunit;

namespace slskd.Tests.Unit.Privacy;

/// <summary>
/// Unit tests for slskd.Privacy.MessagePadder. PR-11: versioned format, Unpad, size limits.
/// </summary>
public class MessagePadderTests
{
    private static MessagePadder CreatePadder(AdversarialOptions? adversarial = null)
    {
        var log = new Mock<ILogger<MessagePadder>>();
        var opts = new Mock<IOptionsMonitor<slskd.Options>>();
        opts.Setup(m => m.CurrentValue).Returns(new slskd.Options());
        IOptionsMonitor<AdversarialOptions>? adv = null;
        if (adversarial != null)
        {
            var m = new Mock<IOptionsMonitor<AdversarialOptions>>();
            m.Setup(x => x.CurrentValue).Returns(adversarial);
            adv = m.Object;
        }
        return new MessagePadder(log.Object, opts.Object, adv);
    }

    [Fact]
    public void Pad_targetSize_then_Unpad_roundtrip()
    {
        var p = CreatePadder();
        var msg = new byte[] { 1, 2, 3, 4, 5 };

        var padded = p.Pad(msg, 64);
        var unpadded = p.Unpad(padded);

        Assert.Equal(msg, unpadded);
    }

    [Fact]
    public void Unpad_corrupt_header_version_throws()
    {
        var p = CreatePadder();
        var msg = new byte[] { 10, 20 };
        var padded = p.Pad(msg, 32);
        padded[0] = 0x99; // wrong version

        var ex = Assert.Throws<ArgumentException>(() => p.Unpad(padded));
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void Unpad_too_short_throws()
    {
        var p = CreatePadder();
        var shortBuf = new byte[] { 0x01, 0, 0 }; // 3 bytes < HeaderLength (5)

        var ex = Assert.Throws<ArgumentException>(() => p.Unpad(shortBuf));
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Unpad_corrupt_originalLength_exceeds_padded_throws()
    {
        var p = CreatePadder();
        // [0x01][4B BE len=100][payload 10 bytes][random] -> total 5+10=15, but we claim orig=100
        var buf = new byte[32];
        buf[0] = 0x01;
        buf[1] = 0; buf[2] = 0; buf[3] = 0; buf[4] = 100; // originalLength=100
        new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }.AsSpan().CopyTo(buf.AsSpan(5));

        var ex = Assert.Throws<ArgumentException>(() => p.Unpad(buf));
        Assert.Contains("originalLength", ex.Message);
    }

    [Fact]
    public void Unpad_oversized_padded_throws_when_MaxPaddedBytes_low()
    {
        var adv = new AdversarialOptions { Privacy = new PrivacyLayerOptions { Padding = new MessagePaddingOptions { MaxPaddedBytes = 50 } } };
        var p = CreatePadder(adv);
        var msg = new byte[] { 1, 2, 3 };
        var padded = p.Pad(msg, 64); // 64 > 50

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => p.Unpad(padded));
        Assert.Contains("MaxPaddedBytes", ex.Message);
    }

    [Fact]
    public void Unpad_oversized_originalLength_throws_when_MaxUnpaddedBytes_low()
    {
        var adv = new AdversarialOptions { Privacy = new PrivacyLayerOptions { Padding = new MessagePaddingOptions { MaxUnpaddedBytes = 5 } } };
        var p = CreatePadder(adv);
        // Build v1 payload manually: [0x01][4B BE originalLength=6][payload 6 bytes][padding]. Pad() would throw because 6 > MaxUnpaddedBytes 5.
        var padded = new byte[32];
        padded[0] = 0x01;
        padded[1] = 0; padded[2] = 0; padded[3] = 0; padded[4] = 6;
        new byte[] { 1, 2, 3, 4, 5, 6 }.CopyTo(padded, 5);
        // rest is random; Unpad only reads header + originalLength bytes

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => p.Unpad(padded));
        Assert.Contains("MaxUnpaddedBytes", ex.Message);
    }

    [Fact]
    public void Pad_targetSize_with_message_over_MaxUnpaddedBytes_throws()
    {
        var adv = new AdversarialOptions { Privacy = new PrivacyLayerOptions { Padding = new MessagePaddingOptions { MaxUnpaddedBytes = 10 } } };
        var p = CreatePadder(adv);
        var msg = new byte[20];

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => p.Pad(msg, 64));
        Assert.Contains("MaxUnpaddedBytes", ex.Message);
    }

    [Fact]
    public void Unpad_null_throws()
    {
        var p = CreatePadder();
        Assert.Throws<ArgumentNullException>(() => p.Unpad(null!));
    }

    [Fact]
    public void Pad_targetSize_returns_unchanged_when_targetSize_le_message_length()
    {
        var p = CreatePadder();
        var msg = new byte[] { 1, 2, 3 };

        var result = p.Pad(msg, 3);
        Assert.Equal(msg, result);

        result = p.Pad(msg, 2);
        Assert.Equal(msg, result);
    }

    [Fact]
    public void Pad_bucket_then_Unpad_roundtrip_when_padding_enabled()
    {
        var adv = new AdversarialOptions
        {
            Privacy = new PrivacyLayerOptions
            {
                Padding = new MessagePaddingOptions { Enabled = true, BucketSizes = new List<int> { 64, 128 } }
            }
        };
        var p = CreatePadder(adv);
        var msg = new byte[] { 10, 20, 30 };

        var padded = p.Pad(msg);
        var unpadded = p.Unpad(padded);

        Assert.Equal(msg, unpadded);
    }
}
