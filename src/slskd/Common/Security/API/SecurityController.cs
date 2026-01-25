// <copyright file="SecurityController.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security.API;

using slskd.Core.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Common.Security;
using slskd.Mesh;

/// <summary>
/// API controller for security monitoring and management.
/// </summary>
[ApiController]
[Route("api/v0/security")]
[Authorize]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
public class SecurityController : ControllerBase
{
    private readonly SecurityServices? _security;
    private readonly ISecurityEventSink? _eventSink;
    private readonly AdversarialOptions? _adversarialOptions;
    private readonly AnonymityTransportSelector? _transportSelector;
    private readonly Mesh.IMeshCircuitBuilder? _circuitBuilder;
    private readonly Mesh.IMeshPeerManager? _peerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityController"/> class.
    /// </summary>
    public SecurityController(
        SecurityServices? security = null,
        ISecurityEventSink? eventSink = null,
        AdversarialOptions? adversarialOptions = null,
        AnonymityTransportSelector? transportSelector = null,
        Mesh.IMeshCircuitBuilder? circuitBuilder = null,
        Mesh.IMeshPeerManager? peerManager = null)
    {
        _security = security;
        _eventSink = eventSink ?? security?.EventSink;
        _adversarialOptions = adversarialOptions;
        _transportSelector = transportSelector;
        _circuitBuilder = circuitBuilder;
        _peerManager = peerManager;
    }

    /// <summary>
    /// Get security dashboard overview.
    /// </summary>
    [HttpGet("dashboard")]
    public ActionResult<SecurityDashboard> GetDashboard()
    {
        return Ok(new SecurityDashboard
        {
            EventStats = _eventSink?.GetStats(),
            NetworkGuardStats = _security?.NetworkGuard?.GetStats(),
            ViolationStats = _security?.ViolationTracker?.GetStats(),
            ReputationStats = _security?.PeerReputation?.GetStats(),
            ParanoidStats = _security?.ParanoidMode?.GetStats(),
            FingerprintStats = _security?.FingerprintDetection?.GetStats(),
            HoneypotStats = _security?.Honeypot?.GetStats(),
            CanaryStats = _security?.CanaryTraps?.GetStats(),
            EntropyStats = _security?.EntropyMonitor?.GetStats(),
            ConsensusStats = _security?.ByzantineConsensus?.GetStats(),
            VerificationStats = _security?.ProbabilisticVerification?.GetStats(),
            DisclosureStats = _security?.AsymmetricDisclosure?.GetStats(),
            TemporalStats = _security?.TemporalConsistency?.GetStats(),
        });
    }

    /// <summary>
    /// Get recent security events.
    /// </summary>
    [HttpGet("events")]
    public ActionResult<IEnumerable<SecurityEvent>> GetEvents(
        [FromQuery] int count = 100,
        [FromQuery] string? minSeverity = null)
    {
        var severity = SecuritySeverity.Info;
        if (!string.IsNullOrEmpty(minSeverity) && Enum.TryParse<SecuritySeverity>(minSeverity, true, out var parsed))
        {
            severity = parsed;
        }

        var events = _eventSink?.GetRecentEvents(count, severity) ?? Array.Empty<SecurityEvent>();
        return Ok(events);
    }

    /// <summary>
    /// Get active bans.
    /// </summary>
    [HttpGet("bans")]
    public ActionResult<IEnumerable<BanRecord>> GetBans()
    {
        var bans = _security?.ViolationTracker?.GetActiveBans() ?? Array.Empty<BanRecord>();
        return Ok(bans);
    }

    /// <summary>
    /// Ban an IP address.
    /// </summary>
    /// <remarks>Requires admin privileges.</remarks>
    [HttpPost("bans/ip")]
    [Authorize(Roles = "Administrator")]
    public ActionResult BanIp([FromBody] BanIpRequest request)
    {
        if (!IPAddress.TryParse(request.IpAddress, out var ip))
        {
            return BadRequest("Invalid IP address");
        }

        // Compute duration with precedence: Duration (TimeSpan) first, then DurationMinutes, then null
        var duration = request.Duration 
            ?? (request.DurationMinutes.HasValue ? TimeSpan.FromMinutes(request.DurationMinutes.Value) : null);

        _security?.ViolationTracker?.BanIp(ip, request.Reason ?? "Manual ban", duration, request.Permanent);

        _eventSink?.Report(SecurityEvent.Create(
            SecurityEventType.Ban,
            SecuritySeverity.Medium,
            $"Manual IP ban: {request.IpAddress}",
            request.IpAddress));

        return Ok();
    }

