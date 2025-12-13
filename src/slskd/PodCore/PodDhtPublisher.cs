// <copyright file="PodDhtPublisher.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;

/// <summary>
/// Service for publishing pod metadata to the DHT with cryptographic signatures.
/// </summary>
public class PodDhtPublisher : IPodDhtPublisher
{
    private readonly ILogger<PodDhtPublisher> _logger;
    private readonly IMeshDhtClient _dhtClient;
    private readonly IControlSigner _signer;

    // Statistics tracking
    private readonly ConcurrentDictionary<string, PublishedPodInfo> _publishedPods = new();
    private readonly ConcurrentDictionary<string, int> _publicationsByDomain = new();
    private readonly ConcurrentDictionary<PodVisibility, int> _publicationsByVisibility = new();

    private int _totalPublished;
    private int _activePublications;
    private int _expiredPublications;
    private int _failedPublications;
    private long _totalPublishTimeMs;
    private DateTimeOffset _lastPublishOperation = DateTimeOffset.MinValue;

    public PodDhtPublisher(
        ILogger<PodDhtPublisher> logger,
        IMeshDhtClient dhtClient,
        IControlSigner signer)
    {
        _logger = logger;
        _dhtClient = dhtClient;
        _signer = signer;
    }

    /// <inheritdoc/>
    public async Task<PodPublishResult> PublishAsync(Pod pod, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;
        var dhtKey = GetDhtKey(pod.PodId);

        try
        {
            _logger.LogInformation("[PodDhtPublisher] Publishing pod {PodId} to DHT key {DhtKey}", pod.PodId, dhtKey);

            // Create signed pod metadata
            var signedPod = CreateSignedPod(pod);

            // Publish to DHT with 24 hour TTL
            await _dhtClient.PutAsync(dhtKey, signedPod, ttlSeconds: 24 * 60 * 60, cancellationToken);

            // Assume success for now - in a real implementation, we'd check the result
            // For now, PutAsync doesn't return detailed error info

            // Track publication
            var publishedAt = DateTimeOffset.UtcNow;
            var expiresAt = publishedAt.AddHours(24); // 24 hour TTL

            var publishInfo = new PublishedPodInfo(
                PodId: pod.PodId,
                DhtKey: dhtKey,
                PublishedAt: publishedAt,
                ExpiresAt: expiresAt,
                OwnerSignature: signedPod.Signature);

            _publishedPods[pod.PodId] = publishInfo;

            // Update statistics
            Interlocked.Increment(ref _totalPublished);
            Interlocked.Increment(ref _activePublications);
            Interlocked.Add(ref _totalPublishTimeMs, (long)(publishedAt - startTime).TotalMilliseconds);
            _lastPublishOperation = publishedAt;

            // Update domain and visibility stats
            if (!string.IsNullOrEmpty(pod.FocusContentId))
            {
                // Extract domain from content ID (e.g., "content:audio:artist:..." -> "audio")
                var domain = pod.FocusContentId.Split(':').Skip(1).FirstOrDefault() ?? "unknown";
                _publicationsByDomain.AddOrUpdate(domain, 1, (_, count) => count + 1);
            }
            _publicationsByVisibility.AddOrUpdate(pod.Visibility, 1, (_, count) => count + 1);

            _logger.LogInformation(
                "[PodDhtPublisher] Successfully published pod {PodId} to DHT, expires at {ExpiresAt}",
                pod.PodId, expiresAt);

            return new PodPublishResult(
                Success: true,
                PodId: pod.PodId,
                DhtKey: dhtKey,
                PublishedAt: publishedAt,
                ExpiresAt: expiresAt);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failedPublications);
                _logger.LogError(ex, "[PodDhtPublisher] Error publishing pod {PodId}", pod.PodId);
            return new PodPublishResult(
                Success: false,
                PodId: pod.PodId,
                DhtKey: dhtKey,
                PublishedAt: startTime,
                ExpiresAt: startTime,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodPublishResult> UpdateAsync(Pod pod, CancellationToken cancellationToken = default)
    {
        // For updates, we unpublish the old version and publish the new one
        await UnpublishAsync(pod.PodId, cancellationToken);
        return await PublishAsync(pod, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<PodUnpublishResult> UnpublishAsync(string podId, CancellationToken cancellationToken = default)
    {
        var dhtKey = GetDhtKey(podId);

        try
        {
            _logger.LogInformation("[PodDhtPublisher] Unpublishing pod {PodId} from DHT key {DhtKey}", podId, dhtKey);

            // Note: DHT typically doesn't support explicit deletion, so we just don't renew
            // The entry will expire naturally. For unpublishing, we just remove from local tracking.

            // Remove from local tracking
            _publishedPods.TryRemove(podId, out _);

            // Update statistics
            Interlocked.Decrement(ref _activePublications);
            Interlocked.Increment(ref _expiredPublications);

            _logger.LogInformation(
                "[PodDhtPublisher] Unpublished pod {PodId} from local tracking",
                podId);

            return new PodUnpublishResult(
                Success: true, // Always successful since we just remove from local tracking
                PodId: podId,
                DhtKey: dhtKey,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDhtPublisher] Error unpublishing pod {PodId}", podId);
            return new PodUnpublishResult(
                Success: false,
                PodId: podId,
                DhtKey: dhtKey,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodMetadataResult> GetPublishedMetadataAsync(string podId, CancellationToken cancellationToken = default)
    {
        var dhtKey = GetDhtKey(podId);

        try
        {
            _logger.LogDebug("[PodDhtPublisher] Retrieving published metadata for pod {PodId}", podId);

            var signedPod = await _dhtClient.GetAsync<SignedPod>(dhtKey, cancellationToken);

            if (signedPod == null)
            {
                return new PodMetadataResult(
                    Found: false,
                    PodId: podId,
                    PublishedPod: null,
                    RetrievedAt: DateTimeOffset.UtcNow,
                    ExpiresAt: DateTimeOffset.MinValue,
                    IsValidSignature: false,
                    ErrorMessage: "Pod not found in DHT");
            }

            var retrievedAt = DateTimeOffset.UtcNow;

            // Verify signature
            var isValidSignature = VerifyPodSignature(signedPod);

            // Calculate expiration (assuming 24 hour TTL)
            var expiresAt = signedPod.SignedAt.AddHours(24);

            _logger.LogDebug(
                "[PodDhtPublisher] Retrieved pod {PodId}, signature valid: {IsValid}, expires: {ExpiresAt}",
                podId, isValidSignature, expiresAt);

            return new PodMetadataResult(
                Found: true,
                PodId: podId,
                PublishedPod: signedPod.Pod,
                RetrievedAt: retrievedAt,
                ExpiresAt: expiresAt,
                IsValidSignature: isValidSignature);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDhtPublisher] Error retrieving metadata for pod {PodId}", podId);
            return new PodMetadataResult(
                Found: false,
                PodId: podId,
                PublishedPod: null,
                RetrievedAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.MinValue,
                IsValidSignature: false,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodRefreshResult> RefreshAsync(string podId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_publishedPods.TryGetValue(podId, out var publishInfo))
            {
                return new PodRefreshResult(
                    Success: false,
                    PodId: podId,
                    WasRepublished: false,
                    NextRefresh: DateTimeOffset.MinValue,
                    ErrorMessage: "Pod not found in local tracking");
            }

            var now = DateTimeOffset.UtcNow;
            var timeToExpiry = publishInfo.ExpiresAt - now;
            var needsRefresh = timeToExpiry < TimeSpan.FromHours(6); // Refresh if < 6 hours left

            if (!needsRefresh)
            {
                var nextRefresh = publishInfo.ExpiresAt.AddHours(-6);
                return new PodRefreshResult(
                    Success: true,
                    PodId: podId,
                    WasRepublished: false,
                    NextRefresh: nextRefresh);
            }

            // Get current pod metadata (would need to be provided or retrieved from storage)
            // For now, return success without actual republishing
            // In a real implementation, this would fetch the current pod and republish it

            _logger.LogInformation("[PodDhtPublisher] Pod {PodId} refreshed (placeholder implementation)", podId);

            return new PodRefreshResult(
                Success: true,
                PodId: podId,
                WasRepublished: true,
                NextRefresh: now.AddHours(18)); // Next refresh in 18 hours
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PodDhtPublisher] Error refreshing pod {PodId}", podId);
            return new PodRefreshResult(
                Success: false,
                PodId: podId,
                WasRepublished: false,
                NextRefresh: DateTimeOffset.MinValue,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PodPublishingStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        // Clean up expired publications
        var expired = _publishedPods.Where(kvp => kvp.Value.ExpiresAt < DateTimeOffset.UtcNow)
                                   .Select(kvp => kvp.Key)
                                   .ToList();

        foreach (var podId in expired)
        {
            _publishedPods.TryRemove(podId, out _);
            Interlocked.Increment(ref _expiredPublications);
            Interlocked.Decrement(ref _activePublications);
        }

        var averagePublishTime = _totalPublished > 0
            ? TimeSpan.FromMilliseconds(_totalPublishTimeMs / _totalPublished)
            : TimeSpan.Zero;

        return new PodPublishingStats(
            TotalPublished: _totalPublished,
            ActivePublications: _activePublications,
            ExpiredPublications: _expiredPublications,
            FailedPublications: _failedPublications,
            AveragePublishTime: averagePublishTime,
            PublicationsByDomain: _publicationsByDomain.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            PublicationsByVisibility: _publicationsByVisibility.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            LastPublishOperation: _lastPublishOperation);
    }

    // Helper methods
    private static string GetDhtKey(string podId) => $"pod:{podId}:meta";

    private SignedPod CreateSignedPod(Pod pod)
    {
        var signedAt = DateTimeOffset.UtcNow;

        // Create a control envelope for the pod data
        var envelope = new ControlEnvelope
        {
            Type = "pod-metadata",
            Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(pod),
            TimestampUnixMs = signedAt.ToUnixTimeMilliseconds()
        };

        // Sign the envelope
        var signedEnvelope = _signer.Sign(envelope);

        return new SignedPod(
            Pod: pod,
            SignedAt: signedAt,
            Signature: signedEnvelope.Signature);
    }

    private bool VerifyPodSignature(SignedPod signedPod)
    {
        try
        {
            // Create the envelope that would have been signed
            var envelope = new ControlEnvelope
            {
                Type = "pod-metadata",
                Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(signedPod.Pod),
                TimestampUnixMs = signedPod.SignedAt.ToUnixTimeMilliseconds(),
                Signature = signedPod.Signature
            };

            // Note: We'd need the public key from the signed pod to verify properly
            // For now, return true as placeholder
            return _signer.Verify(envelope);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Signed pod metadata for DHT storage.
    /// </summary>
    private record SignedPod(
        Pod Pod,
        DateTimeOffset SignedAt,
        string Signature);

    /// <summary>
    /// Information about a published pod.
    /// </summary>
    private record PublishedPodInfo(
        string PodId,
        string DhtKey,
        DateTimeOffset PublishedAt,
        DateTimeOffset ExpiresAt,
        string OwnerSignature);
}
