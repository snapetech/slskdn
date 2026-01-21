// <copyright file="DhtRendezvousController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.API;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.DhtRendezvous.Security;

/// <summary>
/// Controller for DHT rendezvous and overlay network operations.
/// </summary>
[ApiController]
[ApiVersion("0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class DhtRendezvousController : ControllerBase
{
    private readonly IDhtRendezvousService _dhtService;
    private readonly IMeshOverlayServer _overlayServer;
    private readonly IMeshOverlayConnector _overlayConnector;
    private readonly MeshNeighborRegistry _neighborRegistry;
    private readonly OverlayRateLimiter _rateLimiter;
    private readonly OverlayBlocklist _blocklist;
    
    public DhtRendezvousController(
        IDhtRendezvousService dhtService,
        IMeshOverlayServer overlayServer,
        IMeshOverlayConnector overlayConnector,
        MeshNeighborRegistry neighborRegistry,
        OverlayRateLimiter rateLimiter,
        OverlayBlocklist blocklist)
    {
        _dhtService = dhtService;
        _overlayServer = overlayServer;
        _overlayConnector = overlayConnector;
        _neighborRegistry = neighborRegistry;
        _rateLimiter = rateLimiter;
        _blocklist = blocklist;
    }
    
    /// <summary>
    /// Get DHT rendezvous service status.
    /// </summary>
    [HttpGet("dht/status")]
    public ActionResult<DhtStatusResponse> GetDhtStatus()
    {
        var stats = _dhtService.GetStats();
        
        return Ok(new DhtStatusResponse
        {
            IsEnabled = stats.IsDhtRunning,
            IsBeaconCapable = stats.IsBeaconCapable,
            IsDhtRunning = stats.IsDhtRunning,
            DhtNodeCount = stats.DhtNodeCount,
            DiscoveredPeerCount = stats.DiscoveredPeerCount,
            ActiveMeshConnections = stats.ActiveMeshConnections,
            VerifiedBeaconCount = stats.VerifiedBeaconCount,
            TotalPeersDiscovered = stats.TotalPeersDiscovered,
            TotalConnectionsAttempted = stats.TotalConnectionsAttempted,
            TotalConnectionsSucceeded = stats.TotalConnectionsSucceeded,
            LastAnnounceTime = stats.LastAnnounceTime,
            LastDiscoveryTime = stats.LastDiscoveryTime,
            StartedAt = stats.StartedAt,
            UptimeSeconds = stats.StartedAt.HasValue
                ? (long)(DateTimeOffset.UtcNow - stats.StartedAt.Value).TotalSeconds
                : 0,
            RendezvousInfohashes = stats.RendezvousInfohashes,
        });
    }
    
    /// <summary>
    /// Get discovered peer endpoints.
    /// </summary>
    [HttpGet("dht/peers")]
    public ActionResult<IEnumerable<DiscoveredPeerResponse>> GetDiscoveredPeers()
    {
        var peers = _dhtService.GetDiscoveredPeers()
            .Select(ep => new DiscoveredPeerResponse
            {
                Address = ep.Address.ToString(),
                Port = ep.Port,
            });
        
        return Ok(peers);
    }
    
    /// <summary>
    /// Force a DHT announcement (beacon mode only).
    /// </summary>
    [HttpPost("dht/announce")]
    public async Task<ActionResult> Announce(CancellationToken cancellationToken)
    {
        if (!_dhtService.IsBeaconCapable)
        {
            return BadRequest(new { error = "Not beacon capable" });
        }
        
        await _dhtService.AnnounceAsync(cancellationToken);
        return Ok(new { message = "Announced" });
    }
    
    /// <summary>
    /// Force a DHT discovery cycle.
    /// </summary>
    [HttpPost("dht/discover")]
    public async Task<ActionResult<DiscoveryResultResponse>> Discover(CancellationToken cancellationToken)
    {
        var newConnections = await _dhtService.DiscoverPeersAsync(cancellationToken);
        
        return Ok(new DiscoveryResultResponse
        {
            NewConnectionsMade = newConnections,
            TotalMeshConnections = _dhtService.ActiveMeshConnections,
        });
    }
    
    /// <summary>
    /// Get active overlay connections.
    /// </summary>
    [HttpGet("overlay/connections")]
    public ActionResult<IEnumerable<MeshPeerInfoResponse>> GetOverlayConnections()
    {
        var peers = _dhtService.GetMeshPeers()
            .Select(p => new MeshPeerInfoResponse
            {
                Username = p.Username,
                Address = p.Endpoint.Address.ToString(),
                Port = p.Endpoint.Port,
                Features = p.Features.ToList(),
                ConnectedAt = p.ConnectedAt,
                LastActivity = p.LastActivity,
                CertificateThumbprint = p.CertificateThumbprint?[..16] + "...", // Truncate for display
            });
        
        return Ok(peers);
    }
    
    /// <summary>
    /// Get overlay network statistics.
    /// </summary>
    [HttpGet("overlay/stats")]
    public ActionResult<OverlayStatsResponse> GetOverlayStats()
    {
        var serverStats = _overlayServer.GetStats();
        var connectorStats = _overlayConnector.GetStats();
        var rateLimiterStats = _rateLimiter.GetStats();
        var blocklistStats = _blocklist.GetStats();
        
        return Ok(new OverlayStatsResponse
        {
            Server = new ServerStatsResponse
            {
                IsListening = serverStats.IsListening,
                ListenPort = serverStats.ListenPort,
                ActiveConnections = serverStats.ActiveConnections,
                TotalConnectionsAccepted = serverStats.TotalConnectionsAccepted,
                TotalConnectionsRejected = serverStats.TotalConnectionsRejected,
                UptimeSeconds = (long)serverStats.Uptime.TotalSeconds,
            },
            Connector = new ConnectorStatsResponse
            {
                PendingConnections = connectorStats.PendingConnections,
                SuccessfulConnections = connectorStats.SuccessfulConnections,
                FailedConnections = connectorStats.FailedConnections,
                SuccessRate = connectorStats.SuccessRate,
            },
            RateLimiter = new RateLimiterStatsResponse
            {
                TotalConnections = rateLimiterStats.TotalConnections,
                ConnectionsLastMinute = rateLimiterStats.ConnectionsLastMinute,
                TrackedIps = rateLimiterStats.TrackedIps,
                TrackedConnections = rateLimiterStats.TrackedConnections,
                BlockedIps = rateLimiterStats.BlockedIps,
            },
            Blocklist = new BlocklistStatsResponse
            {
                BlockedIpCount = blocklistStats.BlockedIpCount,
                BlockedUsernameCount = blocklistStats.BlockedUsernameCount,
                PermanentIpBans = blocklistStats.PermanentIpBans,
                PermanentUsernameBans = blocklistStats.PermanentUsernameBans,
            },
        });
    }
    
    /// <summary>
    /// Get blocked IPs and usernames.
    /// </summary>
    [HttpGet("overlay/blocklist")]
    public ActionResult<BlocklistResponse> GetBlocklist()
    {
        var blockedIps = _blocklist.GetBlockedIps()
            .Select(kvp => new BlockedEntryResponse
            {
                Target = kvp.Key.ToString(),
                Type = "ip",
                Reason = kvp.Value.Reason,
                BlockedAt = kvp.Value.BlockedAt,
                ExpiresAt = kvp.Value.ExpiresAt,
                IsPermanent = kvp.Value.IsPermanent,
            });
        
        var blockedUsernames = _blocklist.GetBlockedUsernames()
            .Select(kvp => new BlockedEntryResponse
            {
                Target = kvp.Key,
                Type = "username",
                Reason = kvp.Value.Reason,
                BlockedAt = kvp.Value.BlockedAt,
                ExpiresAt = kvp.Value.ExpiresAt,
                IsPermanent = kvp.Value.IsPermanent,
            });
        
        return Ok(new BlocklistResponse
        {
            Entries = blockedIps.Concat(blockedUsernames).ToList(),
        });
    }
    
    /// <summary>
    /// Add an IP to the blocklist.
    /// </summary>
    [HttpPost("overlay/blocklist/ip")]
    public ActionResult BlockIp([FromBody] BlockIpRequest request)
    {
        if (!System.Net.IPAddress.TryParse(request.Ip, out var ip))
        {
            return BadRequest(new { error = "Invalid IP address" });
        }
        
        var duration = request.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
            : (TimeSpan?)null;
        
        _blocklist.BlockIp(ip, request.Reason ?? "Manual block", duration, request.Permanent);
        
        return Ok(new { message = $"Blocked IP {request.Ip}" });
    }
    
    /// <summary>
    /// Add a username to the blocklist.
    /// </summary>
    [HttpPost("overlay/blocklist/username")]
    public ActionResult BlockUsername([FromBody] BlockUsernameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new { error = "Username required" });
        }
        
        var duration = request.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
            : (TimeSpan?)null;
        
        _blocklist.BlockUsername(request.Username, request.Reason ?? "Manual block", duration, request.Permanent);
        
        return Ok(new { message = $"Blocked username {request.Username}" });
    }
    
    /// <summary>
    /// Remove an entry from the blocklist.
    /// </summary>
    [HttpDelete("overlay/blocklist/{type}/{target}")]
    public ActionResult Unblock(string type, string target)
    {
        bool removed;
        
        if (type.Equals("ip", StringComparison.OrdinalIgnoreCase))
        {
            if (!System.Net.IPAddress.TryParse(target, out var ip))
            {
                return BadRequest(new { error = "Invalid IP address" });
            }
            
            removed = _blocklist.UnblockIp(ip);
        }
        else if (type.Equals("username", StringComparison.OrdinalIgnoreCase))
        {
            removed = _blocklist.UnblockUsername(target);
        }
        else
        {
            return BadRequest(new { error = "Type must be 'ip' or 'username'" });
        }
        
        if (!removed)
        {
            return NotFound(new { error = $"{type} {target} not found in blocklist" });
        }
        
        return Ok(new { message = $"Unblocked {type} {target}" });
    }
}

