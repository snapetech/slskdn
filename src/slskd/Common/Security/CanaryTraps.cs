// <copyright file="CanaryTraps.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

/// <summary>
/// Implements canary traps (invisible watermarks) for tracking file leaks.
/// SECURITY: Enables identification of which peer leaked a file.
/// </summary>
public sealed class CanaryTraps
{
    private readonly ILogger<CanaryTraps> _logger;
    private readonly ConcurrentDictionary<string, CanaryRecord> _canaries = new();

    /// <summary>
    /// Maximum canaries to track.
    /// </summary>
    public const int MaxCanaries = 10000;

    /// <summary>
    /// Secret key for HMAC-based watermarks.
    /// </summary>
    private readonly byte[] _secretKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="CanaryTraps"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="secretKey">Secret key for watermarking (32 bytes recommended).</param>
    public CanaryTraps(ILogger<CanaryTraps> logger, byte[]? secretKey = null)
    {
        _logger = logger;
        _secretKey = secretKey ?? RandomNumberGenerator.GetBytes(32);
    }

    /// <summary>
    /// Generate a unique canary ID for a peer and file combination.
    /// This ID is embedded invisibly in shared content.
    /// </summary>
    /// <param name="username">Peer username.</param>
    /// <param name="filename">Filename being shared.</param>
    /// <returns>Canary ID and registration record.</returns>
    public CanaryResult GenerateCanary(string username, string filename)
    {
        // Create a deterministic but unpredictable canary based on peer + file + secret
        var input = $"{username}|{filename}|{DateTimeOffset.UtcNow:yyyyMMdd}";
        var inputBytes = Encoding.UTF8.GetBytes(input);

        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(inputBytes);

        // Use first 8 bytes as canary ID (64 bits - enough to be unique)
        var canaryId = Convert.ToHexString(hash[..8]).ToLowerInvariant();

        var record = new CanaryRecord
        {
            CanaryId = canaryId,
            Username = username,
            Filename = filename,
            CreatedAt = DateTimeOffset.UtcNow,
            FullHash = Convert.ToHexString(hash).ToLowerInvariant(),
        };

        // Store for later lookup
        if (_canaries.Count >= MaxCanaries)
        {
            var oldest = _canaries.Values.OrderBy(c => c.CreatedAt).FirstOrDefault();
            if (oldest != null)
            {
                _canaries.TryRemove(oldest.CanaryId, out _);
            }
        }

        _canaries[canaryId] = record;

        _logger.LogDebug(
            "Generated canary {CanaryId} for {Username}:{Filename}",
            canaryId, username, filename);

        return new CanaryResult
        {
            CanaryId = canaryId,
            Record = record,
        };
    }

    /// <summary>
    /// Embed a canary into FLAC metadata (VORBIS_COMMENT padding).
    /// Uses invisible padding characters that don't affect playback.
    /// </summary>
    /// <param name="canaryId">The canary ID to embed.</param>
    /// <returns>Padding bytes to append to metadata.</returns>
    public byte[] GenerateFlacPadding(string canaryId)
    {
        // Encode canary as variable-length padding
        // Uses 0x00 bytes with the canary encoded in the length pattern
        var canaryBytes = Convert.FromHexString(canaryId);
        var padding = new List<byte>();

        // Marker: specific byte sequence that's valid but unusual
        padding.AddRange(new byte[] { 0x00, 0x00, 0x00 });

        // Encode canary bytes as padding lengths
        foreach (var b in canaryBytes)
        {
            // Add b+1 zero bytes for each canary byte
            for (int i = 0; i <= b % 16; i++)
            {
                padding.Add(0x00);
            }

            padding.Add(0x01); // Separator
        }

        return padding.ToArray();
    }

