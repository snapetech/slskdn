// <copyright file="ShadowIndexBuilderImpl.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Audio;
using slskd.VirtualSoulfind.Capture;

namespace slskd.VirtualSoulfind.ShadowIndex;

/// <summary>
/// Aggregates observations into shadow index shards.
/// </summary>
public class ShadowIndexBuilder : IShadowIndexBuilder
{
    private readonly ILogger<ShadowIndexBuilder> logger;
    private readonly IUsernamePseudonymizer pseudonymizer;
    private readonly ConcurrentDictionary<string, List<VariantObservation>> observations = new();

    public ShadowIndexBuilder(
        ILogger<ShadowIndexBuilder> logger,
        IUsernamePseudonymizer pseudonymizer)
    {
        this.logger = logger;
        this.pseudonymizer = pseudonymizer;
    }

    public async Task AddVariantObservationAsync(
        string username,
        string recordingId,
        AudioVariant variant,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-SHADOW] Adding variant observation for {RecordingId} from {Username}",
            recordingId, username);

        // Pseudonymize username
        var peerId = await pseudonymizer.GetPeerIdAsync(username, ct);

        // Store observation
        var obs = new VariantObservation
        {
            PeerId = peerId,
            Variant = variant,
            ObservedAt = DateTimeOffset.UtcNow
        };

        observations.AddOrUpdate(
            recordingId,
            _ => new List<VariantObservation> { obs },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(obs);
                    
                    // Keep only recent observations (last 100 per recording)
                    if (list.Count > 100)
                    {
                        list.RemoveAt(0);
                    }
                }
                return list;
            });

        logger.LogInformation("[VSF-SHADOW] Recorded variant observation: {RecordingId} from {PeerId}",
            recordingId, peerId);
    }

    public Task<ShadowIndexShard?> BuildShardAsync(string mbid, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SHADOW] Building shard for {MBID}", mbid);

        if (!observations.TryGetValue(mbid, out var variantList))
        {
            logger.LogDebug("[VSF-SHADOW] No observations for {MBID}", mbid);
            return Task.FromResult<ShadowIndexShard?>(null);
        }

        List<VariantObservation> snapshot;
        lock (variantList)
        {
            snapshot = variantList.ToList();
        }

        if (snapshot.Count == 0)
        {
            return Task.FromResult<ShadowIndexShard?>(null);
        }

        // Build compact peer ID hints (first 8 bytes of each unique peer ID)
        var peerIdHints = snapshot
            .Select(obs => obs.PeerId)
            .Distinct()
            .Select(peerId => System.Text.Encoding.UTF8.GetBytes(peerId).Take(8).ToArray())
            .ToList();

        // Build canonical variant hints (top N by quality score)
        var canonicalVariants = snapshot
            .GroupBy(obs => obs.Variant.Codec)
            .SelectMany(group => group
                .OrderByDescending(obs => obs.Variant.QualityScore)
                .Take(3))  // Top 3 per codec
            .Select(obs => new VariantHint
            {
                Codec = obs.Variant.Codec ?? "UNKNOWN",
                BitrateKbps = obs.Variant.BitrateKbps,
                SizeBytes = obs.Variant.FileSizeBytes,
                HashPrefix = ParseHashPrefix(obs.Variant.FileSha256),
                QualityScore = obs.Variant.QualityScore
            })
            .ToList();

        var shard = new ShadowIndexShard
        {
            ShardVersion = "1.0",
            Timestamp = DateTimeOffset.UtcNow,
            TTLSeconds = 3600,  // 1 hour
            PeerIdHints = peerIdHints,
            CanonicalVariants = canonicalVariants,
            ApproximatePeerCount = peerIdHints.Count
        };

        logger.LogInformation("[VSF-SHADOW] Built shard for {MBID}: {PeerCount} peers, {VariantCount} variants",
            mbid, shard.ApproximatePeerCount, shard.CanonicalVariants.Count);

        return Task.FromResult<ShadowIndexShard?>(shard);
    }

    private static byte[] ParseHashPrefix(string? sha256Hex)
    {
        if (string.IsNullOrEmpty(sha256Hex))
        {
            return Array.Empty<byte>();
        }

        try
        {
            // Take first 16 bytes (32 hex chars)
            var prefix = sha256Hex.Length > 32 ? sha256Hex.Substring(0, 32) : sha256Hex;
            return Convert.FromHexString(prefix);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}

/// <summary>
/// Internal variant observation record.
/// </summary>
internal class VariantObservation
{
    public string PeerId { get; set; } = string.Empty;
    public AudioVariant Variant { get; set; } = new();
    public DateTimeOffset ObservedAt { get; set; }
}
