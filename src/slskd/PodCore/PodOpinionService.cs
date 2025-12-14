// <copyright file="PodOpinionService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.MediaCore;
using slskd.Mesh.Dht;

/// <summary>
///     Service for managing pod member opinions on content variants.
/// </summary>
public class PodOpinionService : IPodOpinionService
{
    private readonly IPodService _podService;
    private readonly IMeshDhtClient _dhtClient;
    private readonly IMessageSigner _messageSigner;
    private readonly ILogger<PodOpinionService> _logger;

    // Cache for opinions (podId -> contentId -> opinions)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, List<PodVariantOpinion>>> _opinionCache = new();

    // Statistics tracking
    private long _totalOpinionsPublished;
    private long _totalOpinionsRetrieved;
    private readonly ConcurrentDictionary<string, long> _opinionsByPod = new();

    public PodOpinionService(
        IPodService podService,
        IMeshDhtClient dhtClient,
        IMessageSigner messageSigner,
        ILogger<PodOpinionService> logger)
    {
        _podService = podService;
        _dhtClient = dhtClient;
        _messageSigner = messageSigner;
        _logger = logger;
    }

    public async Task<OpinionPublishResult> PublishOpinionAsync(string podId, PodVariantOpinion opinion, CancellationToken ct = default)
    {
        try
        {
            // Validate the opinion first
            var validation = await ValidateOpinionAsync(podId, opinion, ct);
            if (!validation.IsValid)
            {
                return new OpinionPublishResult(
                    false, podId, opinion.ContentId, opinion.VariantHash, validation.ErrorMessage);
            }

            // Ensure opinion is signed
            if (string.IsNullOrWhiteSpace(opinion.Signature))
            {
                opinion = await SignOpinionAsync(opinion, ct);
            }

            // Create DHT key: pod:<PodId>:opinions:<ContentId>
            var dhtKey = $"pod:{podId}:opinions:{opinion.ContentId}";

            // Get existing opinions for this content
            var existingOpinions = await GetOpinionsFromDhtAsync(dhtKey, ct);
            existingOpinions.Add(opinion);

            // Store updated opinions list in DHT
            var jsonData = System.Text.Json.JsonSerializer.Serialize(existingOpinions);
            await _dhtClient.PutAsync(dhtKey, jsonData, ttlSeconds: 3600, ct); // 1 hour TTL

            // Update local cache
            var podCache = _opinionCache.GetOrAdd(podId, _ => new ConcurrentDictionary<string, List<PodVariantOpinion>>());
            var contentOpinions = podCache.GetOrAdd(opinion.ContentId, _ => new List<PodVariantOpinion>());
            contentOpinions.Add(opinion);

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
                false, podId, opinion.ContentId, opinion.VariantHash, ex.Message);
        }
    }

    public async Task<IReadOnlyList<PodVariantOpinion>> GetOpinionsAsync(string podId, string contentId, CancellationToken ct = default)
    {
        // Check cache first
        if (_opinionCache.TryGetValue(podId, out var podCache) &&
            podCache.TryGetValue(contentId, out var cachedOpinions))
        {
            Interlocked.Add(ref _totalOpinionsRetrieved, cachedOpinions.Count);
            return cachedOpinions.AsReadOnly();
        }

        // Fetch from DHT
        var dhtKey = $"pod:{podId}:opinions:{contentId}";
        var opinions = await GetOpinionsFromDhtAsync(dhtKey, ct);

        // Update cache
        var contentCache = _opinionCache.GetOrAdd(podId, _ => new ConcurrentDictionary<string, List<PodVariantOpinion>>());
        contentCache[contentId] = opinions.ToList();

        Interlocked.Add(ref _totalOpinionsRetrieved, opinions.Count);
        return opinions;
    }

    public async Task<IReadOnlyList<PodVariantOpinion>> GetVariantOpinionsAsync(string podId, string contentId, string variantHash, CancellationToken ct = default)
    {
        var allOpinions = await GetOpinionsAsync(podId, contentId, ct);
        return allOpinions.Where(o => o.VariantHash == variantHash).ToList();
    }

    public async Task<OpinionValidationResult> ValidateOpinionAsync(string podId, PodVariantOpinion opinion, CancellationToken ct = default)
    {
        // Validate pod exists
        var pod = await _podService.GetPodAsync(podId, ct);
        if (pod == null)
        {
            return new OpinionValidationResult(false, $"Pod {podId} does not exist");
        }

        // Validate sender is pod member
        var members = await _podService.GetMembersAsync(podId, ct);
        var isMember = members.Any(m => m.PeerId == opinion.SenderPeerId && !m.IsBanned);
        if (!isMember)
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

        // If signature exists, validate it
        if (!string.IsNullOrWhiteSpace(opinion.Signature))
        {
            // Create a PodMessage equivalent for signature verification
            var messageForVerification = new PodMessage
            {
                MessageId = Guid.NewGuid().ToString(),
                ChannelId = "opinion",
                SenderPeerId = opinion.SenderPeerId,
                Body = $"{opinion.ContentId}:{opinion.VariantHash}:{opinion.Score:F2}:{opinion.Note}",
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Signature = opinion.Signature
            };

            var signatureValid = await _messageSigner.VerifyMessageAsync(messageForVerification, ct);
            if (!signatureValid)
            {
                return new OpinionValidationResult(false, "Invalid signature");
            }
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
            // Clear cache for this pod
            _opinionCache.TryRemove(podId, out _);

            // Get all content IDs that this pod has opinions about
            // This is a simplified approach - in practice, we'd need to track content IDs
            var contentIds = new[] { "placeholder" }; // TODO: Implement content ID discovery

            foreach (var contentId in contentIds)
            {
                var dhtKey = $"pod:{podId}:opinions:{contentId}";
                var opinions = await GetOpinionsFromDhtAsync(dhtKey, ct);

                if (opinions.Any())
                {
                    var cache = _opinionCache.GetOrAdd(podId, _ => new ConcurrentDictionary<string, List<PodVariantOpinion>>());
                    cache[contentId] = opinions.ToList();
                    refreshedCount += opinions.Count;
                }
            }

            return new OpinionRefreshResult(
                true, podId, refreshedCount, newOpinionsCount, stopwatch.Elapsed);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing opinions for pod {PodId}", podId);
            return new OpinionRefreshResult(
                false, podId, refreshedCount, newOpinionsCount, stopwatch.Elapsed, ex.Message);
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

    private async Task<PodVariantOpinion> SignOpinionAsync(PodVariantOpinion opinion, CancellationToken ct)
    {
        // For now, we'll generate a simple signature. In a real implementation,
        // we'd need to get the user's private key and use proper signing
        // TODO: Implement proper opinion signing with user keys
        var signableData = $"{opinion.ContentId}:{opinion.VariantHash}:{opinion.Score:F2}:{opinion.Note}:{opinion.SenderPeerId}";
        var signature = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(signableData));

        return new PodVariantOpinion
        {
            ContentId = opinion.ContentId,
            VariantHash = opinion.VariantHash,
            Score = opinion.Score,
            Note = opinion.Note,
            SenderPeerId = opinion.SenderPeerId,
            Signature = signature
        };
    }

    private string ExtractPodIdFromKey(string dhtKey)
    {
        // Key format: pod:<PodId>:opinions:<ContentId>
        var parts = dhtKey.Split(':');
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }
}