    /// <summary>
    /// Generate invisible filename suffix for tracking.
    /// Uses Unicode zero-width characters.
    /// </summary>
    /// <param name="canaryId">The canary ID.</param>
    /// <returns>Invisible suffix string.</returns>
    public string GenerateInvisibleSuffix(string canaryId)
    {
        // Use zero-width space (U+200B) and zero-width non-joiner (U+200C)
        // to encode binary data in the filename
        var sb = new StringBuilder();

        foreach (var c in canaryId)
        {
            var nibble = Convert.ToInt32(c.ToString(), 16);
            for (int i = 3; i >= 0; i--)
            {
                sb.Append(((nibble >> i) & 1) == 1 ? '\u200B' : '\u200C');
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Decode a canary from an invisible filename suffix.
    /// </summary>
    /// <param name="suffix">The invisible suffix.</param>
    /// <returns>Decoded canary ID or null if invalid.</returns>
    public string? DecodeInvisibleSuffix(string suffix)
    {
        if (string.IsNullOrEmpty(suffix))
        {
            return null;
        }

        // Filter to only zero-width characters
        var bits = suffix.Where(c => c == '\u200B' || c == '\u200C').ToList();

        if (bits.Count < 4 || bits.Count % 4 != 0)
        {
            return null;
        }

        var sb = new StringBuilder();

        for (int i = 0; i < bits.Count; i += 4)
        {
            int nibble = 0;
            for (int j = 0; j < 4; j++)
            {
                nibble <<= 1;
                if (bits[i + j] == '\u200B')
                {
                    nibble |= 1;
                }
            }

            sb.Append(nibble.ToString("x"));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Look up who a canary belongs to.
    /// </summary>
    /// <param name="canaryId">The canary ID.</param>
    /// <returns>Canary record if found.</returns>
    public CanaryRecord? LookupCanary(string canaryId)
    {
        return _canaries.TryGetValue(canaryId.ToLowerInvariant(), out var record) ? record : null;
    }

    /// <summary>
    /// Report a canary sighting (file appeared elsewhere with our canary).
    /// </summary>
    /// <param name="canaryId">The canary ID found.</param>
    /// <param name="foundContext">Where the file was found.</param>
    /// <returns>Investigation result.</returns>
    public CanaryInvestigation ReportSighting(string canaryId, string foundContext)
    {
        var record = LookupCanary(canaryId);

        if (record == null)
        {
            return new CanaryInvestigation
            {
                Found = false,
                Message = "Canary not in registry",
            };
        }

        record.Sightings.Add(new CanarySighting
        {
            Context = foundContext,
            ReportedAt = DateTimeOffset.UtcNow,
        });

        _logger.LogWarning(
            "CANARY ALERT: File {Filename} shared with {Username} found at {Context}",
            record.Filename, record.Username, foundContext);

        return new CanaryInvestigation
        {
            Found = true,
            OriginalUsername = record.Username,
            OriginalFilename = record.Filename,
            SharedAt = record.CreatedAt,
            TotalSightings = record.Sightings.Count,
            Message = $"File was shared with {record.Username} on {record.CreatedAt:g}",
        };
    }

    /// <summary>
    /// Generate per-download unique bytes that can be appended to files.
    /// </summary>
    /// <param name="username">Recipient username.</param>
    /// <param name="filename">Filename.</param>
    /// <param name="length">Length of watermark bytes.</param>
    /// <returns>Unique watermark bytes.</returns>
    public byte[] GenerateWatermarkBytes(string username, string filename, int length = 32)
    {
        var input = $"{username}|{filename}|{Guid.NewGuid()}";
        var inputBytes = Encoding.UTF8.GetBytes(input);

        using var hmac = new HMACSHA256(_secretKey);
        var hash = hmac.ComputeHash(inputBytes);

        // Expand if needed
        if (length <= hash.Length)
        {
            return hash[..length];
        }

        // Use HKDF-like expansion
        var result = new byte[length];
        var counter = 1;
        var pos = 0;

        while (pos < length)
        {
            var counterBytes = BitConverter.GetBytes(counter++);
            var expandInput = hash.Concat(counterBytes).ToArray();
            var expanded = hmac.ComputeHash(expandInput);

            var toCopy = Math.Min(expanded.Length, length - pos);
            Buffer.BlockCopy(expanded, 0, result, pos, toCopy);
            pos += toCopy;
        }

        return result;
    }

    /// <summary>
    /// Get statistics about canaries.
    /// </summary>
    public CanaryStats GetStats()
    {
        var records = _canaries.Values.ToList();
        return new CanaryStats
        {
            TotalCanaries = records.Count,
            CanariesWithSightings = records.Count(r => r.Sightings.Count > 0),
            TotalSightings = records.Sum(r => r.Sightings.Count),
            UniqueUsersTracked = records.Select(r => r.Username).Distinct().Count(),
        };
    }

    /// <summary>
    /// Get canaries for a specific user.
    /// </summary>
    public IReadOnlyList<CanaryRecord> GetCanariesForUser(string username)
    {
        return _canaries.Values
            .Where(r => r.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }
}

/// <summary>
/// A canary registration record.
/// </summary>
public sealed class CanaryRecord
{
    /// <summary>Gets the canary ID.</summary>
    public required string CanaryId { get; init; }

    /// <summary>Gets the username this was shared with.</summary>
    public required string Username { get; init; }

    /// <summary>Gets the filename.</summary>
    public required string Filename { get; init; }

    /// <summary>Gets when created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the full hash for verification.</summary>
    public required string FullHash { get; init; }

    /// <summary>Gets sightings of this canary.</summary>
    public List<CanarySighting> Sightings { get; } = new();
}

/// <summary>
/// A sighting of a canary (file appeared somewhere).
/// </summary>
public sealed class CanarySighting
{
    /// <summary>Gets the context where found.</summary>
    public required string Context { get; init; }

    /// <summary>Gets when reported.</summary>
    public required DateTimeOffset ReportedAt { get; init; }
}

/// <summary>
/// Result of generating a canary.
/// </summary>
public sealed class CanaryResult
{
    /// <summary>Gets the canary ID.</summary>
    public required string CanaryId { get; init; }

    /// <summary>Gets the full record.</summary>
    public required CanaryRecord Record { get; init; }
}

/// <summary>
/// Result of investigating a canary.
/// </summary>
public sealed class CanaryInvestigation
{
    /// <summary>Gets whether the canary was found.</summary>
    public required bool Found { get; init; }

    /// <summary>Gets the original username.</summary>
    public string? OriginalUsername { get; init; }

    /// <summary>Gets the original filename.</summary>
    public string? OriginalFilename { get; init; }

    /// <summary>Gets when originally shared.</summary>
    public DateTimeOffset? SharedAt { get; init; }

    /// <summary>Gets total sightings.</summary>
    public int TotalSightings { get; init; }

    /// <summary>Gets the message.</summary>
    public required string Message { get; init; }
}

/// <summary>
/// Canary statistics.
/// </summary>
public sealed class CanaryStats
{
    /// <summary>Gets total canaries.</summary>
    public int TotalCanaries { get; init; }

    /// <summary>Gets canaries with sightings.</summary>
    public int CanariesWithSightings { get; init; }

    /// <summary>Gets total sightings.</summary>
    public int TotalSightings { get; init; }

    /// <summary>Gets unique users tracked.</summary>
    public int UniqueUsersTracked { get; init; }
}

