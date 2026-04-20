// <copyright file="DhtRendezvousController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.API;

using slskd.Core.Security;

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
[Authorize(Policy = AuthPolicy.Any)]
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
            IsEnabled = stats.IsEnabled,
            LanOnly = stats.LanOnly,
            IsBeaconCapable = stats.IsBeaconCapable,
            IsDhtRunning = stats.IsDhtRunning,
            DhtNodeCount = stats.DhtNodeCount,
            DiscoveredPeerCount = stats.DiscoveredPeerCount,
            ActiveMeshConnections = stats.ActiveMeshConnections,
            VerifiedBeaconCount = stats.VerifiedBeaconCount,
            TotalPeersDiscovered = stats.TotalPeersDiscovered,
            TotalCandidateEndpointsSeen = stats.TotalCandidateEndpointsSeen,
            TotalCandidatesAccepted = stats.TotalCandidatesAccepted,
            TotalCandidatesSkippedDhtPort = stats.TotalCandidatesSkippedDhtPort,
            TotalCandidatesSkippedDiscoveredCapacity = stats.TotalCandidatesSkippedDiscoveredCapacity,
            TotalCandidatesDeferredConnectorCapacity = stats.TotalCandidatesDeferredConnectorCapacity,
            TotalCandidatesSkippedReconnectBackoff = stats.TotalCandidatesSkippedReconnectBackoff,
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
                Username = p.Username ?? string.Empty,
                Address = p.Endpoint.Address.ToString(),
                Port = p.Endpoint.Port,
                Features = p.Features.ToList(),
                ConnectedAt = p.ConnectedAt,
                LastActivity = p.LastActivity,
                CertificateThumbprint = p.CertificateThumbprint?[..16] + "...", // Truncate for display
                Version = p.PeerVersion,
                IsOutbound = p.IsOutbound,
            });

        return Ok(peers);
    }

    /// <summary>
    /// Connect to a specific overlay endpoint.
    /// </summary>
    [HttpPost("overlay/connect")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<OverlayConnectResultResponse>> ConnectOverlayPeer([FromBody] ConnectOverlayPeerRequest request, CancellationToken cancellationToken)
    {
        var addressText = request.Address?.Trim() ?? string.Empty;
        if (!System.Net.IPAddress.TryParse(addressText, out var address))
        {
            return BadRequest(new { error = "Invalid IP address" });
        }

        if (request.Port <= 0 || request.Port > 65535)
        {
            return BadRequest(new { error = "Invalid port" });
        }

        var endpoint = new System.Net.IPEndPoint(address, request.Port);
        var connection = await _overlayConnector.ConnectToEndpointAsync(endpoint, cancellationToken);
        if (connection is null)
        {
            return StatusCode(502, new OverlayConnectResultResponse
            {
                Connected = false,
                Address = endpoint.Address.ToString(),
                Port = endpoint.Port,
                ActiveConnections = _dhtService.ActiveMeshConnections,
            });
        }

        return Ok(new OverlayConnectResultResponse
        {
            Connected = true,
            Address = endpoint.Address.ToString(),
            Port = endpoint.Port,
            Username = connection.Username,
            ActiveConnections = _dhtService.ActiveMeshConnections,
        });
    }

    [HttpGet("overlay/stats")]
    public ActionResult<OverlayStatsResponse> GetOverlayStats()
    {
        var serverStats = _overlayServer.GetStats();
        var connectorStats = _overlayConnector.GetStats();
        var rateLimiterStats = _rateLimiter.GetStats();
        var blocklistStats = _blocklist.GetStats();

        // Get version stats from connected peers
        var connectedPeers = _dhtService.GetMeshPeers();
        var slskdnPeersWithVersion = connectedPeers.Count(p => p.PeerVersion.HasValue);
        var slskdnPeersWithoutVersion = connectedPeers.Count(p => !p.PeerVersion.HasValue);

        return Ok(new OverlayStatsResponse
        {
            Server = new ServerStatsResponse
            {
                IsListening = serverStats.IsListening,
                ListenPort = serverStats.ListenPort ?? 0,
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
                TotalSlskdnPeers = slskdnPeersWithVersion + slskdnPeersWithoutVersion,
                SlskdnPeersWithVersion = slskdnPeersWithVersion,
                SlskdnPeersWithoutVersion = slskdnPeersWithoutVersion,
                EndpointCooldownSkips = connectorStats.EndpointCooldownSkips,
                FailureReasons = new ConnectorFailureReasonStatsResponse
                {
                    ConnectTimeouts = connectorStats.FailureReasons.ConnectTimeouts,
                    NoRouteFailures = connectorStats.FailureReasons.NoRouteFailures,
                    ConnectionRefusedFailures = connectorStats.FailureReasons.ConnectionRefusedFailures,
                    ConnectionResetFailures = connectorStats.FailureReasons.ConnectionResetFailures,
                    TlsEofFailures = connectorStats.FailureReasons.TlsEofFailures,
                    TlsHandshakeFailures = connectorStats.FailureReasons.TlsHandshakeFailures,
                    ProtocolHandshakeFailures = connectorStats.FailureReasons.ProtocolHandshakeFailures,
                    RegistrationFailures = connectorStats.FailureReasons.RegistrationFailures,
                    BlockedPeerFailures = connectorStats.FailureReasons.BlockedPeerFailures,
                    UnknownFailures = connectorStats.FailureReasons.UnknownFailures,
                },
                TopProblemEndpoints = connectorStats.TopProblemEndpoints
                    .Select(endpoint => new OverlayEndpointHealthResponse
                    {
                        Endpoint = endpoint.Endpoint,
                        ConsecutiveFailureCount = endpoint.ConsecutiveFailureCount,
                        TotalFailures = endpoint.TotalFailures,
                        LastFailureReason = endpoint.LastFailureReason,
                        LastFailureAt = endpoint.LastFailureAt,
                        SuppressedUntil = endpoint.SuppressedUntil,
                        LastSuccessAt = endpoint.LastSuccessAt,
                        LastUsername = endpoint.LastUsername,
                    })
                    .ToList(),
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
        var ipText = request.Ip?.Trim() ?? string.Empty;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (!System.Net.IPAddress.TryParse(ipText, out var ip))
        {
            return BadRequest(new { error = "Invalid IP address" });
        }

        var duration = request.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
            : (TimeSpan?)null;

        _blocklist.BlockIp(ip, reason ?? "Manual block", duration, request.Permanent);

        return Ok(new { message = "IP address blocked" });
    }

    /// <summary>
    /// Add a username to the blocklist.
    /// </summary>
    [HttpPost("overlay/blocklist/username")]
    public ActionResult BlockUsername([FromBody] BlockUsernameRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { error = "Username required" });
        }

        var duration = request.DurationMinutes.HasValue
            ? TimeSpan.FromMinutes(request.DurationMinutes.Value)
            : (TimeSpan?)null;

        _blocklist.BlockUsername(username, reason ?? "Manual block", duration, request.Permanent);

        return Ok(new { message = "Username blocked" });
    }

    /// <summary>
    /// Remove an entry from the blocklist.
    /// </summary>
    [HttpDelete("overlay/blocklist/{type}/{target}")]
    public ActionResult Unblock(string type, string target)
    {
        type = type?.Trim() ?? string.Empty;
        target = target?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(target))
        {
            return BadRequest(new { error = "Type and target are required" });
        }

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
            return BadRequest(new { error = "Invalid blocklist entry type" });
        }

        if (!removed)
        {
            return NotFound(new { error = "Blocklist entry not found" });
        }

        return Ok(new { message = "Blocklist entry removed" });
    }
}

