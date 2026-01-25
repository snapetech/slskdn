// <copyright file="ControlSignerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Moq;
using slskd.Mesh.Overlay;
using slskd.Mesh.Transport;
using Xunit;

namespace slskd.Tests.Unit.Mesh.Overlay;

/// <summary>
/// Unit tests for ControlSigner (KeyedSigner): canonical sign/verify and legacy verify path. PR-10.
/// </summary>
public class ControlSignerTests
{
    private readonly Mock<ILogger<ControlSigner>> _logger;
    private readonly Mock<IKeyStore> _keyStore;

    public ControlSignerTests()
    {
        _logger = new Mock<ILogger<ControlSigner>>();
        var keyPair = Ed25519KeyPair.Generate();
        _keyStore = new Mock<IKeyStore>();
        _keyStore.Setup(k => k.Current).Returns(keyPair);
    }

    [Fact]
    public void Sign_then_Verify_canonical_roundtrip_returns_true()
    {
        var signer = new ControlSigner(_logger.Object, _keyStore.Object);
        var envelope = new ControlEnvelope
        {
            Type = "test-type",
            MessageId = "msg-canonical-1",
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Payload = new byte[] { 1, 2, 3, 4, 5 }
        };

        var signed = signer.Sign(envelope);

        Assert.True(signer.Verify(signed));
    }

    [Fact]
    public void Verify_envelope_signed_with_legacy_format_returns_true()
    {
        // Envelope signed with legacy format (Type|Timestamp|Base64(Payload)) must still verify.
        var keyPair = _keyStore.Object.Current;
        var envelope = new ControlEnvelope
        {
            Type = "legacy-type",
            MessageId = "msg-legacy-1",
            TimestampUnixMs = 1700000000000,
            Payload = new byte[] { 10, 20, 30 }
        };

        var legacyData = envelope.GetLegacySignableData();
        byte[] signature;
        using (var ed = new Ed25519Signer())
        {
            signature = ed.Sign(legacyData, keyPair.PrivateKey);
        }

        envelope.PublicKey = Convert.ToBase64String(keyPair.PublicKey);
        envelope.Signature = Convert.ToBase64String(signature);

        var signer = new ControlSigner(_logger.Object, _keyStore.Object);

        Assert.True(signer.Verify(envelope));
    }

    [Fact]
    public void Verify_no_public_key_returns_false()
    {
        var signer = new ControlSigner(_logger.Object, _keyStore.Object);
        var envelope = signer.Sign(new ControlEnvelope
        {
            Type = "x",
            Payload = Array.Empty<byte>(),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        envelope.PublicKey = "";

        Assert.False(signer.Verify(envelope));
    }

    [Fact]
    public void Verify_no_signature_returns_false()
    {
        var signer = new ControlSigner(_logger.Object, _keyStore.Object);
        var envelope = signer.Sign(new ControlEnvelope
        {
            Type = "x",
            Payload = Array.Empty<byte>(),
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        envelope.Signature = "";

        Assert.False(signer.Verify(envelope));
    }
}
