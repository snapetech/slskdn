// <copyright file="SceneMembershipTracker.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Scenes;

using slskd.VirtualSoulfind.ShadowIndex;
using System.Collections.Concurrent;
using MessagePack;

/// <summary>
/// Interface for scene membership tracking.
/// </summary>
public interface ISceneMembershipTracker
{
    /// <summary>
    /// Get scene metadata from DHT.
    /// </summary>
    Task<SceneMetadata?> GetSceneMetadataAsync(string sceneId, CancellationToken ct = default);

    /// <summary>
    /// Get active members of a scene from DHT.
    /// </summary>
    Task<List<SceneMember>> GetMembersAsync(string sceneId, CancellationToken ct = default);

    /// <summary>
    /// Track a peer joining a scene (local cache).
    /// </summary>
    Task TrackJoinAsync(string sceneId, string peerId, CancellationToken ct = default);

    /// <summary>
    /// Track a peer leaving a scene (local cache).
    /// </summary>
    Task TrackLeaveAsync(string sceneId, string peerId, CancellationToken ct = default);
}

/// <summary>
/// Tracks scene membership via DHT queries and local cache.
/// </summary>
public class SceneMembershipTracker : ISceneMembershipTracker
{
    private readonly ILogger<SceneMembershipTracker> logger;
    private readonly IDhtClient dht;
    private readonly ConcurrentDictionary<string, SceneMetadata> metadataCache = new();
    private readonly ConcurrentDictionary<string, List<SceneMember>> memberCache = new();

    public SceneMembershipTracker(
        ILogger<SceneMembershipTracker> logger,
        IDhtClient dht)
    {
        this.logger = logger;
        this.dht = dht;
    }

    public async Task<SceneMetadata?> GetSceneMetadataAsync(string sceneId, CancellationToken ct)
    {
        // Check cache first
        if (metadataCache.TryGetValue(sceneId, out var cached))
        {
            var age = DateTimeOffset.UtcNow - cached.LastUpdatedAt;
            if (memberCache.TryGetValue(sceneId, out var cachedMembers))
            {
                lock (cachedMembers)
                {
                    cached.ApproximateMemberCount = cachedMembers.Count(member => member.IsActive);
                }
            }

            if (age < TimeSpan.FromMinutes(5))
            {
                logger.LogDebug("[VSF-SCENE-TRACK] Metadata cache hit for {SceneId}", sceneId);
                return cached;
            }
        }

        // Query DHT for scene metadata
        logger.LogDebug("[VSF-SCENE-TRACK] Querying DHT for scene metadata: {SceneId}", sceneId);

        var key = DhtKeyDerivation.DeriveSceneKey(sceneId);
        var data = await dht.GetAsync(key, ct);

        if (data == null)
        {
            if (memberCache.TryGetValue(sceneId, out var cachedMembers))
            {
                int activeCount;
                lock (cachedMembers)
                {
                    activeCount = cachedMembers.Count(member => member.IsActive);
                }

                if (activeCount > 0)
                {
                    logger.LogDebug("[VSF-SCENE-TRACK] No DHT metadata for {SceneId}, synthesizing from local membership cache", sceneId);
                    var synthesized = CreateFallbackMetadata(sceneId, activeCount);
                    metadataCache[sceneId] = synthesized;
                    return synthesized;
                }
            }

            logger.LogDebug("[VSF-SCENE-TRACK] No metadata found for scene {SceneId}", sceneId);
            return null;
        }

        // Parse metadata (Phase 6C: T-815 - MessagePack deserialization)
        SceneMetadata metadata;
        try
        {
            metadata = MessagePack.MessagePackSerializer.Deserialize<SceneMetadata>(data, cancellationToken: ct);
            if (metadata == null)
            {
                throw new InvalidOperationException("Deserialized metadata is null");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-SCENE-TRACK] Failed to deserialize scene metadata, using defaults");

            // Fallback: create metadata from scene ID
            metadata = CreateFallbackMetadata(sceneId, 0);
        }

        // Get member count from members list
        var members = await GetMembersAsync(sceneId, ct);
        metadata.ApproximateMemberCount = members.Count;

        // Cache metadata
        metadataCache[sceneId] = metadata;

        return metadata;
    }

