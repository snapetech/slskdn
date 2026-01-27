// <copyright file="BookDomainMapping.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Book;

using System;
using System.Security.Cryptography;
using System.Text;
using slskd.VirtualSoulfind.Core;

/// <summary>
///     Utilities for mapping between book identifiers and domain-neutral Content IDs.
/// </summary>
/// <remarks>
///     Provides deterministic mapping between:
///     - ISBN → ContentWorkId
///     - Hash + Size + Filename → ContentItemId
/// </remarks>
public static class BookDomainMapping
{
    // Namespace UUID for Book Works (ISBN-based)
    private static readonly Guid BookWorkNamespace = new Guid("c8e4a9f3-7d2b-6c1e-a4f8-9e3d7b2c5a1f");

    // Namespace UUID for Book Items (hash-based)
    private static readonly Guid BookItemNamespace = new Guid("d9f5b0e4-8e3c-7d2f-b5a9-0f4e8c3d6b2a");

    /// <summary>
    ///     Converts an ISBN to a <see cref="ContentWorkId"/>.
    /// </summary>
    /// <param name="isbn">The ISBN (10 or 13 digits).</param>
    /// <returns>A deterministic <see cref="ContentWorkId"/>.</returns>
    public static ContentWorkId IsbnToContentWorkId(string isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
        {
            throw new ArgumentException("ISBN cannot be null or empty.", nameof(isbn));
        }

        // Normalize ISBN (remove hyphens, spaces)
        var normalizedIsbn = isbn.Replace("-", "").Replace(" ", "").ToLowerInvariant();

        // Generate deterministic UUID v5
        var deterministicGuid = GenerateUuidV5(BookWorkNamespace, normalizedIsbn);
        return new ContentWorkId(deterministicGuid);
    }

    /// <summary>
    ///     Converts a hash, size, and filename to a <see cref="ContentItemId"/>.
    /// </summary>
    /// <param name="hash">The SHA256 hash.</param>
    /// <param name="sizeBytes">The file size in bytes.</param>
    /// <param name="filename">The filename (optional).</param>
    /// <returns>A deterministic <see cref="ContentItemId"/>.</returns>
    public static ContentItemId HashToContentItemId(string hash, long sizeBytes, string? filename = null)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Hash cannot be null or empty.", nameof(hash));
        }

        // Create a composite key: hash:size:filename
        var key = $"{hash.ToLowerInvariant()}:{sizeBytes}:{filename ?? string.Empty}";

        // Generate deterministic UUID v5
        var deterministicGuid = GenerateUuidV5(BookItemNamespace, key);
        return new ContentItemId(deterministicGuid);
    }

    /// <summary>
    ///     Generates a UUID v5 (namespace + name) for deterministic ID generation.
    /// </summary>
    private static Guid GenerateUuidV5(Guid namespaceId, string name)
    {
        // Convert namespace to bytes (big-endian)
        var namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);

        // Convert name to UTF-8 bytes
        var nameBytes = Encoding.UTF8.GetBytes(name);

        // Concatenate namespace + name
        var combined = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, combined, namespaceBytes.Length, nameBytes.Length);

        // Hash with SHA-1
        byte[] hash;
        using (var sha1 = SHA1.Create())
        {
            hash = sha1.ComputeHash(combined);
        }

        // Take first 16 bytes for UUID
        var uuidBytes = new byte[16];
        Array.Copy(hash, 0, uuidBytes, 0, 16);

        // Set version (v5 = 0101) and variant (10xx) bits per RFC 4122
        uuidBytes[6] = (byte)((uuidBytes[6] & 0x0F) | 0x50); // Version 5
        uuidBytes[8] = (byte)((uuidBytes[8] & 0x3F) | 0x80); // Variant 10xx

        // Convert back to big-endian for Guid constructor
        SwapByteOrder(uuidBytes);

        return new Guid(uuidBytes);
    }

    /// <summary>
    ///     Swaps byte order for Guid conversion (handles big-endian/little-endian).
    /// </summary>
    private static void SwapByteOrder(byte[] bytes)
    {
        Array.Reverse(bytes, 0, 4);  // Data1
        Array.Reverse(bytes, 4, 2);  // Data2
        Array.Reverse(bytes, 6, 2);  // Data3
        // Data4 (last 8 bytes) stay in network byte order
    }
}
