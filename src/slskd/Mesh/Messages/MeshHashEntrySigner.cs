// <copyright file="MeshHashEntrySigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Mesh.Messages;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using slskd.Mesh.Transport;

/// <summary>
///     Canonicalization, signing, and verification helpers for <see cref="MeshHashEntry"/>.
/// </summary>
/// <remarks>
///     HARDENING-2026-04-20 H7: sign the immutable identity fields of an entry (FlacKey, ByteHash,
///     Size, MetaFlags) so that a recipient can pin the (key, hash, size) tuple to a specific peer
///     id. Mutable bookkeeping fields (SeqId, UseCount, timestamps) are intentionally excluded —
///     they're per-observer and would fail to verify across peers even for the same underlying file.
///
///     The signing format is length-prefixed and domain-separated with a version tag so we can
///     evolve the schema without accepting v1 signatures as v2.
/// </remarks>
public static class MeshHashEntrySigner
{
    private const string DomainTag = "slskdn/hashdb-entry/v1";
    private const int SentinelMissingMetaFlags = int.MinValue;

    /// <summary>
    ///     Produces the canonical byte sequence that is signed (and verified) for a given entry.
    /// </summary>
    public static byte[] GetSigningBytes(MeshHashEntry entry)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        using var ms = new MemoryStream();
        WriteLengthPrefixedUtf8(ms, DomainTag);
        WriteLengthPrefixedUtf8(ms, entry.FlacKey ?? string.Empty);
        WriteLengthPrefixedUtf8(ms, entry.ByteHash ?? string.Empty);
        WriteInt64(ms, entry.Size);
        WriteInt32(ms, entry.MetaFlags ?? SentinelMissingMetaFlags);
        return ms.ToArray();
    }

    /// <summary>
    ///     Signs <paramref name="entry"/> in place using the caller-provided keypair. Populates
    ///     <see cref="MeshHashEntry.SignerPublicKey"/> and <see cref="MeshHashEntry.Signature"/>.
    /// </summary>
    public static void Sign(MeshHashEntry entry, byte[] privateKey, byte[] publicKey, Ed25519Signer signer)
    {
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (signer == null)
        {
            throw new ArgumentNullException(nameof(signer));
        }

        if (privateKey == null || privateKey.Length != 32)
        {
            throw new ArgumentException("Ed25519 private key must be 32 bytes", nameof(privateKey));
        }

        if (publicKey == null || publicKey.Length != 32)
        {
            throw new ArgumentException("Ed25519 public key must be 32 bytes", nameof(publicKey));
        }

        var data = GetSigningBytes(entry);
        var sig = signer.Sign(data, privateKey);

        entry.SignerPublicKey = Convert.ToBase64String(publicKey);
        entry.Signature = Convert.ToBase64String(sig);
    }

    /// <summary>
    ///     Attempts to verify the signature on <paramref name="entry"/>. Returns <c>false</c> if
    ///     either field is absent, malformed, or if the signature does not verify against the
    ///     embedded public key.
    /// </summary>
    public static bool TryVerify(MeshHashEntry entry, Ed25519Signer signer, out string signerPeerId)
    {
        signerPeerId = string.Empty;

        if (entry == null || signer == null)
        {
            return false;
        }

        if (string.IsNullOrEmpty(entry.SignerPublicKey) || string.IsNullOrEmpty(entry.Signature))
        {
            return false;
        }

        byte[] pub;
        byte[] sig;
        try
        {
            pub = Convert.FromBase64String(entry.SignerPublicKey);
            sig = Convert.FromBase64String(entry.Signature);
        }
        catch (FormatException)
        {
            return false;
        }

        if (pub.Length != 32 || sig.Length != 64)
        {
            return false;
        }

        var data = GetSigningBytes(entry);
        if (!signer.Verify(data, sig, pub))
        {
            return false;
        }

        signerPeerId = Ed25519Signer.DerivePeerId(pub);
        return true;
    }

    /// <summary>
    ///     Indicates whether the entry carries a signature payload at all. Used to distinguish
    ///     "legacy unsigned entry" from "forged/broken signature" in the ingest path.
    /// </summary>
    public static bool HasSignature(MeshHashEntry entry) =>
        entry != null
        && !string.IsNullOrEmpty(entry.SignerPublicKey)
        && !string.IsNullOrEmpty(entry.Signature);

    private static void WriteLengthPrefixedUtf8(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteInt32(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, value);
        stream.Write(buf);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, value);
        stream.Write(buf);
    }
}
