// <copyright file="DescriptorSeqTracker.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tracks the last accepted DescriptorSeq per PeerId to prevent rollback attacks.
/// Persists state to disk.
/// </summary>
public interface IDescriptorSeqTracker
{
    /// <summary>
    /// Checks if a new sequence number is valid (greater than last accepted).
    /// If valid, records it as the new accepted sequence.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="newSeq">The new sequence number to validate.</param>
    /// <returns>True if newSeq > lastAcceptedSeq (or if no previous seq exists).</returns>
    bool ValidateAndUpdate(string peerId, ulong newSeq);

    /// <summary>
    /// Gets the last accepted sequence number for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The last accepted seq, or 0 if none recorded.</returns>
    ulong GetLastAcceptedSeq(string peerId);

    /// <summary>
    /// Persists current state to disk.
    /// </summary>
    void Save();
}

public class DescriptorSeqTracker : IDescriptorSeqTracker
{
    private readonly ILogger<DescriptorSeqTracker> logger;
    private readonly string persistencePath;
    private readonly ConcurrentDictionary<string, ulong> seqMap = new();

    public DescriptorSeqTracker(ILogger<DescriptorSeqTracker> logger, string persistencePath)
    {
        this.logger = logger;
        this.persistencePath = persistencePath;

        Load();
    }

    public bool ValidateAndUpdate(string peerId, ulong newSeq)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            logger.LogWarning("[DescriptorSeqTracker] Cannot validate with null/empty peerId");
            return false;
        }

        var lastSeq = seqMap.GetOrAdd(peerId, 0);

        if (newSeq <= lastSeq)
        {
            logger.LogWarning(
                "[DescriptorSeqTracker] Rollback attack detected: peerId={PeerId}, newSeq={NewSeq}, lastSeq={LastSeq}",
                peerId,
                newSeq,
                lastSeq);
            return false;
        }

        // Update to new sequence
        seqMap[peerId] = newSeq;
        logger.LogDebug("[DescriptorSeqTracker] Accepted seq={Seq} for peerId={PeerId}", newSeq, peerId);

        // Persist asynchronously (fire-and-forget for performance)
        _ = System.Threading.Tasks.Task.Run(() => Save());

        return true;
    }

    public ulong GetLastAcceptedSeq(string peerId)
    {
        return seqMap.TryGetValue(peerId, out var seq) ? seq : 0;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(persistencePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(seqMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value));
            File.WriteAllText(persistencePath, json);

            logger.LogDebug("[DescriptorSeqTracker] Saved {Count} seq entries to {Path}", seqMap.Count, persistencePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DescriptorSeqTracker] Failed to save state to {Path}", persistencePath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(persistencePath))
            {
                logger.LogInformation("[DescriptorSeqTracker] No existing state file at {Path}", persistencePath);
                return;
            }

            var json = File.ReadAllText(persistencePath);
            var data = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ulong>>(json);

            if (data != null)
            {
                foreach (var kvp in data)
                {
                    seqMap[kvp.Key] = kvp.Value;
                }

                logger.LogInformation("[DescriptorSeqTracker] Loaded {Count} seq entries from {Path}", data.Count, persistencePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DescriptorSeqTracker] Failed to load state from {Path}, starting fresh", persistencePath);
        }
    }
}

