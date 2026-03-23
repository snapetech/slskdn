// <copyright file="PodOpinionService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.MediaCore;
using slskd.Mesh.Dht;
using slskd.Mesh.Transport;

/// <summary>
///     Service for managing pod member opinions on content variants.
/// </summary>
public class PodOpinionService : IPodOpinionService
{
    private const string SignaturePrefix = "ed25519:";

    private readonly IPodService _podService;
    private readonly IMeshDhtClient _dhtClient;
    private readonly Ed25519Signer _ed25519;
    private readonly ILogger<PodOpinionService> _logger;

    // Cache for opinions (podId -> contentId -> opinions)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<PodVariantOpinion>>> _opinionCache = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _knownContentIdsByPod = new();

    // Statistics tracking
    private long _totalOpinionsPublished;
    private long _totalOpinionsRetrieved;
    private readonly ConcurrentDictionary<string, long> _opinionsByPod = new();

    public PodOpinionService(
        IPodService podService,
        IMeshDhtClient dhtClient,
        Ed25519Signer ed25519,
        ILogger<PodOpinionService> logger)
    {
        _podService = podService;
        _dhtClient = dhtClient;
        _ed25519 = ed25519;
        _logger = logger;
    }

    public async Task<OpinionPublishResult> PublishOpinionAsync(string podId, PodVariantOpinion opinion, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        opinion = NormalizeOpinion(opinion);

        try
        {
            // Validate the opinion first
            var validation = await ValidateOpinionAsync(podId, opinion, ct);
            if (!validation.IsValid)
            {
                return new OpinionPublishResult(
                    false, podId, opinion.ContentId, opinion.VariantHash, validation.ErrorMessage);
            }

            // Create DHT key: pod:<PodId>:opinions:<ContentId>
            var dhtKey = $"pod:{podId}:opinions:{opinion.ContentId}";

            // Get existing opinions for this content
            var existingOpinions = await GetOpinionsFromDhtAsync(dhtKey, ct);
            existingOpinions.RemoveAll(existing =>
                string.Equals(existing.SenderPeerId, opinion.SenderPeerId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.VariantHash, opinion.VariantHash, StringComparison.OrdinalIgnoreCase));
            existingOpinions.Add(opinion);

            // Store updated opinions list in DHT
            await _dhtClient.PutAsync(dhtKey, existingOpinions, ttlSeconds: 3600, ct); // 1 hour TTL

            // Update local cache
            AddOpinionToCache(podId, opinion);
            TrackKnownContentId(podId, opinion.ContentId);

            // Update statistics
            Interlocked.Increment(ref _totalOpinionsPublished);
            _opinionsByPod.AddOrUpdate(podId, 1, (_, count) => count + 1);

            _logger.LogInformation("Published opinion for pod {PodId} content {ContentId} variant {VariantHash}",
                podId, opinion.ContentId, opinion.VariantHash);

            return new OpinionPublishResult(
                true, podId, opinion.ContentId, opinion.VariantHash, PublishedOpinion: opinion);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing opinion for pod {PodId}", podId);
            return new OpinionPublishResult(
                false, podId, opinion.ContentId, opinion.VariantHash, "Failed to publish opinion");
        }
    }

    public async Task<IReadOnlyList<PodVariantOpinion>> GetOpinionsAsync(string podId, string contentId, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        contentId = contentId?.Trim() ?? string.Empty;

        // Check cache first
        if (_opinionCache.TryGetValue(podId, out var podCache) &&
            podCache.TryGetValue(contentId, out var cachedOpinions))
        {
            var snapshot = SnapshotOpinions(cachedOpinions);
            Interlocked.Add(ref _totalOpinionsRetrieved, snapshot.Count);
            return snapshot;
        }

        // Fetch from DHT
        var dhtKey = $"pod:{podId}:opinions:{contentId}";
        var opinions = await GetOpinionsFromDhtAsync(dhtKey, ct);

        // Update cache
        var contentCache = _opinionCache.GetOrAdd(podId, _ => new ConcurrentDictionary<string, List<PodVariantOpinion>>());
        var opinionList = opinions.ToList();
        contentCache[contentId] = opinionList;
        TrackKnownContentId(podId, contentId);

        Interlocked.Add(ref _totalOpinionsRetrieved, opinions.Count);
        return SnapshotOpinions(opinionList);
    }

