// <copyright file="MeshHashEntrySignerTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Tests.Unit.Mesh.Messages;

using System;
using slskd.Mesh.Messages;
using slskd.Mesh.Transport;
using Xunit;

// HARDENING-2026-04-20 H7: verifies that MeshHashEntry signing round-trips, that mutation of any
// identity field invalidates the signature, and that the HasSignature / TryVerify split lets the
// ingest path distinguish "legacy unsigned" from "broken signature".
public class MeshHashEntrySignerTests
{
    private static MeshHashEntry SampleEntry() => new()
    {
        FlacKey = "deadbeefcafebabe",
        ByteHash = new string('a', 64),
        Size = 123_456_789,
        MetaFlags = 0x1234,
        SeqId = 42,
    };

    [Fact]
    public void Sign_AndTryVerify_RoundTripsSuccessfully()
    {
        using var signer = new Ed25519Signer();
        var (sk, pk) = signer.GenerateKeyPair();
        var entry = SampleEntry();

        MeshHashEntrySigner.Sign(entry, sk, pk, signer);

        Assert.True(MeshHashEntrySigner.HasSignature(entry));
        Assert.True(MeshHashEntrySigner.TryVerify(entry, signer, out var peerId));
        Assert.Equal(Ed25519Signer.DerivePeerId(pk), peerId);
    }

    [Fact]
    public void TryVerify_ReturnsFalseForUnsignedEntry()
    {
        using var signer = new Ed25519Signer();
        var entry = SampleEntry();

        Assert.False(MeshHashEntrySigner.HasSignature(entry));
        Assert.False(MeshHashEntrySigner.TryVerify(entry, signer, out var peerId));
        Assert.Equal(string.Empty, peerId);
    }

    [Theory]
    [InlineData("flac")]
    [InlineData("byte")]
    [InlineData("size")]
    [InlineData("meta")]
    public void TryVerify_FailsIfIdentityFieldMutatedAfterSigning(string field)
    {
        using var signer = new Ed25519Signer();
        var (sk, pk) = signer.GenerateKeyPair();
        var entry = SampleEntry();

        MeshHashEntrySigner.Sign(entry, sk, pk, signer);

        switch (field)
        {
            case "flac": entry.FlacKey = "0000000000000000"; break;
            case "byte": entry.ByteHash = new string('b', 64); break;
            case "size": entry.Size += 1; break;
            case "meta": entry.MetaFlags = (entry.MetaFlags ?? 0) ^ 1; break;
        }

        Assert.False(MeshHashEntrySigner.TryVerify(entry, signer, out _));
    }

    [Fact]
    public void TryVerify_IgnoresMutableBookkeepingFields()
    {
        // SeqId is per-observer and deliberately excluded from the signing payload.
        using var signer = new Ed25519Signer();
        var (sk, pk) = signer.GenerateKeyPair();
        var entry = SampleEntry();

        MeshHashEntrySigner.Sign(entry, sk, pk, signer);
        entry.SeqId = 9999;

        Assert.True(MeshHashEntrySigner.TryVerify(entry, signer, out _));
    }

    [Fact]
    public void TryVerify_RejectsSignatureFromDifferentKey()
    {
        using var signer = new Ed25519Signer();
        var (skA, pkA) = signer.GenerateKeyPair();
        var (_, pkB) = signer.GenerateKeyPair();
        var entry = SampleEntry();

        MeshHashEntrySigner.Sign(entry, skA, pkA, signer);
        // Swap in a different claimed public key; signature was produced by A but claims to be from B.
        entry.SignerPublicKey = Convert.ToBase64String(pkB);

        Assert.False(MeshHashEntrySigner.TryVerify(entry, signer, out _));
    }

    [Fact]
    public void TryVerify_RejectsMalformedBase64()
    {
        using var signer = new Ed25519Signer();
        var entry = SampleEntry();
        entry.SignerPublicKey = "!!!not-base64!!!";
        entry.Signature = "!!!not-base64!!!";

        Assert.False(MeshHashEntrySigner.TryVerify(entry, signer, out _));
    }
}