    public async Task<List<SceneMember>> GetMembersAsync(string sceneId, CancellationToken ct)
    {
        // Check cache first
        if (memberCache.TryGetValue(sceneId, out var cached))
        {
            logger.LogDebug("[VSF-SCENE-TRACK] Member cache hit for {SceneId}", sceneId);
            lock (cached)
            {
                return cached
                    .Where(member => member.IsActive)
                    .ToList();
            }
        }

        // Query DHT for scene members
        logger.LogDebug("[VSF-SCENE-TRACK] Querying DHT for scene members: {SceneId}", sceneId);

        var key = DhtKeyDerivation.DeriveSceneMembersKey(sceneId);
        var memberDataList = await dht.GetMultipleAsync(key, ct);

        var members = new List<SceneMember>();

        foreach (var data in memberDataList)
        {
            var member = ParseMemberAnnouncement(sceneId, data);
            if (member != null && member.IsActive)
            {
                members.Add(member);
            }
        }

        // Cache members
        var normalizedMembers = members
            .GroupBy(member => member.PeerId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(member => member.LastSeenAt).First())
            .OrderByDescending(member => member.LastSeenAt)
            .ToList();
        memberCache[sceneId] = normalizedMembers;

        if (metadataCache.TryGetValue(sceneId, out var cachedMetadata))
        {
            cachedMetadata.ApproximateMemberCount = normalizedMembers.Count;
            cachedMetadata.LastUpdatedAt = DateTimeOffset.UtcNow;
        }

        logger.LogInformation("[VSF-SCENE-TRACK] Found {Count} active members in scene {SceneId}",
            normalizedMembers.Count, sceneId);

        return normalizedMembers;
    }

    public Task TrackJoinAsync(string sceneId, string peerId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-TRACK] Tracking join: {PeerId} → {SceneId}", peerId, sceneId);

        var members = memberCache.GetOrAdd(sceneId, _ => new List<SceneMember>());
        lock (members)
        {
            var existing = members.FirstOrDefault(m => m.PeerId == peerId);
            if (existing != null)
            {
                existing.LastSeenAt = DateTimeOffset.UtcNow;
                existing.IsActive = true;
            }
            else
            {
                members.Add(new SceneMember
                {
                    SceneId = sceneId,
                    PeerId = peerId,
                    JoinedAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow,
                    IsActive = true
                });
            }
        }

        RefreshCachedMetadata(sceneId, members);

        return Task.CompletedTask;
    }

    public Task TrackLeaveAsync(string sceneId, string peerId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-TRACK] Tracking leave: {PeerId} → {SceneId}", peerId, sceneId);

        if (memberCache.TryGetValue(sceneId, out var members))
        {
            lock (members)
            {
                var existing = members.FirstOrDefault(m => m.PeerId == peerId);
                if (existing != null)
                {
                    existing.IsActive = false;
                    existing.LastSeenAt = DateTimeOffset.UtcNow;
                }
            }

            RefreshCachedMetadata(sceneId, members);
        }

        return Task.CompletedTask;
    }

    private SceneType ParseSceneType(string sceneId)
    {
        if (sceneId.StartsWith("scene:label:"))
            return SceneType.Label;
        if (sceneId.StartsWith("scene:genre:"))
            return SceneType.Genre;
        if (sceneId.StartsWith("scene:key:"))
            return SceneType.Private;

        return SceneType.Genre; // Default
    }

    private SceneMember? ParseMemberAnnouncement(string sceneId, byte[] data)
    {
        if (data.Length < 11)
            return null;

        var isJoin = data[0] == 1;
        var timestamp = BitConverter.ToInt64(data, 1);
        if (timestamp <= 0)
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        var peerId = ParsePeerId(data);
        if (string.IsNullOrWhiteSpace(peerId))
        {
            return null;
        }

        return new SceneMember
        {
            SceneId = sceneId,
            PeerId = peerId,
            JoinedAt = DateTimeOffset.FromUnixTimeSeconds(timestamp),
            LastSeenAt = DateTimeOffset.FromUnixTimeSeconds(timestamp),
            IsActive = isJoin
        };
    }

    private static string? ParsePeerId(byte[] data)
    {
        if (data.Length >= 11)
        {
            var peerIdLength = BitConverter.ToUInt16(data, 9);
            if (peerIdLength > 0 && data.Length >= 11 + peerIdLength)
            {
                var peerId = System.Text.Encoding.UTF8.GetString(data, 11, peerIdLength).Trim();
                return string.IsNullOrWhiteSpace(peerId) ? null : peerId;
            }
        }

        if (data.Length >= 17)
            return null;

        return null;
    }

    private SceneMetadata CreateFallbackMetadata(string sceneId, int approximateMemberCount)
    {
        return new SceneMetadata
        {
            SceneId = sceneId,
            DisplayName = sceneId.Split(':').LastOrDefault() ?? sceneId,
            Type = ParseSceneType(sceneId),
            ApproximateMemberCount = approximateMemberCount,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private void RefreshCachedMetadata(string sceneId, List<SceneMember> members)
    {
        // All callers (TrackJoinAsync, TrackLeaveAsync) lock on members before mutating it,
        // but release the lock before calling here. Lock for the read too to prevent a
        // concurrent Add/Remove from racing with the LINQ iteration.
        int activeCount;
        lock (members)
        {
            activeCount = members.Count(member => member.IsActive);
        }

        metadataCache.AddOrUpdate(
            sceneId,
            _ => CreateFallbackMetadata(sceneId, activeCount),
            (_, existing) =>
            {
                existing.ApproximateMemberCount = activeCount;
                existing.LastUpdatedAt = DateTimeOffset.UtcNow;
                return existing;
            });
    }
}
