// <copyright file="RelayTlsPinValidatorTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Relay;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using slskd.Mesh.Transport;
using slskd.Relay;
using Xunit;

// HARDENING-2026-04-20 H8-pin: the Relay controller connection gains an SPKI-pin override
// that takes precedence over both CA validation and IgnoreCertificateErrors. These tests
// exercise the parser (whitespace, duplicates, empty segments) and the matcher (match /
// mismatch / empty-pin / null-cert edge cases).
public class RelayTlsPinValidatorTests
{
    [Fact]
    public void ParsePins_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(RelayTlsPinValidator.ParsePins(null));
        Assert.Empty(RelayTlsPinValidator.ParsePins(string.Empty));
        Assert.Empty(RelayTlsPinValidator.ParsePins("   "));
    }

    [Fact]
    public void ParsePins_Single_Trimmed()
    {
        var pins = RelayTlsPinValidator.ParsePins("  abc123  ");
        Assert.Single(pins);
        Assert.Equal("abc123", pins[0]);
    }

    [Fact]
    public void ParsePins_Multiple_TrimmedDistinct()
    {
        var pins = RelayTlsPinValidator.ParsePins(" a, b , a ,,c");
        Assert.Equal(new[] { "a", "b", "c" }, pins);
    }

    [Fact]
    public void IsPinned_NullCertificate_ReturnsFalse()
    {
        Assert.False(RelayTlsPinValidator.IsPinned(null, new[] { "x" }));
    }

    [Fact]
    public void IsPinned_EmptyPinList_ReturnsFalse()
    {
        using var cert = BuildSelfSignedCert();
        Assert.False(RelayTlsPinValidator.IsPinned(cert, Array.Empty<string>()));
    }

    [Fact]
    public void IsPinned_MatchingPin_ReturnsTrue()
    {
        using var cert = BuildSelfSignedCert();
        var pin = SecurityUtils.ExtractSpkiPin(cert);

        Assert.False(string.IsNullOrEmpty(pin));
        Assert.True(RelayTlsPinValidator.IsPinned(cert, new[] { pin! }));
    }

    [Fact]
    public void IsPinned_MismatchingPin_ReturnsFalse()
    {
        using var cert = BuildSelfSignedCert();
        Assert.False(RelayTlsPinValidator.IsPinned(cert, new[] { "this-is-not-the-right-pin" }));
    }

    [Fact]
    public void IsPinned_PinMatchesWhenAmongOthers_ReturnsTrue()
    {
        using var cert = BuildSelfSignedCert();
        var pin = SecurityUtils.ExtractSpkiPin(cert)!;

        Assert.True(RelayTlsPinValidator.IsPinned(cert, new[] { "wrong-1", pin, "wrong-2" }));
    }

    [Fact]
    public void IsPinned_IsCaseSensitive()
    {
        using var cert = BuildSelfSignedCert();
        var pin = SecurityUtils.ExtractSpkiPin(cert)!;

        Assert.False(RelayTlsPinValidator.IsPinned(cert, new[] { pin.ToLowerInvariant() == pin ? pin.ToUpperInvariant() : pin.ToLowerInvariant() }));
    }

    private static X509Certificate2 BuildSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=slskd-relay-pin-test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1));
    }
}
