// <copyright file="SmallWorldNeighborService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages semi-sticky "small-world" neighbors for faster mesh propagation.
/// 
/// Small-world networks have two properties:
/// 1. Most nodes are not direct neighbors, but can reach each other quickly (short paths)
/// 2. Each node has a few stable, long-term connections (neighbors)
/// 
/// This creates a graph where:
/// - New hashes propagate quickly via stable neighbor edges
/// - Random connections prevent network fragmentation
/// - No single point of failure
/// </summary>
public sealed class SmallWorldNeighborService
{
    private readonly ILogger<SmallWorldNeighborService> _logger;
    private readonly IMeshSyncService _meshSyncService;
    private readonly ConcurrentDictionary<string, NeighborInfo> _neighbors = new();
    private readonly ConcurrentDictionary<string, NeighborInfo> _candidates = new();
    private readonly Random _random = new();
    
    /// <summary>
    /// Target number of stable neighbors.
    /// </summary>
    public int TargetNeighborCount { get; set; } = 5;
    
    /// <summary>
    /// Maximum candidates to track for promotion.
    /// </summary>
    public int MaxCandidates { get; set; } = 20;
    
    /// <summary>
    /// Minimum successful interactions before promotion to neighbor.
    /// </summary>
    public int PromotionThreshold { get; set; } = 3;
    
    /// <summary>
    /// Maximum time without interaction before neighbor is demoted.
    /// </summary>
    public TimeSpan NeighborTimeout { get; set; } = TimeSpan.FromHours(24);
    
    /// <summary>
    /// Interval for syncing with neighbors vs random peers.
    /// </summary>
    public TimeSpan NeighborSyncInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan RandomSyncInterval { get; set; } = TimeSpan.FromHours(2);
    
    public SmallWorldNeighborService(
        ILogger<SmallWorldNeighborService> logger,
        IMeshSyncService meshSyncService)
    {
        _logger = logger;
        _meshSyncService = meshSyncService;
    }
    
    /// <summary>
    /// Get current stable neighbors.
    /// </summary>
    public IReadOnlyList<string> GetNeighbors()
    {
        return _neighbors.Keys.ToList();
    }
    
    /// <summary>
    /// Get current candidates for promotion.
    /// </summary>
    public IReadOnlyList<string> GetCandidates()
    {
        return _candidates.Keys.ToList();
    }
    
    /// <summary>
    /// Record a successful interaction with a peer.
    /// May promote candidate to neighbor.
    /// </summary>
    public void RecordInteraction(string username, InteractionType type)
    {
        var now = DateTimeOffset.UtcNow;
        
        // If already a neighbor, update their info
        if (_neighbors.TryGetValue(username, out var neighbor))
        {
            neighbor.LastInteraction = now;
            neighbor.InteractionCount++;
            neighbor.LastInteractionType = type;
            _logger.LogDebug("Updated neighbor {Username}: {Count} interactions", 
                username, neighbor.InteractionCount);
            return;
        }
        
        // Track as candidate
        var candidate = _candidates.GetOrAdd(username, _ => new NeighborInfo
        {
            Username = username,
            FirstSeen = now,
            LastInteraction = now,
            InteractionCount = 0,
        });
        
        candidate.LastInteraction = now;
        candidate.InteractionCount++;
        candidate.LastInteractionType = type;
        
        // Check for promotion
        if (candidate.InteractionCount >= PromotionThreshold && 
            _neighbors.Count < TargetNeighborCount)
        {
            PromoteToNeighbor(username, candidate);
        }
        
        // Prune old candidates
        PruneCandidates();
    }
    
    /// <summary>
    /// Record a failed interaction with a peer.
    /// May demote neighbor to candidate.
    /// </summary>
    public void RecordFailure(string username)
    {
        if (_neighbors.TryGetValue(username, out var neighbor))
        {
            neighbor.FailureCount++;
            
            // Demote after 3 consecutive failures
            if (neighbor.FailureCount >= 3)
            {
                DemoteNeighbor(username);
            }
        }
    }
    
    /// <summary>
    /// Get peers to sync with, prioritizing neighbors.
    /// </summary>
    public IReadOnlyList<string> GetPeersForSync(int count)
    {
        var result = new List<string>();
        var now = DateTimeOffset.UtcNow;
        
        // Add neighbors that are due for sync
        var neighborsForSync = _neighbors.Values
            .Where(n => now - n.LastSync > NeighborSyncInterval)
            .OrderBy(n => n.LastSync)
            .Take(count)
            .Select(n => n.Username)
            .ToList();
        
        result.AddRange(neighborsForSync);
        
        // Fill remaining slots with random candidates/known peers
        var remaining = count - result.Count;
        if (remaining > 0)
        {
            var candidatesForSync = _candidates.Values
                .Where(c => now - c.LastSync > RandomSyncInterval)
                .OrderBy(_ => _random.Next())
                .Take(remaining)
                .Select(c => c.Username);
            
            result.AddRange(candidatesForSync);
        }
        
        return result;
    }
    
    /// <summary>
    /// Mark that we synced with a peer.
    /// </summary>
    public void RecordSync(string username)
    {
        var now = DateTimeOffset.UtcNow;
        
        if (_neighbors.TryGetValue(username, out var neighbor))
        {
            neighbor.LastSync = now;
            neighbor.SyncCount++;
        }
        else if (_candidates.TryGetValue(username, out var candidate))
        {
            candidate.LastSync = now;
            candidate.SyncCount++;
        }
    }
    
