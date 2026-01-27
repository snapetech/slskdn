// <copyright file="TvDomainMapping.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Core.Tv;

using System;
using System.Security.Cryptography;
using System.Text;
using slskd.VirtualSoulfind.Core;

/// <summary>
///     Utilities for mapping between TV identifiers and domain-neutral Content IDs.
/// </summary>
/// <remarks>
///     Provides deterministic mapping between:
///     - TVDB ID → ContentWorkId
///     - Hash + Size + Filename → ContentItemId
///     - Series + Season + Episode → ContentItemId
/// </remarks>
public static class TvDomainMapping
{
    // Namespace UUID for TV Works (TVDB ID-based)
    private static readonly Guid TvWorkNamespace = new Guid("a3b8c2d7-1b6e-0a5f-e9d4-3c8b2f7e1a6d");

    // Namespace UUID for TV Items (hash-based)
    private static readonly Guid TvItemNamespace = new Guid("b4c9d3e8-2c7f-1b6g-f0e5-4d9c3g8f2b7e");

    /// <summary>
    ///     Converts a TVDB ID to a <see cref="ContentWorkId"/>.
    /// </summary>
    /// <param name="tvdbId">The TVDB series ID.</param>
    /// <returns>A deterministic <see cref="ContentWorkId"/>.</returns>
    public static ContentWorkId TvdbIdToContentWorkId(string tvdbId)
    {
        if (string.IsNullOrWhiteSpace(tvdbId))
        {
            throw new ArgumentException("TVDB ID cannot be null or empty.", nameof(tvdbId));
        }

        // Normalize TVDB ID (lowercase)
        var normalizedTvdbId = tvdbId.ToLowerInvariant();

        // Generate deterministic UUID v5
        var deterministicGuid = GenerateUuidV5(TvWorkNamespace, normalizedTvdbId);
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
        var deterministicGuid = GenerateUuidV5(TvItemNamespace, key);
        return new ContentItemId(deterministicGuid);
    }

    /// <summary>
    ///     Converts a series ID, season, and episode to a <see cref="ContentItemId"/>.
    /// </summary>
    /// <param name="seriesId">The TVDB series ID.</param>
    /// <param name="season">The season number.</param>
    /// <param name="episode">The episode number.</param>
    /// <returns>A deterministic <see cref="ContentItemId"/>.</returns>
    public static ContentItemId EpisodeToContentItemId(string seriesId, int season, int episode)
    {
        if (string.IsNullOrWhiteSpace(seriesId))
        {
            throw new ArgumentException("Series ID cannot be null or empty.", nameof(seriesId));
        }

        // Create a composite key: seriesId:season:episode
        var key = $"{seriesId.ToLowerInvariant()}:{season}:{episode}";

        // Generate deterministic UUID v5
        var deterministicGuid = GenerateUuidV5(TvItemNamespace, key);
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