    public async Task<IReadOnlyList<PodVariantOpinion>> GetVariantOpinionsAsync(string podId, string contentId, string variantHash, CancellationToken ct = default)
    {
        variantHash = variantHash?.Trim() ?? string.Empty;
        var allOpinions = await GetOpinionsAsync(podId, contentId, ct);
        return allOpinions.Where(o => string.Equals(o.VariantHash, variantHash, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<OpinionValidationResult> ValidateOpinionAsync(string podId, PodVariantOpinion opinion, CancellationToken ct = default)
    {
        podId = podId?.Trim() ?? string.Empty;
        opinion = NormalizeOpinion(opinion);

        // Validate pod exists
        var pod = await _podService.GetPodAsync(podId, ct);
        if (pod == null)
        {
            return new OpinionValidationResult(false, $"Pod {podId} does not exist");
        }

        // Validate sender is pod member
        var members = await _podService.GetMembersAsync(podId, ct);
        var sender = members.FirstOrDefault(m =>
            string.Equals(m.PeerId?.Trim(), opinion.SenderPeerId, StringComparison.OrdinalIgnoreCase) &&
            !m.IsBanned);
        if (sender == null)
        {
            return new OpinionValidationResult(false, $"Sender {opinion.SenderPeerId} is not a member of pod {podId}");
        }

        // Validate content ID format
        if (!ContentIdParser.IsValid(opinion.ContentId))
        {
            return new OpinionValidationResult(false, "Invalid content ID format");
        }

        // Validate score is reasonable
        if (opinion.Score < 0 || opinion.Score > 10)
        {
            return new OpinionValidationResult(false, "Score must be between 0 and 10");
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(opinion.VariantHash))
        {
            return new OpinionValidationResult(false, "Variant hash is required");
        }

        if (string.IsNullOrWhiteSpace(opinion.SenderPeerId))
        {
            return new OpinionValidationResult(false, "Sender peer ID is required");
        }

        if (string.IsNullOrWhiteSpace(opinion.Signature))
        {
            return new OpinionValidationResult(false, "Opinion signatures are required.");
        }

        if (string.IsNullOrWhiteSpace(sender.PublicKey))
        {
            return new OpinionValidationResult(false, $"Sender {opinion.SenderPeerId} has no public key");
        }

        if (!TryParseSignature(opinion.Signature, out var signatureBytes, out var signatureError))
        {
            return new OpinionValidationResult(false, signatureError);
        }

        if (!TryParsePublicKey(sender.PublicKey, out var publicKeyBytes, out var publicKeyError))
        {
            return new OpinionValidationResult(false, publicKeyError);
        }

        var payload = CreateCanonicalOpinionPayload(podId, opinion);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var isValidSignature = _ed25519.Verify(payloadBytes, signatureBytes!, publicKeyBytes!);
        if (!isValidSignature)
        {
            return new OpinionValidationResult(false, "Opinion signature is invalid.");
        }

        return new OpinionValidationResult(true, ValidatedOpinion: opinion);
    }

    public async Task<OpinionStatistics> GetOpinionStatisticsAsync(string podId, string contentId, CancellationToken ct = default)
    {
        var opinions = await GetOpinionsAsync(podId, contentId, ct);

        if (!opinions.Any())
        {
            return new OpinionStatistics(
                PodId: podId,
                ContentId: contentId,
                TotalOpinions: 0,
                UniqueVariants: 0,
                AverageScore: 0,
                MinScore: 0,
                MaxScore: 0,
                ScoreDistribution: new(),
                LastUpdated: DateTimeOffset.UtcNow);
        }

        var scores = opinions.Select(o => o.Score).ToList();
        var scoreGroups = opinions.GroupBy(o => Math.Floor(o.Score))
                                 .ToDictionary(g => g.Key.ToString("F0"), g => g.Count());

        return new OpinionStatistics(
            PodId: podId,
            ContentId: contentId,
            TotalOpinions: opinions.Count,
            UniqueVariants: opinions.Select(o => o.VariantHash).Distinct().Count(),
            AverageScore: scores.Average(),
            MinScore: scores.Min(),
            MaxScore: scores.Max(),
            ScoreDistribution: scoreGroups,
            LastUpdated: DateTimeOffset.UtcNow);
    }

    public async Task<OpinionRefreshResult> RefreshOpinionsAsync(string podId, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var refreshedCount = 0;
        var newOpinionsCount = 0;

        try
        {
            var previousCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (_opinionCache.TryGetValue(podId, out var existingCache))
            {
                foreach (var entry in existingCache)
                {
                    lock (entry.Value)
                    {
                        previousCounts[entry.Key] = entry.Value.Count;
                    }
                }
            }

            // Clear cache for this pod
            _opinionCache.TryRemove(podId, out _);

            var contentIds = GetKnownContentIds(podId);
            if (contentIds.Count == 0)
            {
                return new OpinionRefreshResult(true, podId, 0, 0, stopwatch.Elapsed);
            }

            foreach (var contentId in contentIds)
            {
                var dhtKey = $"pod:{podId}:opinions:{contentId}";
                var opinions = await GetOpinionsFromDhtAsync(dhtKey, ct);

                if (opinions.Any())
                {
                    var cache = _opinionCache.GetOrAdd(podId, _ => new ConcurrentDictionary<string, List<PodVariantOpinion>>());
                    cache[contentId] = opinions.ToList();
                    refreshedCount += opinions.Count;

                    var previousCount = previousCounts.TryGetValue(contentId, out var count)
                        ? count
                        : 0;
                    if (opinions.Count > previousCount)
                    {
                        newOpinionsCount += opinions.Count - previousCount;
                    }
                }
            }

            return new OpinionRefreshResult(
                true, podId, refreshedCount, newOpinionsCount, stopwatch.Elapsed);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing opinions for pod {PodId}", podId);
            return new OpinionRefreshResult(
                false, podId, refreshedCount, newOpinionsCount, stopwatch.Elapsed, "Failed to refresh opinions");
        }
    }

    private async Task<List<PodVariantOpinion>> GetOpinionsFromDhtAsync(string dhtKey, CancellationToken ct)
    {
        try
        {
            var opinions = await _dhtClient.GetAsync<List<PodVariantOpinion>>(dhtKey, ct);

            if (opinions != null)
            {
                // Validate each opinion
                var validOpinions = new List<PodVariantOpinion>();
                var podId = ExtractPodIdFromKey(dhtKey);

                foreach (var opinion in opinions)
                {
                    var validation = await ValidateOpinionAsync(podId, opinion, ct);
                    if (validation.IsValid)
                    {
                        validOpinions.Add(opinion);
                    }
                }

                return validOpinions;
            }

            return new List<PodVariantOpinion>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error retrieving opinions from DHT key {Key}", dhtKey);
            return new List<PodVariantOpinion>();
        }
    }

    private void AddOpinionToCache(string podId, PodVariantOpinion opinion)
    {
        var podCache = _opinionCache.GetOrAdd(podId, _ => new ConcurrentDictionary<string, List<PodVariantOpinion>>());
        var contentOpinions = podCache.GetOrAdd(opinion.ContentId, _ => new List<PodVariantOpinion>());

        lock (contentOpinions)
        {
            contentOpinions.RemoveAll(existing =>
                string.Equals(existing.SenderPeerId, opinion.SenderPeerId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.VariantHash, opinion.VariantHash, StringComparison.OrdinalIgnoreCase));
            contentOpinions.Add(opinion);
        }
    }

    private static IReadOnlyList<PodVariantOpinion> SnapshotOpinions(List<PodVariantOpinion> opinions)
    {
        lock (opinions)
        {
            return opinions.ToList();
        }
    }

    private static string CreateCanonicalOpinionPayload(string podId, PodVariantOpinion opinion)
    {
        var noteHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(opinion.Note ?? string.Empty)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"1|{podId}|{opinion.ContentId}|{opinion.VariantHash}|{opinion.SenderPeerId}|{opinion.Score:G17}|{noteHash}");
    }

    private static bool TryParseSignature(string signature, out byte[]? signatureBytes, out string? errorMessage)
    {
        signatureBytes = null;
        errorMessage = null;
        signature = signature?.Trim() ?? string.Empty;

        if (!signature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Opinion signature must use the ed25519: prefix.";
            return false;
        }

        try
        {
            signatureBytes = Convert.FromBase64String(signature.Substring(SignaturePrefix.Length));
        }
        catch (FormatException)
        {
            errorMessage = "Opinion signature is not valid base64.";
            return false;
        }

        if (signatureBytes.Length != 64)
        {
            errorMessage = "Opinion signature must be 64 bytes.";
            signatureBytes = null;
            return false;
        }

        return true;
    }

    private static bool TryParsePublicKey(string publicKey, out byte[]? publicKeyBytes, out string? errorMessage)
    {
        publicKeyBytes = null;
        errorMessage = null;
        publicKey = publicKey?.Trim() ?? string.Empty;

        try
        {
            publicKeyBytes = Convert.FromBase64String(publicKey);
        }
        catch (FormatException)
        {
            errorMessage = "Sender public key is not valid base64.";
            return false;
        }

        if (publicKeyBytes.Length != 32)
        {
            errorMessage = "Sender public key must be 32 bytes.";
            publicKeyBytes = null;
            return false;
        }

        return true;
    }

    private string ExtractPodIdFromKey(string dhtKey)
    {
        // Key format: pod:<PodId>:opinions:<ContentId>
        var parts = (dhtKey ?? string.Empty).Trim().Split(':');
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    private void TrackKnownContentId(string podId, string contentId)
    {
        podId = podId?.Trim() ?? string.Empty;
        contentId = contentId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(podId) || string.IsNullOrWhiteSpace(contentId))
        {
            return;
        }

        var contentIds = _knownContentIdsByPod.GetOrAdd(podId, _ => new ConcurrentDictionary<string, byte>());
        contentIds[contentId] = 0;
    }

    private IReadOnlyList<string> GetKnownContentIds(string podId)
    {
        podId = podId?.Trim() ?? string.Empty;
        if (_knownContentIdsByPod.TryGetValue(podId, out var contentIds))
        {
            return contentIds.Keys
                .Where(contentId => !string.IsNullOrWhiteSpace(contentId))
                .OrderBy(contentId => contentId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return Array.Empty<string>();
    }

    private static PodVariantOpinion NormalizeOpinion(PodVariantOpinion opinion)
    {
        opinion.ContentId = opinion.ContentId?.Trim() ?? string.Empty;
        opinion.VariantHash = opinion.VariantHash?.Trim() ?? string.Empty;
        opinion.Note = opinion.Note?.Trim() ?? string.Empty;
        opinion.SenderPeerId = opinion.SenderPeerId?.Trim() ?? string.Empty;
        opinion.Signature = opinion.Signature?.Trim() ?? string.Empty;
        return opinion;
    }
}