    /// <summary>
    /// Get service statistics.
    /// </summary>
    public SmallWorldStats GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        
        return new SmallWorldStats
        {
            NeighborCount = _neighbors.Count,
            CandidateCount = _candidates.Count,
            TargetNeighborCount = TargetNeighborCount,
            NeighborsDueForSync = _neighbors.Values.Count(n => now - n.LastSync > NeighborSyncInterval),
            OldestNeighborAge = _neighbors.Values.Any() 
                ? now - _neighbors.Values.Min(n => n.FirstSeen) 
                : TimeSpan.Zero,
            TotalNeighborInteractions = _neighbors.Values.Sum(n => n.InteractionCount),
            TotalNeighborSyncs = _neighbors.Values.Sum(n => n.SyncCount),
        };
    }
    
    /// <summary>
    /// Run maintenance: prune stale neighbors and candidates.
    /// </summary>
    public void RunMaintenance()
    {
        var now = DateTimeOffset.UtcNow;
        
        // Demote stale neighbors
        var staleNeighbors = _neighbors.Values
            .Where(n => now - n.LastInteraction > NeighborTimeout)
            .Select(n => n.Username)
            .ToList();
        
        foreach (var username in staleNeighbors)
        {
            _logger.LogInformation("Demoting stale neighbor {Username}", username);
            DemoteNeighbor(username);
        }
        
        // Prune old candidates
        PruneCandidates();
        
        // Try to fill neighbor slots from candidates
        while (_neighbors.Count < TargetNeighborCount)
        {
            var bestCandidate = _candidates.Values
                .OrderByDescending(c => c.InteractionCount)
                .FirstOrDefault(c => c.InteractionCount >= PromotionThreshold);
            
            if (bestCandidate is null)
            {
                break;
            }
            
            PromoteToNeighbor(bestCandidate.Username, bestCandidate);
        }
    }
    
    /// <summary>
    /// Force add a neighbor (e.g., from saved state).
    /// </summary>
    public void AddNeighbor(string username)
    {
        if (_neighbors.ContainsKey(username))
        {
            return;
        }
        
        var now = DateTimeOffset.UtcNow;
        _neighbors[username] = new NeighborInfo
        {
            Username = username,
            FirstSeen = now,
            LastInteraction = now,
            InteractionCount = PromotionThreshold, // Assume valid
        };
        
        _candidates.TryRemove(username, out _);
        _logger.LogInformation("Added neighbor {Username}", username);
    }
    
    /// <summary>
    /// Force remove a neighbor.
    /// </summary>
    public void RemoveNeighbor(string username)
    {
        if (_neighbors.TryRemove(username, out _))
        {
            _logger.LogInformation("Removed neighbor {Username}", username);
        }
    }
    
    private void PromoteToNeighbor(string username, NeighborInfo info)
    {
        if (_neighbors.Count >= TargetNeighborCount * 2)
        {
            // Don't exceed 2x target
            return;
        }
        
        _neighbors[username] = info;
        _candidates.TryRemove(username, out _);
        
        _logger.LogInformation("Promoted {Username} to neighbor after {Count} interactions",
            username, info.InteractionCount);
    }
    
    private void DemoteNeighbor(string username)
    {
        if (_neighbors.TryRemove(username, out var info))
        {
            // Keep as candidate for potential re-promotion
            info.FailureCount = 0;
            _candidates[username] = info;
            
            _logger.LogInformation("Demoted neighbor {Username} to candidate", username);
        }
    }
    
    private void PruneCandidates()
    {
        // Remove oldest candidates if over limit
        while (_candidates.Count > MaxCandidates)
        {
            var oldest = _candidates.Values
                .OrderBy(c => c.LastInteraction)
                .FirstOrDefault();
            
            if (oldest is not null)
            {
                _candidates.TryRemove(oldest.Username, out _);
            }
            else
            {
                break;
            }
        }
    }
    
    private sealed class NeighborInfo
    {
        public required string Username { get; init; }
        public required DateTimeOffset FirstSeen { get; init; }
        public DateTimeOffset LastInteraction { get; set; }
        public DateTimeOffset LastSync { get; set; }
        public int InteractionCount { get; set; }
        public int SyncCount { get; set; }
        public int FailureCount { get; set; }
        public InteractionType LastInteractionType { get; set; }
    }
}

/// <summary>
/// Type of interaction with a peer.
/// </summary>
public enum InteractionType
{
    /// <summary>Received mesh sync from peer.</summary>
    MeshSync,
    
    /// <summary>Downloaded from peer.</summary>
    Download,
    
    /// <summary>Uploaded to peer.</summary>
    Upload,
    
    /// <summary>Browsed peer's shares.</summary>
    Browse,
    
    /// <summary>Chat message.</summary>
    Chat,
    
    /// <summary>Overlay connection established.</summary>
    OverlayConnect,
}

/// <summary>
/// Small-world network statistics.
/// </summary>
public sealed class SmallWorldStats
{
    public int NeighborCount { get; init; }
    public int CandidateCount { get; init; }
    public int TargetNeighborCount { get; init; }
    public int NeighborsDueForSync { get; init; }
    public TimeSpan OldestNeighborAge { get; init; }
    public long TotalNeighborInteractions { get; init; }
    public long TotalNeighborSyncs { get; init; }
}


