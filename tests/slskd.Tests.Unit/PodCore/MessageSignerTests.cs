// <copyright file="MessageSignerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using slskd.Mesh.Transport;
using slskd.PodCore;
using Xunit;

namespace slskd.Tests.Unit.PodCore;

/// <summary>
/// Unit tests for MessageSigner. PR-12: Ed25519, canonical payload, membership pubkey.
/// </summary>
public class MessageSignerTests
{
    [Fact]
    public async Task Sign_then_Verify_roundtrip()
    {
        using var ed = new Ed25519Signer();
        var (priv, pub) = ed.GenerateKeyPair();
        var privB64 = Convert.ToBase64String(priv);
        var pubB64 = Convert.ToBase64String(pub);

        var log = new Mock<ILogger<MessageSigner>>();
        var pod = new Mock<IPodService>();
        pod.Setup(s => s.GetMembersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer1", PublicKey = pubB64 } });

        var opts = new Mock<IOptionsMonitor<PodMessageSignerOptions>>();
        opts.Setup(o => o.CurrentValue).Returns(new PodMessageSignerOptions { SignatureMode = SignatureMode.Off });

        var ms = new MessageSigner(log.Object, pod.Object, ed, opts.Object);

        var msg = new PodMessage
        {
            MessageId = "m1",
            PodId = "pod1",
            ChannelId = "general",
            SenderPeerId = "peer1",
            Body = "hello",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var signed = await ms.SignMessageAsync(msg, privB64);
        Assert.StartsWith("ed25519:", signed.Signature);
        Assert.Equal(1, signed.SigVersion);

        var ok = await ms.VerifyMessageAsync(signed);
        Assert.True(ok);
    }

    [Fact]
    public async Task Verify_wrong_body_fails()
    {
        using var ed = new Ed25519Signer();
        var (priv, pub) = ed.GenerateKeyPair();
        var privB64 = Convert.ToBase64String(priv);
        var pubB64 = Convert.ToBase64String(pub);

        var log = new Mock<ILogger<MessageSigner>>();
        var pod = new Mock<IPodService>();
        pod.Setup(s => s.GetMembersAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PodMember> { new PodMember { PeerId = "peer1", PublicKey = pubB64 } });

        var opts = new Mock<IOptionsMonitor<PodMessageSignerOptions>>();
        opts.Setup(o => o.CurrentValue).Returns(new PodMessageSignerOptions { SignatureMode = SignatureMode.Off });

        var ms = new MessageSigner(log.Object, pod.Object, ed, opts.Object);

        var msg = new PodMessage
        {
            MessageId = "m2",
            PodId = "pod1",
            ChannelId = "general",
            SenderPeerId = "peer1",
            Body = "original",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var signed = await ms.SignMessageAsync(msg, privB64);
        signed.Body = "tampered";

        var ok = await ms.VerifyMessageAsync(signed);
        Assert.False(ok);
    }

    [Fact]
    public async Task Verify_Enforce_rejects_missing_signature()
    {
        using var ed = new Ed25519Signer();
        var log = new Mock<ILogger<MessageSigner>>();
        var pod = new Mock<IPodService>();

        var opts = new Mock<IOptionsMonitor<PodMessageSignerOptions>>();
        opts.Setup(o => o.CurrentValue).Returns(new PodMessageSignerOptions { SignatureMode = SignatureMode.Enforce });

        var ms = new MessageSigner(log.Object, pod.Object, ed, opts.Object);
        var msg = new PodMessage { MessageId = "m", PodId = "p", ChannelId = "c", SenderPeerId = "s", Signature = "" };

        var ok = await ms.VerifyMessageAsync(msg);
        Assert.False(ok);
    }
}