    /// <summary>
    /// Unban an IP address.
    /// </summary>
    /// <remarks>Requires admin privileges.</remarks>
    [HttpDelete("bans/ip/{ipAddress}")]
    [Authorize(Roles = "Administrator")]
    public ActionResult UnbanIp(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            return BadRequest("Invalid IP address");
        }

        var result = _security?.ViolationTracker?.UnbanIp(ip) ?? false;

        if (result)
        {
            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.Ban,
                SecuritySeverity.Low,
                $"IP unbanned: {ipAddress}",
                ipAddress));
        }

        return result ? Ok() : NotFound();
    }

    /// <summary>
    /// Ban a username.
    /// </summary>
    /// <remarks>Requires admin privileges.</remarks>
    [HttpPost("bans/username")]
    [Authorize(Roles = "Administrator")]
    public ActionResult BanUsername([FromBody] BanUsernameRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest("Username is required");
        }

        // Compute duration with precedence: Duration (TimeSpan) first, then DurationMinutes, then null
        var duration = request.Duration 
            ?? (request.DurationMinutes.HasValue ? TimeSpan.FromMinutes(request.DurationMinutes.Value) : null);

        _security?.ViolationTracker?.BanUsername(request.Username, request.Reason ?? "Manual ban", duration, request.Permanent);

        _eventSink?.Report(SecurityEvent.Create(
            SecurityEventType.Ban,
            SecuritySeverity.Medium,
            $"Manual username ban: {request.Username}",
            username: request.Username));

        return Ok();
    }

    /// <summary>
    /// Unban a username.
    /// </summary>
    /// <remarks>Requires admin privileges.</remarks>
    [HttpDelete("bans/username/{username}")]
    [Authorize(Roles = "Administrator")]
    public ActionResult UnbanUsername(string username)
    {
        var result = _security?.ViolationTracker?.UnbanUsername(username) ?? false;

        if (result)
        {
            _eventSink?.Report(SecurityEvent.Create(
                SecurityEventType.Ban,
                SecuritySeverity.Low,
                $"Username unbanned: {username}",
                username: username));
        }

        return result ? Ok() : NotFound();
    }

    /// <summary>
    /// Get peer reputation.
    /// </summary>
    [HttpGet("reputation/{username}")]
    public ActionResult<PeerProfile> GetReputation(string username)
    {
        var profile = _security?.PeerReputation?.GetOrCreateProfile(username);
        return profile != null ? Ok(profile) : NotFound();
    }

    /// <summary>
    /// Set peer reputation manually.
    /// </summary>
    /// <remarks>Requires admin privileges.</remarks>
    [HttpPut("reputation/{username}")]
    [Authorize(Roles = "Administrator")]
    public ActionResult SetReputation(string username, [FromBody] SetReputationRequest request)
    {
        if (request.Score < 0 || request.Score > 100)
        {
            return BadRequest("Score must be between 0 and 100");
        }

        _security?.PeerReputation?.SetScore(username, (int)request.Score, request.Reason ?? "Manual adjustment");

        _eventSink?.Report(SecurityEvent.Create(
            SecurityEventType.TrustChange,
            SecuritySeverity.Low,
            $"Reputation set for {username}: {request.Score}",
            username: username));

        return Ok();
    }

    /// <summary>
    /// Get suspicious peers (low reputation).
    /// </summary>
    [HttpGet("reputation/suspicious")]
    public ActionResult<IEnumerable<PeerProfile>> GetSuspiciousPeers([FromQuery] int limit = 50)
    {
        var peers = _security?.PeerReputation?.GetSuspiciousPeers(limit) ?? Array.Empty<PeerProfile>();
        return Ok(peers);
    }

    /// <summary>
    /// Get trusted peers (high reputation).
    /// </summary>
    [HttpGet("reputation/trusted")]
    public ActionResult<IEnumerable<PeerProfile>> GetTrustedPeers([FromQuery] int limit = 50)
    {
        var peers = _security?.PeerReputation?.GetTrustedPeers(limit) ?? Array.Empty<PeerProfile>();
        return Ok(peers);
    }

    /// <summary>
    /// Get known scanners/reconnaissance.
    /// </summary>
    [HttpGet("scanners")]
    public ActionResult<IEnumerable<ReconnaissanceProfile>> GetScanners()
    {
        var scanners = _security?.FingerprintDetection?.GetKnownScanners() ?? Array.Empty<ReconnaissanceProfile>();
        return Ok(scanners);
    }

    /// <summary>
    /// Get threat profiles from honeypots.
    /// </summary>
    [HttpGet("threats")]
    public ActionResult<IEnumerable<ThreatProfile>> GetThreats([FromQuery] string? minLevel = null)
    {
        var level = ThreatLevel.Medium;
        if (!string.IsNullOrEmpty(minLevel) && Enum.TryParse<ThreatLevel>(minLevel, true, out var parsed))
        {
            level = parsed;
        }

        var threats = _security?.Honeypot?.GetThreats(level) ?? Array.Empty<ThreatProfile>();
        return Ok(threats);
    }

    /// <summary>
    /// Get canary trap sightings.
    /// </summary>
    [HttpGet("canaries")]
    public ActionResult<CanaryStats> GetCanaryStats()
    {
        var stats = _security?.CanaryTraps?.GetStats();
        return stats != null ? Ok(stats) : NotFound();
    }

    /// <summary>
    /// Run entropy health check.
    /// </summary>
    [HttpPost("entropy/check")]
    public ActionResult<RngHealthCheck> RunEntropyCheck()
    {
        var result = _security?.EntropyMonitor?.TestRngHealth();
        return result != null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Get trust disclosure permissions for a peer.
    /// </summary>
    [HttpGet("disclosure/{username}")]
    public ActionResult<DisclosurePermissions> GetDisclosure(string username)
    {
        var permissions = _security?.AsymmetricDisclosure?.GetDisclosurePermissions(username);
        return permissions != null ? Ok(permissions) : NotFound();
    }

    /// <summary>
    /// Set trust tier for a peer.
    /// </summary>
    [HttpPut("disclosure/{username}")]
    public ActionResult SetTrustTier(string username, [FromBody] SetTrustTierRequest request)
    {
        // Validate numeric tier value is a defined enum member
        if (!Enum.IsDefined(typeof(TrustTier), request.Tier))
        {
            return BadRequest("Invalid trust tier");
        }

        var tier = (TrustTier)request.Tier;

        _security?.AsymmetricDisclosure?.SetTrustTier(username, tier, request.Reason ?? "Manual override");

        _eventSink?.Report(SecurityEvent.Create(
            SecurityEventType.TrustChange,
            SecuritySeverity.Low,
            $"Trust tier set for {username}: {tier}",
            username: username));

        return Ok();
    }

    /// <summary>
    /// Get network guard statistics.
    /// </summary>
    [HttpGet("network")]
    public ActionResult<NetworkGuardStats> GetNetworkStats()
    {
        var stats = _security?.NetworkGuard?.GetStats();
        return stats != null ? Ok(stats) : NotFound();
    }

    /// <summary>
    /// Get top connectors by IP.
    /// </summary>
    [HttpGet("network/top")]
    public ActionResult<IEnumerable<ConnectionInfo>> GetTopConnectors([FromQuery] int limit = 10)
    {
        var connectors = _security?.NetworkGuard?.GetTopConnectors(limit) ?? Array.Empty<ConnectionInfo>();
        return Ok(connectors);
    }

    /// <summary>
    /// Get server anomalies detected by paranoid mode.
    /// </summary>
    [HttpGet("anomalies")]
    public ActionResult<IEnumerable<ServerAnomaly>> GetAnomalies([FromQuery] int count = 100)
    {
        var anomalies = _security?.ParanoidMode?.GetRecentAnomalies(count) ?? Array.Empty<ServerAnomaly>();
        return Ok(anomalies);
    }
    public ActionResult<AdversarialOptions> GetAdversarialSettings()
    {
        if (_adversarialOptions == null)
        {
            return NotFound("Adversarial features are not configured");
        }

        return Ok(_adversarialOptions);
    }

    [HttpGet("tor/status")]
    public ActionResult<AnonymityTransportStatus> GetTorStatus()
    {
        var torTransport = _transportSelector?.GetTorTransport();
        if (torTransport == null)
        {
            return NotFound("Tor transport is not configured or available");
        }

        var status = torTransport.GetStatus();
        return Ok(status);
    }

    [HttpPost("tor/test")]
    public async Task<ActionResult<AnonymityTransportStatus>> TestTorConnectivity()
    {
        var torTransport = _transportSelector?.GetTorTransport();
        if (torTransport == null)
        {
            return NotFound("Tor transport is not configured or available");
        }

        try
        {
            await torTransport.IsAvailableAsync();
            var status = torTransport.GetStatus();
            return Ok(status);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Tor connectivity test failed: {ex.Message}");
        }
    }

    [HttpPut("adversarial")]
    public ActionResult UpdateAdversarialSettings([FromBody] AdversarialOptions settings)
    {
        // NOTE: In a real implementation, this would persist the settings
        // For now, just validate the input
        if (settings == null)
        {
            return BadRequest("Settings cannot be null");
        }

        // Basic validation
        if (settings.Privacy?.Padding?.BucketSizes != null &&
            settings.Privacy.Padding.BucketSizes.Any(size => size <= 0))
        {
            return BadRequest("Bucket sizes must be positive");
        }

        // TODO: Implement persistence and runtime configuration updates
        return Ok(new { message = "Adversarial settings updated (persistence not yet implemented)" });
    }

    [HttpGet("adversarial/stats")]
    public ActionResult<AdversarialStats> GetAdversarialStats()
    {
        return Ok(new AdversarialStats
        {
            Enabled = _adversarialOptions?.Enabled ?? false,
            Profile = (_adversarialOptions?.Profile ?? AdversarialProfile.Disabled).ToString(),
            PrivacyEnabled = _adversarialOptions?.Privacy?.Enabled ?? false,
            AnonymityEnabled = _adversarialOptions?.Anonymity?.Enabled ?? false,
            TransportEnabled = _adversarialOptions?.Transport?.Enabled ?? false,
            OnionRoutingEnabled = _adversarialOptions?.OnionRouting?.Enabled ?? false,
            CensorshipResistanceEnabled = _adversarialOptions?.CensorshipResistance?.Enabled ?? false,
            PlausibleDeniabilityEnabled = _adversarialOptions?.PlausibleDeniability?.Enabled ?? false,
        });
    }

    [HttpGet("transports/status")]
    public ActionResult<TransportSelectorStatus> GetTransportStatus()
    {
        if (_transportSelector == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Transport selector not available");
        }

        return Ok(_transportSelector.GetSelectorStatus());
    }

    [HttpGet("transports")]
    public ActionResult<Dictionary<AnonymityTransportType, AnonymityTransportStatus>> GetAllTransportStatuses()
    {
        if (_transportSelector == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Transport selector not available");
        }

        return Ok(_transportSelector.GetAllStatuses());
    }

    [HttpPost("transports/test")]
    public async Task<ActionResult> TestTransportConnectivity()
    {
        if (_transportSelector == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Transport selector not available");
        }

        try
        {
            await _transportSelector.TestConnectivityAsync();
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.InternalServerError, $"Connectivity test failed: {ex.Message}");
        }
    }

    [HttpGet("circuits/stats")]
    public ActionResult<CircuitStatistics> GetCircuitStats()
    {
        if (_circuitBuilder == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Circuit builder not available");
        }

        return Ok(_circuitBuilder.GetStatistics());
    }

    [HttpGet("circuits")]
    public ActionResult<List<CircuitInfo>> GetActiveCircuits()
    {
        if (_circuitBuilder == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Circuit builder not available");
        }

        var circuits = _circuitBuilder.GetActiveCircuits();
        return Ok(circuits.Select(c => c.GetInfo()).ToList());
    }

    [HttpPost("circuits")]
    public async Task<ActionResult<CircuitInfo>> BuildCircuit([FromBody] BuildCircuitRequest request)
    {
        if (_circuitBuilder == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Circuit builder not available");
        }

        // Validate TargetPeerId is provided
        if (string.IsNullOrWhiteSpace(request.TargetPeerId))
        {
            return BadRequest("TargetPeerId is required");
        }

        // Compute circuit length with precedence: Length (nullable) first, then CircuitLength, default to 3
        var circuitLength = request.Length ?? request.CircuitLength;

        try
        {
            var circuit = await _circuitBuilder.BuildCircuitAsync(
                request.TargetPeerId,
                circuitLength);

            return Ok(circuit.GetInfo());
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.BadRequest, $"Circuit building failed: {ex.Message}");
        }
    }

    [HttpDelete("circuits/{circuitId}")]
    public ActionResult DestroyCircuit(string circuitId)
    {
        if (_circuitBuilder == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Circuit builder not available");
        }

        _circuitBuilder.DestroyCircuit(circuitId);
        return Ok();
    }

    [HttpGet("peers/stats")]
    public ActionResult<PeerStatistics> GetPeerStats()
    {
        if (_peerManager == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Peer manager not available");
        }

        return Ok(_peerManager.GetStatistics());
    }

    [HttpGet("peers")]
    public async Task<ActionResult<List<PeerInfo>>> GetPeers()
    {
        if (_peerManager == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Peer manager not available");
        }

        var peers = await _peerManager.GetAvailablePeersAsync();
        return Ok(peers.Select(p => new PeerInfo
        {
            PeerId = p.PeerId,
            Addresses = p.Addresses.Select(a => a.ToString()).ToList(),
            LastSeen = p.LastSeen,
            TrustScore = p.TrustScore,
            LatencyMs = p.LatencyMs,
            BandwidthMbps = p.BandwidthMbps,
            SupportsOnionRouting = p.SupportsOnionRouting,
            Version = p.Version,
            QualityScore = p.GetQualityScore()
        }).ToList());
    }

}