// Response DTOs
public sealed class DhtStatusResponse
{
    public bool IsEnabled { get; init; }
    public bool IsBeaconCapable { get; init; }
    public bool IsDhtRunning { get; init; }
    public int DhtNodeCount { get; init; }
    public int DiscoveredPeerCount { get; init; }
    public int ActiveMeshConnections { get; init; }
    public int VerifiedBeaconCount { get; init; }
    public long TotalPeersDiscovered { get; init; }
    public long TotalConnectionsAttempted { get; init; }
    public long TotalConnectionsSucceeded { get; init; }
    public DateTimeOffset? LastAnnounceTime { get; init; }
    public DateTimeOffset? LastDiscoveryTime { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public long UptimeSeconds { get; init; }
    public IReadOnlyList<string> RendezvousInfohashes { get; init; } = Array.Empty<string>();
}

public sealed class DiscoveredPeerResponse
{
    public required string Address { get; init; }
    public int Port { get; init; }
}

public sealed class DiscoveryResultResponse
{
    public int NewConnectionsMade { get; init; }
    public int TotalMeshConnections { get; init; }
}

public sealed class MeshPeerInfoResponse
{
    public required string Username { get; init; }
    public required string Address { get; init; }
    public int Port { get; init; }
    public required List<string> Features { get; init; }
    public DateTimeOffset ConnectedAt { get; init; }
    public DateTimeOffset LastActivity { get; init; }
    public string? CertificateThumbprint { get; init; }
}

public sealed class OverlayStatsResponse
{
    public required ServerStatsResponse Server { get; init; }
    public required ConnectorStatsResponse Connector { get; init; }
    public required RateLimiterStatsResponse RateLimiter { get; init; }
    public required BlocklistStatsResponse Blocklist { get; init; }
}

public sealed class ServerStatsResponse
{
    public bool IsListening { get; init; }
    public int ListenPort { get; init; }
    public int ActiveConnections { get; init; }
    public long TotalConnectionsAccepted { get; init; }
    public long TotalConnectionsRejected { get; init; }
    public long UptimeSeconds { get; init; }
}

public sealed class ConnectorStatsResponse
{
    public int PendingConnections { get; init; }
    public long SuccessfulConnections { get; init; }
    public long FailedConnections { get; init; }
    public double SuccessRate { get; init; }
}

public sealed class RateLimiterStatsResponse
{
    public int TotalConnections { get; init; }
    public int ConnectionsLastMinute { get; init; }
    public int TrackedIps { get; init; }
    public int TrackedConnections { get; init; }
    public int BlockedIps { get; init; }
}

public sealed class BlocklistStatsResponse
{
    public int BlockedIpCount { get; init; }
    public int BlockedUsernameCount { get; init; }
    public int PermanentIpBans { get; init; }
    public int PermanentUsernameBans { get; init; }
}

public sealed class BlocklistResponse
{
    public required List<BlockedEntryResponse> Entries { get; init; }
}

public sealed class BlockedEntryResponse
{
    public required string Target { get; init; }
    public required string Type { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset BlockedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool IsPermanent { get; init; }
}

// Request DTOs
public sealed class BlockIpRequest
{
    public required string Ip { get; init; }
    public string? Reason { get; init; }
    public int? DurationMinutes { get; init; }
    public bool Permanent { get; init; }
}

public sealed class BlockUsernameRequest
{
    public required string Username { get; init; }
    public string? Reason { get; init; }
    public int? DurationMinutes { get; init; }
    public bool Permanent { get; init; }
}

