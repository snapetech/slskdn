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
            if (result == null) return null;

            var descriptor = new ContentDescriptor
            {
                ContentId = contentId,
                // Hashes are unknown; we rely on content ID and confidence only.
                Hashes = new List<ContentHash>(),
                Confidence = 0.8 // heuristic: from shadow index
            };

            return descriptor;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[MediaCore] ShadowIndex lookup failed for {ContentId}", contentId);
            return null;
        }
    }
}
















