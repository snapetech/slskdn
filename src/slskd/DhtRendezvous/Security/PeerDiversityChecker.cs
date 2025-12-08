// <copyright file="PeerDiversityChecker.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;

/// <summary>
/// Ensures mesh peers come from diverse network locations to prevent eclipse attacks.
/// An eclipse attack occurs when an attacker controls all of a victim's peers,
/// allowing them to feed false information.
/// </summary>
public sealed class PeerDiversityChecker
{
    private readonly ConcurrentDictionary<string, PeerNetworkInfo> _peerInfo = new();
    
    /// <summary>
    /// Maximum peers allowed from the same /16 subnet.
    /// </summary>
    public int MaxPeersPerSubnet16 { get; set; } = 3;
    
    /// <summary>
    /// Maximum peers allowed from the same /24 subnet.
    /// </summary>
    public int MaxPeersPerSubnet24 { get; set; } = 2;
    
    /// <summary>
    /// Minimum different /16 subnets required for a healthy mesh.
    /// </summary>
    public int MinSubnet16Diversity { get; set; } = 3;
    
    /// <summary>
    /// Check if adding a new peer would violate diversity rules.
    /// </summary>
    /// <param name="endpoint">The peer's IP endpoint.</param>
    /// <param name="reason">Rejection reason if not allowed.</param>
    /// <returns>True if the peer can be added.</returns>
    public bool CanAddPeer(IPEndPoint endpoint, out string? reason)
    {
        reason = null;
        
        if (endpoint.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            // For now, only check IPv4 - IPv6 diversity is more complex
            return true;
        }
        
        var bytes = endpoint.Address.GetAddressBytes();
        var subnet16 = $"{bytes[0]}.{bytes[1]}";
        var subnet24 = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
        
        // Count existing peers in same subnets
        var sameSubnet16 = 0;
        var sameSubnet24 = 0;
        
        foreach (var kvp in _peerInfo)
        {
            if (kvp.Value.Subnet16 == subnet16)
            {
                sameSubnet16++;
            }
            
            if (kvp.Value.Subnet24 == subnet24)
            {
                sameSubnet24++;
            }
        }
        
        if (sameSubnet24 >= MaxPeersPerSubnet24)
        {
            reason = $"Too many peers from subnet {subnet24}.0/24 ({sameSubnet24}/{MaxPeersPerSubnet24})";
            return false;
        }
        
        if (sameSubnet16 >= MaxPeersPerSubnet16)
        {
            reason = $"Too many peers from subnet {subnet16}.0.0/16 ({sameSubnet16}/{MaxPeersPerSubnet16})";
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Register a peer that was successfully added.
    /// </summary>
    public void RegisterPeer(string peerId, IPEndPoint endpoint)
    {
        if (endpoint.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return;
        }
        
        var bytes = endpoint.Address.GetAddressBytes();
        _peerInfo[peerId] = new PeerNetworkInfo
        {
            Endpoint = endpoint,
            Subnet16 = $"{bytes[0]}.{bytes[1]}",
            Subnet24 = $"{bytes[0]}.{bytes[1]}.{bytes[2]}",
            AddedAt = DateTimeOffset.UtcNow,
        };
    }
    
    /// <summary>
    /// Unregister a peer that was removed.
    /// </summary>
    public void UnregisterPeer(string peerId)
    {
        _peerInfo.TryRemove(peerId, out _);
    }
    
    /// <summary>
    /// Check if the current peer set has healthy diversity.
    /// </summary>
    public DiversityStatus GetDiversityStatus()
    {
        var uniqueSubnet16 = _peerInfo.Values.Select(p => p.Subnet16).Distinct().Count();
        var uniqueSubnet24 = _peerInfo.Values.Select(p => p.Subnet24).Distinct().Count();
        
        var subnet16Counts = _peerInfo.Values
            .GroupBy(p => p.Subnet16)
            .ToDictionary(g => g.Key, g => g.Count());
        
        var subnet24Counts = _peerInfo.Values
            .GroupBy(p => p.Subnet24)
            .ToDictionary(g => g.Key, g => g.Count());
        
        var maxInSubnet16 = subnet16Counts.Values.DefaultIfEmpty(0).Max();
        var maxInSubnet24 = subnet24Counts.Values.DefaultIfEmpty(0).Max();
        
        var isHealthy = uniqueSubnet16 >= MinSubnet16Diversity || _peerInfo.Count < MinSubnet16Diversity;
        var warnings = new List<string>();
        
        if (uniqueSubnet16 < MinSubnet16Diversity && _peerInfo.Count >= MinSubnet16Diversity)
        {
            warnings.Add($"Low /16 subnet diversity: {uniqueSubnet16}/{MinSubnet16Diversity} required");
        }
        
        if (maxInSubnet24 > 1)
        {
            var worstSubnet = subnet24Counts.OrderByDescending(kv => kv.Value).First();
            warnings.Add($"Subnet concentration: {worstSubnet.Value} peers in {worstSubnet.Key}.0/24");
        }
        
        return new DiversityStatus
        {
            TotalPeers = _peerInfo.Count,
            UniqueSubnet16Count = uniqueSubnet16,
            UniqueSubnet24Count = uniqueSubnet24,
            MaxPeersInSingleSubnet16 = maxInSubnet16,
            MaxPeersInSingleSubnet24 = maxInSubnet24,
            IsHealthy = isHealthy,
            Warnings = warnings,
            Subnet16Distribution = subnet16Counts,
        };
    }
    
    /// <summary>
    /// Score a candidate peer based on how much it would improve diversity.
    /// Higher scores are better.
    /// </summary>
    public int ScoreCandidateDiversity(IPEndPoint endpoint)
    {
        if (endpoint.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return 50; // Neutral score for non-IPv4
        }
        
        var bytes = endpoint.Address.GetAddressBytes();
        var subnet16 = $"{bytes[0]}.{bytes[1]}";
        var subnet24 = $"{bytes[0]}.{bytes[1]}.{bytes[2]}";
        
        var existingInSubnet16 = _peerInfo.Values.Count(p => p.Subnet16 == subnet16);
        var existingInSubnet24 = _peerInfo.Values.Count(p => p.Subnet24 == subnet24);
        
        // Score: prefer peers from new subnets
        var score = 100;
        
        // Penalize if we already have peers in same /24
        score -= existingInSubnet24 * 30;
        
        // Penalize if we already have peers in same /16
        score -= existingInSubnet16 * 10;
        
        // Bonus if this is a completely new /16
        if (existingInSubnet16 == 0)
        {
            score += 20;
        }
        
        return Math.Max(0, score);
    }
    
    /// <summary>
    /// Clear all peer tracking.
    /// </summary>
    public void Clear()
    {
        _peerInfo.Clear();
    }
    
    private sealed class PeerNetworkInfo
    {
        public required IPEndPoint Endpoint { get; init; }
        public required string Subnet16 { get; init; }
        public required string Subnet24 { get; init; }
        public required DateTimeOffset AddedAt { get; init; }
    }
}

/// <summary>
/// Status of peer network diversity.
/// </summary>
public sealed class DiversityStatus
{
    public int TotalPeers { get; init; }
    public int UniqueSubnet16Count { get; init; }
    public int UniqueSubnet24Count { get; init; }
    public int MaxPeersInSingleSubnet16 { get; init; }
    public int MaxPeersInSingleSubnet24 { get; init; }
    public bool IsHealthy { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, int> Subnet16Distribution { get; init; } = new Dictionary<string, int>();
}