// Response DTOs
public sealed class DhtStatusResponse
{
    public bool IsEnabled { get; init; }
    public bool LanOnly { get; init; }
    public bool IsBeaconCapable { get; init; }
    public bool IsDhtRunning { get; init; }
    public int DhtNodeCount { get; init; }
    public int DiscoveredPeerCount { get; init; }
    public int ActiveMeshConnections { get; init; }
    public int VerifiedBeaconCount { get; init; }
    public long TotalPeersDiscovered { get; init; }
    public long TotalCandidateEndpointsSeen { get; init; }
    public long TotalCandidatesAccepted { get; init; }
    public long TotalCandidatesSkippedDhtPort { get; init; }
    public long TotalCandidatesSkippedDiscoveredCapacity { get; init; }
    public long TotalCandidatesDeferredConnectorCapacity { get; init; }
    public long TotalCandidatesSkippedReconnectBackoff { get; init; }
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
    public int? Version { get; init; }
    public bool IsOutbound { get; init; }
}

public sealed class OverlayConnectResultResponse
{
    public bool Connected { get; init; }
    public required string Address { get; init; }
    public int Port { get; init; }
    public string? Username { get; init; }
    public int ActiveConnections { get; init; }
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
    public int TotalSlskdnPeers { get; init; }
    public int SlskdnPeersWithVersion { get; init; }
    public int SlskdnPeersWithoutVersion { get; init; }
    public long EndpointCooldownSkips { get; init; }
    public required ConnectorFailureReasonStatsResponse FailureReasons { get; init; }
    public required List<OverlayEndpointHealthResponse> TopProblemEndpoints { get; init; }
}

public sealed class ConnectorFailureReasonStatsResponse
{
    public long ConnectTimeouts { get; init; }
    public long NoRouteFailures { get; init; }
    public long ConnectionRefusedFailures { get; init; }
    public long ConnectionResetFailures { get; init; }
    public long TlsEofFailures { get; init; }
    public long TlsHandshakeFailures { get; init; }
    public long ProtocolHandshakeFailures { get; init; }
    public long RegistrationFailures { get; init; }
    public long BlockedPeerFailures { get; init; }
    public long UnknownFailures { get; init; }
}

public sealed class OverlayEndpointHealthResponse
{
    public required string Endpoint { get; init; }
    public int ConsecutiveFailureCount { get; init; }
    public long TotalFailures { get; init; }
    public required string LastFailureReason { get; init; }
    public DateTimeOffset? LastFailureAt { get; init; }
    public DateTimeOffset? SuppressedUntil { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public string? LastUsername { get; init; }
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
public sealed class ConnectOverlayPeerRequest
{
    public required string Address { get; init; }
    public int Port { get; init; }
}

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
