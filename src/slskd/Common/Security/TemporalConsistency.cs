// <copyright file="TemporalConsistency.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// Tracks metadata changes over time to detect manipulation.
/// SECURITY: Detects peers who change file metadata suspiciously.
/// </summary>
public sealed class TemporalConsistency : IDisposable
{
    private readonly ConcurrentDictionary<string, FileHistory> _fileHistories = new();
    private readonly ConcurrentDictionary<string, PeerMetadataHistory> _peerHistories = new();
    private readonly Timer _cleanupTimer;

    /// <summary>
    /// Maximum history entries per file.
    /// </summary>
    public const int MaxHistoryPerFile = 50;

    /// <summary>
    /// Maximum files to track.
    /// </summary>
    public const int MaxTrackedFiles = 10000;

    /// <summary>
    /// How long to keep history.
    /// </summary>
    public TimeSpan HistoryRetention { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Minimum time between changes to not be suspicious (very fast changes are suspicious).
    /// </summary>
    public TimeSpan MinTimeBetweenChanges { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum changes per file before flagging as suspicious.
    /// </summary>
    public int MaxChangesPerDay { get; init; } = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalConsistency"/> class.
    /// </summary>
    public TemporalConsistency()
    {
        _cleanupTimer = new Timer(CleanupOld, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Record metadata for a file.
    /// </summary>
    /// <param name="username">The peer advertising the file.</param>
    /// <param name="filename">The filename.</param>
    /// <param name="metadata">Current metadata.</param>
    /// <returns>Analysis result.</returns>
    public ConsistencyAnalysis RecordMetadata(string username, string filename, FileMetadata metadata)
    {
        var fileKey = $"{username}:{filename}".ToLowerInvariant();
        var history = GetOrCreateFileHistory(fileKey);

        lock (history)
        {
            var now = DateTimeOffset.UtcNow;
            var issues = new List<string>();
            var isSuspicious = false;
            var changes = new List<string>();

            // Check if metadata changed
            var lastEntry = history.Entries.LastOrDefault();
            if (lastEntry != null)
            {
                changes = DetectChanges(lastEntry.Metadata, metadata);

                if (changes.Count > 0)
                {
                    // Check time since last change
                    var timeSinceLastChange = now - lastEntry.Timestamp;
                    if (timeSinceLastChange < MinTimeBetweenChanges)
                    {
                        issues.Add($"Metadata changed very quickly ({timeSinceLastChange.TotalSeconds:F0}s since last change)");
                        isSuspicious = true;
                    }

                    // Check change frequency
                    var changesInLastDay = history.Entries
                        .Where(e => e.Timestamp > now.AddDays(-1) && e.Changes.Count > 0)
                        .Count();
                    if (changesInLastDay >= MaxChangesPerDay)
                    {
                        issues.Add($"Too many changes ({changesInLastDay}) in 24 hours");
                        isSuspicious = true;
                    }

                    // Check for size oscillation (size keeps changing back and forth)
                    if (changes.Contains("Size") && DetectSizeOscillation(history))
                    {
                        issues.Add("File size keeps changing - possible manipulation");
                        isSuspicious = true;
                    }

                    // Check for hash changing (very suspicious)
                    if (changes.Contains("Hash"))
                    {
                        issues.Add("File hash changed - content replaced or manipulated");
                        isSuspicious = true;
                    }
                }
            }

            // Add entry
            var entry = new MetadataEntry
            {
                Timestamp = now,
                Metadata = metadata,
                Changes = lastEntry != null ? DetectChanges(lastEntry.Metadata, metadata) : new List<string>(),
            };

            history.Entries.Add(entry);

            // Trim if too large
            while (history.Entries.Count > MaxHistoryPerFile)
            {
                history.Entries.RemoveAt(0);
            }

            history.LastUpdated = now;

            // Also track per-peer
            TrackPeerMetadata(username, changes.Count > 0, isSuspicious);

            return new ConsistencyAnalysis
            {
                IsSuspicious = isSuspicious,
                Issues = issues,
                ChangeCount = history.Entries.Count(e => e.Changes.Count > 0),
                FirstSeen = history.Entries.FirstOrDefault()?.Timestamp ?? now,
                LastSeen = now,
            };
        }
    }

    /// <summary>
    /// Get history for a file.
    /// </summary>
    public FileHistory? GetFileHistory(string username, string filename)
    {
        var fileKey = $"{username}:{filename}".ToLowerInvariant();
        return _fileHistories.TryGetValue(fileKey, out var h) ? h : null;
    }

    /// <summary>
    /// Get metadata change summary for a peer.
    /// </summary>
    public PeerMetadataHistory? GetPeerHistory(string username)
    {
        return _peerHistories.TryGetValue(username.ToLowerInvariant(), out var h) ? h : null;
    }

    /// <summary>
    /// Check if a peer has suspicious metadata patterns.
    /// </summary>
    public bool IsPeerSuspicious(string username)
    {
        if (!_peerHistories.TryGetValue(username.ToLowerInvariant(), out var history))
        {
            return false;
        }

        // Suspicious if more than 20% of changes are flagged
        if (history.TotalChanges > 10 && history.SuspiciousChanges > history.TotalChanges * 0.2)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get statistics.
    /// </summary>
    public TemporalStats GetStats()
    {
        return new TemporalStats
        {
            TrackedFiles = _fileHistories.Count,
            TrackedPeers = _peerHistories.Count,
            TotalChangesRecorded = _peerHistories.Values.Sum(p => p.TotalChanges),
            SuspiciousChanges = _peerHistories.Values.Sum(p => p.SuspiciousChanges),
            SuspiciousPeers = _peerHistories.Values.Count(p =>
                p.TotalChanges > 10 && p.SuspiciousChanges > p.TotalChanges * 0.2),
        };
    }

    private FileHistory GetOrCreateFileHistory(string fileKey)
    {
        // Enforce max size
        if (_fileHistories.Count >= MaxTrackedFiles && !_fileHistories.ContainsKey(fileKey))
        {
            var oldest = _fileHistories
                .OrderBy(kvp => kvp.Value.LastUpdated)
                .FirstOrDefault();
            if (!string.IsNullOrEmpty(oldest.Key))
            {
                _fileHistories.TryRemove(oldest.Key, out _);
            }
        }

        return _fileHistories.GetOrAdd(fileKey, _ => new FileHistory());
    }

    private void TrackPeerMetadata(string username, bool wasChange, bool wasSuspicious)
    {
        var peerKey = username.ToLowerInvariant();
        var history = _peerHistories.GetOrAdd(peerKey, _ => new PeerMetadataHistory { Username = username });

        lock (history)
        {
            if (wasChange)
            {
                history.TotalChanges++;
                history.LastChange = DateTimeOffset.UtcNow;
            }

            if (wasSuspicious)
            {
                history.SuspiciousChanges++;
            }
        }
    }

    private static List<string> DetectChanges(FileMetadata old, FileMetadata @new)
    {
        var changes = new List<string>();

        if (old.Size != @new.Size)
        {
            changes.Add("Size");
        }

        if (old.Hash != @new.Hash && !string.IsNullOrEmpty(old.Hash) && !string.IsNullOrEmpty(@new.Hash))
        {
            changes.Add("Hash");
        }

        if (old.Bitrate != @new.Bitrate && old.Bitrate > 0 && @new.Bitrate > 0)
        {
            changes.Add("Bitrate");
        }

        if (old.Duration != @new.Duration && old.Duration > 0 && @new.Duration > 0)
        {
            changes.Add("Duration");
        }

        if (old.SampleRate != @new.SampleRate && old.SampleRate > 0 && @new.SampleRate > 0)
        {
            changes.Add("SampleRate");
        }

        return changes;
    }

    private static bool DetectSizeOscillation(FileHistory history)
    {
        // Look for A → B → A size pattern in recent entries
        var recentSizes = history.Entries
            .TakeLast(5)
            .Select(e => e.Metadata.Size)
            .ToList();

        if (recentSizes.Count < 3)
        {
            return false;
        }

        // Check if any size appears more than once with different sizes in between
        for (int i = 0; i < recentSizes.Count - 2; i++)
        {
            if (recentSizes[i] != recentSizes[i + 1] &&
                recentSizes[i] == recentSizes[i + 2])
            {
                return true;
            }
        }

        return false;
    }

    private void CleanupOld(object? state)
    {
        var cutoff = DateTimeOffset.UtcNow - HistoryRetention;

        var toRemove = _fileHistories
            .Where(kvp => kvp.Value.LastUpdated < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
        {
            _fileHistories.TryRemove(key, out _);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _cleanupTimer.Dispose();
    }
}

/// <summary>
/// File metadata to track.
/// </summary>
public sealed class FileMetadata
{
    /// <summary>Gets or sets the file size.</summary>
    public long Size { get; set; }

    /// <summary>Gets or sets the file hash (if known).</summary>
    public string? Hash { get; set; }

    /// <summary>Gets or sets the bitrate.</summary>
    public int Bitrate { get; set; }

    /// <summary>Gets or sets the duration in seconds.</summary>
    public int Duration { get; set; }

    /// <summary>Gets or sets the sample rate.</summary>
    public int SampleRate { get; set; }

    /// <summary>Gets or sets the codec.</summary>
    public string? Codec { get; set; }
}

/// <summary>
/// A single metadata observation.
/// </summary>
public sealed class MetadataEntry
{
    /// <summary>Gets when observed.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets the metadata.</summary>
    public required FileMetadata Metadata { get; init; }

    /// <summary>Gets what changed from previous.</summary>
    public required List<string> Changes { get; init; }
}

/// <summary>
/// History of metadata for a file.
/// </summary>
public sealed class FileHistory
{
    /// <summary>Gets the metadata entries.</summary>
    public List<MetadataEntry> Entries { get; } = new();

    /// <summary>Gets or sets when last updated.</summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Per-peer metadata change tracking.
/// </summary>
public sealed class PeerMetadataHistory
{
    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets total metadata changes.</summary>
    public int TotalChanges { get; set; }

    /// <summary>Gets or sets suspicious changes.</summary>
    public int SuspiciousChanges { get; set; }

    /// <summary>Gets or sets when last change occurred.</summary>
    public DateTimeOffset? LastChange { get; set; }
}

/// <summary>
/// Result of consistency analysis.
/// </summary>
public sealed class ConsistencyAnalysis
{
    /// <summary>Gets whether the metadata pattern is suspicious.</summary>
    public required bool IsSuspicious { get; init; }

    /// <summary>Gets any issues detected.</summary>
    public required List<string> Issues { get; init; }

    /// <summary>Gets total changes for this file.</summary>
    public required int ChangeCount { get; init; }

    /// <summary>Gets when first seen.</summary>
    public required DateTimeOffset FirstSeen { get; init; }

    /// <summary>Gets when last seen.</summary>
    public required DateTimeOffset LastSeen { get; init; }
}

/// <summary>
/// Statistics about temporal consistency tracking.
/// </summary>
public sealed class TemporalStats
{
    /// <summary>Gets tracked files count.</summary>
    public int TrackedFiles { get; init; }

    /// <summary>Gets tracked peers count.</summary>
    public int TrackedPeers { get; init; }

    /// <summary>Gets total changes recorded.</summary>
    public long TotalChangesRecorded { get; init; }

    /// <summary>Gets suspicious changes count.</summary>
    public long SuspiciousChanges { get; init; }

    /// <summary>Gets suspicious peers count.</summary>
    public int SuspiciousPeers { get; init; }
}

