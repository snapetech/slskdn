// <copyright file="DescriptorRetriever.cs" company="slskdN Team">
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
using slskd.Mesh.Dht;

namespace slskd.MediaCore;

/// <summary>
/// Content descriptor retrieval service implementation with caching and verification.
/// </summary>
public class DescriptorRetriever : IDescriptorRetriever
{
    private readonly ILogger<DescriptorRetriever> _logger;
    private readonly IMeshDhtClient _dht;
    private readonly IDescriptorValidator _validator;
    private readonly MediaCoreOptions _options;

    // Simple in-memory cache (in production, consider Redis or distributed cache)
    private readonly ConcurrentDictionary<string, CachedDescriptor> _cache = new();
    private readonly ConcurrentDictionary<string, int> _retrievalStats = new();

    private DateTimeOffset _lastCacheCleanup = DateTimeOffset.UtcNow;
    private int _totalRetrievals;
    private int _cacheHits;

    public DescriptorRetriever(
        ILogger<DescriptorRetriever> logger,
        IMeshDhtClient dht,
        IDescriptorValidator validator,
        IOptions<MediaCoreOptions> options)
    {
        _logger = logger;
        _dht = dht;
        _validator = validator;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<DescriptorRetrievalResult> RetrieveAsync(string contentId, bool bypassCache = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
            throw new ArgumentException("ContentId cannot be empty", nameof(contentId));

        contentId = contentId.Trim();
        var startTime = DateTimeOffset.UtcNow;
        Interlocked.Increment(ref _totalRetrievals);

        try
        {
            // Check cache first (unless bypassed)
            if (!bypassCache && _cache.TryGetValue(contentId, out var cached))
            {
                if (!IsExpired(cached))
                {
                    Interlocked.Increment(ref _cacheHits);
                    var cacheVerification = await VerifyAsync(cached.Descriptor, cached.RetrievedAt, cancellationToken);

                    return new DescriptorRetrievalResult(
                        Found: true,
                        Descriptor: cached.Descriptor,
                        RetrievedAt: cached.RetrievedAt,
                        RetrievalDuration: DateTimeOffset.UtcNow - startTime,
                        FromCache: true,
                        Verification: cacheVerification);
                }
                else
                {
                    // Remove expired entry
                    _cache.TryRemove(contentId, out _);
                }
            }

            // Retrieve from DHT
            var key = $"mesh:content:{contentId}";
            var retrievedAt = DateTimeOffset.UtcNow;

            ContentDescriptor? descriptor = null;
            string? errorMessage = null;

            try
            {
                descriptor = await _dht.GetAsync<ContentDescriptor>(key, cancellationToken);

                if (descriptor != null)
                {
                    // Cache the result
                    var cacheEntry = new CachedDescriptor(
                        Descriptor: descriptor,
                        RetrievedAt: retrievedAt,
                        ExpiresAt: retrievedAt + TimeSpan.FromMinutes(_options.MaxTtlMinutes));

                    _cache[contentId] = cacheEntry;

                    // Update domain stats
                    var parsed = ContentIdParser.Parse(contentId);
                    var domain = parsed == null
                        ? "unknown"
                        : ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type);
                    _retrievalStats.AddOrUpdate(domain, 1, (_, count) => count + 1);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                _logger.LogWarning(ex, "[DescriptorRetriever] Failed to retrieve {ContentId} from DHT", contentId);
            }

            var verification = descriptor != null
                ? await VerifyAsync(descriptor, retrievedAt, cancellationToken)
                : null;

            var result = new DescriptorRetrievalResult(
                Found: descriptor != null,
                Descriptor: descriptor,
                RetrievedAt: retrievedAt,
                RetrievalDuration: DateTimeOffset.UtcNow - startTime,
                FromCache: false,
                Verification: verification,
                ErrorMessage: errorMessage);

            if (descriptor != null)
            {
                _logger.LogInformation(
                    "[DescriptorRetriever] Retrieved {ContentId} in {Duration}ms (verified: {Verified})",
                    contentId, result.RetrievalDuration.TotalMilliseconds, verification?.IsValid ?? false);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Error retrieving {ContentId}", contentId);
            return new DescriptorRetrievalResult(
                Found: false,
                Descriptor: null,
                RetrievedAt: DateTimeOffset.UtcNow,
                RetrievalDuration: DateTimeOffset.UtcNow - startTime,
                FromCache: false,
                Verification: null,
                ErrorMessage: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<BatchRetrievalResult> RetrieveBatchAsync(IEnumerable<string> contentIds, CancellationToken cancellationToken = default)
    {
        if (contentIds == null)
            throw new ArgumentNullException(nameof(contentIds));

        var contentIdList = contentIds
            .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
            .Select(contentId => contentId.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var startTime = DateTimeOffset.UtcNow;
        var results = new List<DescriptorRetrievalResult>();
        var found = 0;
        var failed = 0;

        // Process in parallel with concurrency control
        using var semaphore = new SemaphoreSlim(10); // Allow more concurrent retrievals for batch

        try
        {
            var tasks = contentIdList.Select(async contentId =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await RetrieveAsync(contentId, bypassCache: false, cancellationToken);
                    lock (results)
                    {
                        results.Add(result);
                        if (result.Found) found++;
                        else if (result.ErrorMessage != null) failed++;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Error in batch retrieval");
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        _logger.LogInformation(
            "[DescriptorRetriever] Batch retrieval: {Requested} requested, {Found} found, {Failed} failed in {Duration}",
            contentIdList.Count, found, failed, duration);

        return new BatchRetrievalResult(
            Requested: contentIdList.Count,
            Found: found,
            Failed: failed,
            TotalDuration: duration,
            Results: results);
    }

    /// <inheritdoc/>
    public Task<DescriptorQueryResult> QueryByDomainAsync(string domain, string? type = null, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain cannot be empty", nameof(domain));

        var trimmedDomain = domain.Trim();
        var trimmedType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();

        domain = ContentIdParser.NormalizeDomain(trimmedDomain, trimmedType ?? string.Empty);
        type = trimmedType == null ? null : ContentIdParser.NormalizeType(domain, trimmedType);
        maxResults = Math.Clamp(maxResults, 1, 500);

        var startTime = DateTimeOffset.UtcNow;
        var results = new List<ContentDescriptor>();
        var hasMore = false;

        try
        {
            var matchingDescriptors = _cache
                .Where(kvp =>
                {
                    if (IsExpired(kvp.Value))
                    {
                        _cache.TryRemove(kvp.Key, out _);
                        return false;
                    }

                    var parsed = ContentIdParser.Parse(kvp.Key);
                    return parsed != null &&
                           ContentIdParser.NormalizeDomain(parsed.Domain, parsed.Type)
                               .Equals(domain, StringComparison.OrdinalIgnoreCase) &&
                           (type == null || ContentIdParser.NormalizeType(parsed.Domain, parsed.Type)
                               .Equals(type, StringComparison.OrdinalIgnoreCase));
                })
                .OrderByDescending(kvp => kvp.Value.RetrievedAt)
                .Select(kvp => kvp.Value.Descriptor)
                .GroupBy(descriptor => descriptor.ContentId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(maxResults + 1)
                .ToList();

            if (matchingDescriptors.Count > maxResults)
            {
                hasMore = true;
                matchingDescriptors = matchingDescriptors.Take(maxResults).ToList();
            }

            results = matchingDescriptors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Error querying domain {Domain}", domain);
        }

        var duration = DateTimeOffset.UtcNow - startTime;

        return Task.FromResult(new DescriptorQueryResult(
            Domain: domain,
            Type: type,
            TotalFound: results.Count,
            QueryDuration: duration,
            Descriptors: results,
            HasMoreResults: hasMore));
    }

    /// <inheritdoc/>
    public async Task<DescriptorVerificationResult> VerifyAsync(ContentDescriptor descriptor, DateTimeOffset retrievedAt, CancellationToken cancellationToken = default)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        var warnings = new List<string>();
        var age = DateTimeOffset.UtcNow - retrievedAt;

        try
        {
            // Basic validation
            var basicValid = _validator.Validate(descriptor, out var reason);
            if (!basicValid)
            {
                return new DescriptorVerificationResult(
                    IsValid: false,
                    SignatureValid: false,
                    FreshnessValid: false,
                    Age: age,
                    ValidationError: reason);
            }

            // Signature verification (simplified)
            var signatureValid = await VerifySignatureAsync(descriptor, cancellationToken);
            if (!signatureValid)
            {
                warnings.Add("Invalid or missing signature");
            }

            // Freshness check (based on signature timestamp if available)
            var freshnessValid = true;
            if (descriptor.Signature?.TimestampUnixMs > 0)
            {
                var signatureTime = DateTimeOffset.FromUnixTimeMilliseconds(descriptor.Signature.TimestampUnixMs);
                var signatureAge = DateTimeOffset.UtcNow - signatureTime;

                // Consider stale if signature is older than max TTL
                if (signatureAge > TimeSpan.FromMinutes(_options.MaxTtlMinutes))
                {
                    freshnessValid = false;
                    warnings.Add($"Signature is stale (age: {signatureAge.TotalHours:F1} hours)");
                }
            }

            // Additional freshness check based on retrieval time
            if (age > TimeSpan.FromMinutes(_options.MaxTtlMinutes))
            {
                freshnessValid = false;
                warnings.Add($"Retrieved data is stale (age: {age.TotalMinutes:F1} minutes)");
            }

            var isValid = signatureValid && freshnessValid;

            return new DescriptorVerificationResult(
                IsValid: isValid,
                SignatureValid: signatureValid,
                FreshnessValid: freshnessValid,
                Age: age,
                Warnings: warnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DescriptorRetriever] Error verifying descriptor {ContentId}", descriptor.ContentId);
            return new DescriptorVerificationResult(
                IsValid: false,
                SignatureValid: false,
                FreshnessValid: false,
                Age: age,
                ValidationError: ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<RetrievalStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        // Perform cache cleanup if needed
        await PerformCacheCleanupAsync(cancellationToken);

        var cacheMisses = _totalRetrievals - _cacheHits;
        var cacheHitRatio = _totalRetrievals > 0 ? (double)_cacheHits / _totalRetrievals : 0;

        var activeEntries = _cache.Values.Count(cached => !IsExpired(cached));
        var cacheSizeBytes = _cache.Values
            .Where(cached => !IsExpired(cached))
            .Sum(cached => EstimateDescriptorSize(cached.Descriptor));

        var avgRetrievalTime = TimeSpan.Zero; // Would need to track individual retrieval times

        return new RetrievalStats(
            TotalRetrievals: _totalRetrievals,
            CacheHits: _cacheHits,
            CacheMisses: cacheMisses,
            CacheHitRatio: cacheHitRatio,
            AverageRetrievalTime: avgRetrievalTime,
            ActiveCacheEntries: activeEntries,
            CacheSizeBytes: cacheSizeBytes,
            LastCacheCleanup: _lastCacheCleanup);
    }

    /// <inheritdoc/>
    public Task<CacheOperationResult> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        var entriesCleared = _cache.Count;
        var bytesFreed = _cache.Values.Sum(c => EstimateDescriptorSize(c.Descriptor));

        _cache.Clear();
        _lastCacheCleanup = DateTimeOffset.UtcNow;

        _logger.LogInformation(
            "[DescriptorRetriever] Cleared cache: {Entries} entries, {Bytes} bytes freed",
            entriesCleared, bytesFreed);

        return Task.FromResult(new CacheOperationResult(
            Success: true,
            EntriesCleared: entriesCleared,
            BytesFreed: bytesFreed));
    }

    private Task<bool> VerifySignatureAsync(ContentDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.Signature == null)
            return Task.FromResult(false);

        try
        {
            // In a real implementation, this would verify the cryptographic signature
            // For now, perform a basic sanity check
            if (string.IsNullOrWhiteSpace(descriptor.Signature.PublicKey) ||
                string.IsNullOrWhiteSpace(descriptor.Signature.Signature))
            {
                return Task.FromResult(false);
            }

            // Verify signature matches expected format (hex)
            if (!IsValidHex(descriptor.Signature.Signature))
            {
                return Task.FromResult(false);
            }

            // Verify timestamp is reasonable (not in future, not too old)
            var signatureTime = DateTimeOffset.FromUnixTimeMilliseconds(descriptor.Signature.TimestampUnixMs);
            var now = DateTimeOffset.UtcNow;
            var age = now - signatureTime;

            if (signatureTime > now.AddMinutes(5))
            {
                // Allow 5 minute clock skew; anything beyond that looks forged.
                return Task.FromResult(false);
            }

            if (age > TimeSpan.FromDays(365))
            {
                // Descriptors older than one year are considered stale.
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[DescriptorRetriever] Signature verification error for {ContentId}", descriptor.ContentId);
            return Task.FromResult(false);
        }
    }

    private Task PerformCacheCleanupAsync(CancellationToken cancellationToken)
    {
        // Remove expired entries
        var expiredKeys = _cache
            .Where(kvp => IsExpired(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _lastCacheCleanup = DateTimeOffset.UtcNow;
            _logger.LogInformation("[DescriptorRetriever] Cleaned {Count} expired cache entries", expiredKeys.Count);
        }

        return Task.CompletedTask;
    }

    private static bool IsExpired(CachedDescriptor cached)
    {
        return DateTimeOffset.UtcNow > cached.ExpiresAt;
    }

    private static bool IsValidHex(string hex)
    {
        return !string.IsNullOrWhiteSpace(hex) &&
               hex.Length % 2 == 0 &&
               hex.All(c => "0123456789abcdefABCDEF".Contains(c));
    }

    private static long EstimateDescriptorSize(ContentDescriptor descriptor)
    {
        // Rough estimation of memory usage
        var size = descriptor.ContentId.Length * 2L; // UTF-16

        if (descriptor.Hashes != null)
            size += descriptor.Hashes.Sum(h => (h.Algorithm.Length + h.Hex.Length) * 2);

        if (descriptor.PerceptualHashes != null)
            size += descriptor.PerceptualHashes.Sum(h => (h.Algorithm.Length + h.Hex.Length) * 2);

        size += (descriptor.Codec?.Length ?? 0) * 2;
        size += 100; // Overhead for object structure

        return size;
    }
}

/// <summary>
/// Cached descriptor with metadata.
/// </summary>
internal record CachedDescriptor(
    ContentDescriptor Descriptor,
    DateTimeOffset RetrievedAt,
    DateTimeOffset ExpiresAt);
