// <copyright file="ContentDescriptorPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.MediaCore;

/// <summary>
/// Advanced content descriptor publishing service with versioning and batch operations.
/// </summary>
public class ContentDescriptorPublisher : IContentDescriptorPublisher
{
    private readonly ILogger<ContentDescriptorPublisher> _logger;
    private readonly IDescriptorPublisher _basePublisher;
    private readonly IContentIdRegistry _registry;
    private readonly MediaCoreOptions _options;

    // Track published descriptors for statistics and management
    private readonly ConcurrentDictionary<string, PublishedDescriptorInfo> _publishedDescriptors = new();

    public ContentDescriptorPublisher(
        ILogger<ContentDescriptorPublisher> logger,
        IDescriptorPublisher basePublisher,
        IContentIdRegistry registry,
        IOptions<MediaCoreOptions> options)
    {
        _logger = logger;
        _basePublisher = basePublisher;
        _registry = registry;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<DescriptorPublishResult> PublishAsync(ContentDescriptor descriptor, bool forceUpdate = false, CancellationToken cancellationToken = default)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        // H-MCP01: Check if content is advertisable before publishing to network
        if (descriptor.IsAdvertisable == false)
        {
            _logger.LogWarning(
                "[ContentDescriptorPublisher] Blocked publication of non-advertisable content {ContentId}",
                descriptor.ContentId);

            return new DescriptorPublishResult(
                Success: false,
                ContentId: descriptor.ContentId,
                Version: "0",
                PublishedAt: DateTimeOffset.UtcNow,
                Ttl: TimeSpan.Zero,
                ErrorMessage: "Content is not advertisable",
                WasUpdated: false,
                PreviousVersion: null);
        }

        var startTime = DateTimeOffset.UtcNow;
        var version = GenerateVersion(descriptor);
        var ttl = TimeSpan.FromMinutes(Math.Min(_options.MaxTtlMinutes, 60)); // Cap at 1 hour

        try
        {
            // Check if already published and handle versioning
            if (_publishedDescriptors.TryGetValue(descriptor.ContentId, out var existingInfo))
            {
                if (!forceUpdate && !IsNewerVersion(version, existingInfo.Version))
                {
                    return new DescriptorPublishResult(
                        Success: false,
                        ContentId: descriptor.ContentId,
                        Version: version,
                        PublishedAt: startTime,
                        Ttl: ttl,
                        ErrorMessage: $"Version {version} is not newer than existing {existingInfo.Version}",
                        WasUpdated: false,
                        PreviousVersion: existingInfo.Version);
                }
            }

            if (descriptor.Signature == null)
            {
                return new DescriptorPublishResult(
                    Success: false,
                    ContentId: descriptor.ContentId,
                    Version: version,
                    PublishedAt: startTime,
                    Ttl: ttl,
                    ErrorMessage: "Descriptor signature is required; automatic signing is not implemented.",
                    WasUpdated: false,
                    PreviousVersion: existingInfo?.Version);
            }

            // Publish using base publisher
            var success = await _basePublisher.PublishAsync(descriptor, cancellationToken);

            if (success)
            {
                // Update tracking
                var info = new PublishedDescriptorInfo(
                    ContentId: descriptor.ContentId,
                    Version: version,
                    PublishedAt: startTime,
                    ExpiresAt: startTime + ttl,
                    SizeBytes: descriptor.SizeBytes ?? 0);

                _publishedDescriptors[descriptor.ContentId] = info;

                _logger.LogInformation(
                    "[ContentDescriptorPublisher] Published {ContentId} v{Version} (ttl={TtlMinutes}min)",
                    descriptor.ContentId, version, ttl.TotalMinutes);

                return new DescriptorPublishResult(
                    Success: true,
                    ContentId: descriptor.ContentId,
                    Version: version,
                    PublishedAt: startTime,
                    Ttl: ttl,
                    WasUpdated: existingInfo != null,
                    PreviousVersion: existingInfo?.Version);
            }
            else
            {
                return new DescriptorPublishResult(
                    Success: false,
                    ContentId: descriptor.ContentId,
                    Version: version,
                    PublishedAt: startTime,
                    Ttl: ttl,
                    ErrorMessage: "Base publisher failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to publish {ContentId}", descriptor.ContentId);
            return new DescriptorPublishResult(
                Success: false,
                ContentId: descriptor.ContentId,
                Version: version,
                PublishedAt: startTime,
                Ttl: ttl,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<BatchPublishResult> PublishBatchAsync(IEnumerable<ContentDescriptor> descriptors, CancellationToken cancellationToken = default)
    {
        if (descriptors == null)
            throw new ArgumentNullException(nameof(descriptors));

        var descriptorList = descriptors.ToList();
        var startTime = DateTimeOffset.UtcNow;
        var results = new List<DescriptorPublishResult>();
        var successfullyPublished = 0;
        var failedToPublish = 0;
        var skipped = 0;

        // Process in parallel with limited concurrency
        using var semaphore = new SemaphoreSlim(5); // Limit concurrent operations
        var tasks = descriptorList.Select(async descriptor =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var result = await PublishAsync(descriptor, forceUpdate: false, cancellationToken);
                lock (results)
                {
                    results.Add(result);
                    if (result.Success)
                        successfullyPublished++;
                    else if (result.ErrorMessage?.Contains("not newer") == true)
                        skipped++;
                    else
                        failedToPublish++;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var duration = DateTimeOffset.UtcNow - startTime;

        return new BatchPublishResult(
            TotalRequested: descriptorList.Count,
            SuccessfullyPublished: successfullyPublished,
            FailedToPublish: failedToPublish,
            Skipped: skipped,
            TotalDuration: duration,
            Results: results);
    }

    /// <inheritdoc/>
    public Task<DescriptorUpdateResult> UpdateAsync(string contentId, DescriptorUpdates updates, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("ContentId cannot be empty", nameof(contentId));

        if (updates == null)
            throw new ArgumentNullException(nameof(updates));

        try
        {
            var previousVersion = _publishedDescriptors.TryGetValue(contentId, out var info) ? info.Version : "unknown";
            _logger.LogWarning("[ContentDescriptorPublisher] Update requested for {ContentId}, but descriptor update/republish is not implemented", contentId);
            return Task.FromResult(new DescriptorUpdateResult(
                Success: false,
                ContentId: contentId,
                NewVersion: previousVersion,
                PreviousVersion: previousVersion,
                AppliedUpdates: Array.Empty<string>(),
                ErrorMessage: "Descriptor update/republish is not implemented."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to update {ContentId}", contentId);
            return Task.FromResult(new DescriptorUpdateResult(
                Success: false,
                ContentId: contentId,
                NewVersion: "error",
                PreviousVersion: "unknown",
                AppliedUpdates: Array.Empty<string>(),
                ErrorMessage: ex.Message));
        }
    }

    /// <inheritdoc/>
    public Task<RepublishResult> RepublishExpiringAsync(IEnumerable<string>? contentIds = null, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var checkedCount = 0;
        var republished = 0;
        var failed = 0;
        var stillValid = 0;

        var targetIds = contentIds?.ToList() ?? _publishedDescriptors.Keys.ToList();
        var expiringThreshold = DateTimeOffset.UtcNow.AddMinutes(30); // Republish if expires within 30 min

        foreach (var contentId in targetIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            checkedCount++;

            if (_publishedDescriptors.TryGetValue(contentId, out var info))
            {
                if (info.ExpiresAt <= expiringThreshold)
                {
                    _logger.LogWarning(
                        "[ContentDescriptorPublisher] Descriptor {ContentId} is expiring at {Expiry}, but republish is not implemented",
                        contentId, info.ExpiresAt);
                    failed++;
                }
                else
                {
                    stillValid++;
                }
            }
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        return Task.FromResult(new RepublishResult(
            TotalChecked: checkedCount,
            Republished: republished,
            Failed: failed,
            StillValid: stillValid,
            Duration: duration));
    }

    /// <inheritdoc/>
    public Task<UnpublishResult> UnpublishAsync(string contentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("ContentId cannot be empty", nameof(contentId));

        try
        {
            var wasPublished = _publishedDescriptors.TryRemove(contentId, out _);

            // In a real implementation, this would need to expire/remove from DHT
            // DHT entries typically expire naturally, so this mainly updates local tracking
            _logger.LogInformation(
                "[ContentDescriptorPublisher] Unpublished {ContentId} (was published: {WasPublished})",
                contentId, wasPublished);

            return Task.FromResult(new UnpublishResult(
                Success: true,
                ContentId: contentId,
                WasPublished: wasPublished));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentDescriptorPublisher] Failed to unpublish {ContentId}", contentId);
            return Task.FromResult(new UnpublishResult(
                Success: false,
                ContentId: contentId,
                WasPublished: false,
                ErrorMessage: ex.Message));
        }
    }

    /// <inheritdoc/>
    public Task<PublishingStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var publicationsByDomain = new Dictionary<string, int>();
        var totalStorageBytes = 0L;
        var totalTtlHours = 0.0;
        var activeCount = 0;
        var expiringSoonCount = 0;
        var lastPublish = DateTimeOffset.MinValue;

        foreach (var (contentId, info) in _publishedDescriptors)
        {
            if (info.ExpiresAt > now)
            {
                activeCount++;

                // Expires within 1 hour.
                if (info.ExpiresAt <= now.AddMinutes(60))
                {
                    expiringSoonCount++;
                }

                // Parse domain from ContentID
                var domain = ContentIdParser.GetDomain(contentId) ?? "unknown";
                publicationsByDomain.TryGetValue(domain, out var count);
                publicationsByDomain[domain] = count + 1;

                totalStorageBytes += info.SizeBytes;
                totalTtlHours += (info.ExpiresAt - now).TotalHours;

                if (info.PublishedAt > lastPublish)
                {
                    lastPublish = info.PublishedAt;
                }
            }
        }

        var averageTtlHours = activeCount > 0 ? totalTtlHours / activeCount : 0;

        return Task.FromResult(new PublishingStats(
            TotalPublishedDescriptors: _publishedDescriptors.Count,
            ActivePublications: activeCount,
            ExpiringSoon: expiringSoonCount,
            LastPublishOperation: lastPublish,
            PublicationsByDomain: publicationsByDomain,
            TotalStorageBytes: totalStorageBytes,
            AverageTtlHours: averageTtlHours));
    }

    private static string GenerateVersion(ContentDescriptor descriptor)
    {
        // Create a version string based on descriptor content and timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var content = $"{descriptor.ContentId}:{descriptor.Codec}:{descriptor.SizeBytes}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        var versionHash = BitConverter.ToString(hash).Replace("-", string.Empty).Substring(0, 8).ToLower();

        return $"{timestamp}-{versionHash}";
    }

    private static bool IsNewerVersion(string newVersion, string existingVersion)
    {
        // Simple version comparison - in practice, this might be more sophisticated
        return string.CompareOrdinal(newVersion, existingVersion) > 0;
    }
}

/// <summary>
/// Information about a published descriptor.
/// </summary>
internal record PublishedDescriptorInfo(
    string ContentId,
    string Version,
    DateTimeOffset PublishedAt,
    DateTimeOffset ExpiresAt,
    long SizeBytes);
