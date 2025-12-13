// <copyright file="MeshPeerId.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Identity;

using System;
using System.Security.Cryptography;

/// <summary>
/// Unique identifier for a mesh peer derived from their Ed25519 public key.
/// Immutable value type that can be used as a dictionary key.
/// </summary>
public readonly record struct MeshPeerId : IComparable<MeshPeerId>
{
    /// <summary>
    /// Gets the base64url-encoded peer ID (truncated SHA256 of public key).
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// Creates a MeshPeerId from an Ed25519 public key.
    /// </summary>
    /// <param name="publicKey">The 32-byte Ed25519 public key.</param>
    /// <returns>A MeshPeerId derived from the public key hash.</returns>
    public static MeshPeerId FromPublicKey(byte[] publicKey)
    {
        if (publicKey == null || publicKey.Length != 32)
        {
            throw new ArgumentException("Public key must be exactly 32 bytes (Ed25519)", nameof(publicKey));
        }

        // Hash the public key and take first 16 bytes for compact ID
        var hash = SHA256.HashData(publicKey);
        var truncated = hash.AsSpan(0, 16);
        
        // Use base64url (URL-safe, no padding) for compact string representation
        var base64 = Convert.ToBase64String(truncated)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new MeshPeerId { Value = base64 };
    }

    /// <summary>
    /// Parses a MeshPeerId from its string representation.
    /// </summary>
    /// <param name="value">The base64url-encoded peer ID.</param>
    /// <returns>A MeshPeerId instance.</returns>
    /// <exception cref="ArgumentException">If value is invalid.</exception>
    public static MeshPeerId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("MeshPeerId value cannot be empty", nameof(value));
        }

        // Basic validation: should be base64url format, approximately 22 chars for 16 bytes
        if (value.Length < 20 || value.Length > 24)
        {
            throw new ArgumentException($"MeshPeerId has invalid length: {value.Length}", nameof(value));
        }

        return new MeshPeerId { Value = value };
    }

    /// <summary>
    /// Tries to parse a MeshPeerId from its string representation.
    /// </summary>
    public static bool TryParse(string? value, out MeshPeerId peerId)
    {
        peerId = default;
        
        if (string.IsNullOrWhiteSpace(value) || value.Length < 20 || value.Length > 24)
        {
            return false;
        }

        peerId = new MeshPeerId { Value = value };
        return true;
    }

    /// <summary>
    /// Returns a short display-friendly version of the peer ID.
    /// </summary>
    public string ToShortString() => Value.Length > 8 ? $"{Value[..8]}…" : Value;

    /// <summary>
    /// Returns the full peer ID.
    /// </summary>
    public override string ToString() => Value;

    /// <summary>
    /// Compares this peer ID to another for sorting.
    /// </summary>
    public int CompareTo(MeshPeerId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);

    /// <summary>
    /// Checks if this peer ID is valid (non-empty).
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
}














