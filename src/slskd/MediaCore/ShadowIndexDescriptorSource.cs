// <copyright file="ShadowIndexDescriptorSource.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
using Microsoft.Extensions.Logging;
using slskd.VirtualSoulfind.ShadowIndex;

namespace slskd.MediaCore;

/// <summary>
/// Content descriptor source backed by shadow index queries (best-effort).
/// </summary>
public class ShadowIndexDescriptorSource : IContentDescriptorSource
{
    private readonly ILogger<ShadowIndexDescriptorSource> logger;
    private readonly IShadowIndexQuery shadowIndex;

    public ShadowIndexDescriptorSource(ILogger<ShadowIndexDescriptorSource> logger, IShadowIndexQuery shadowIndex)
    {
        this.logger = logger;
        this.shadowIndex = shadowIndex;
    }

    public async IAsyncEnumerable<ContentDescriptor> GetDescriptorsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // This source is best-effort: it does not enumerate all content IDs.
        // It provides descriptors only when explicitly asked via PublishForAsync.
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// Build a descriptor for a specific contentId (MB recording) using shadow index hints.
    /// </summary>
    public async Task<ContentDescriptor?> BuildForAsync(string contentId, CancellationToken ct = default)
    {
        // Expect format: content:mb:recording:<mbid>
        if (!contentId.StartsWith("content:mb:recording:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var mbid = contentId.Substring("content:mb:recording:".Length);
        try
        {
            var result = await shadowIndex.QueryAsync(mbid, ct);
            if (result == null)
            {
                return null;
            }

            var descriptor = BuildDescriptor(contentId, result);
            if (descriptor == null)
            {
                logger.LogDebug("[MediaCore] ShadowIndex returned no usable variant hints for {ContentId}", contentId);
            }

            return descriptor;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[MediaCore] ShadowIndex lookup failed for {ContentId}", contentId);
            return null;
        }
    }

    private static ContentDescriptor? BuildDescriptor(string contentId, ShadowIndexQueryResult result)
    {
        var variants = result.CanonicalVariants
            .Where(variant => variant != null)
            .OrderByDescending(variant => variant.QualityScore)
            .ThenByDescending(variant => variant.SizeBytes)
            .ToList();
        if (variants.Count == 0)
        {
            return null;
        }

        var bestVariant = variants[0];
        var hashes = variants
            .Where(variant => variant.HashPrefix != null && variant.HashPrefix.Length > 0)
            .Select(variant => new ContentHash("sha256-prefix16", Convert.ToHexString(variant.HashPrefix).ToLowerInvariant()))
            .Distinct()
            .ToList();
        var peerContribution = Math.Min(0.15, result.TotalPeerCount * 0.03);
        var qualityContribution = Math.Min(0.18, Math.Max(0.0, bestVariant.QualityScore) * 0.18);
        var confidenceBase = 0.55 + peerContribution + qualityContribution;
        var confidence = Math.Min(0.98, confidenceBase);

        return new ContentDescriptor
        {
            ContentId = contentId,
            Hashes = hashes,
            SizeBytes = bestVariant.SizeBytes > 0 ? bestVariant.SizeBytes : null,
            Codec = string.IsNullOrWhiteSpace(bestVariant.Codec) ? null : bestVariant.Codec,
            Confidence = confidence,
            IsAdvertisable = true,
        };
    }
}
