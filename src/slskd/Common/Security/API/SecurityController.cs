// <copyright file="SecurityController.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security.API;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using slskd.Common.Security;

/// <summary>
/// API controller for security monitoring and management.
/// </summary>
[ApiController]
[Route("api/v0/security")]
[Authorize]
public class SecurityController : ControllerBase
{
    private readonly SecurityServices? _security;
    private readonly ISecurityEventSink? _eventSink;
    private readonly AdversarialOptions? _adversarialOptions;
    private readonly AnonymityTransportSelector? _transportSelector;
    private readonly Mesh.MeshCircuitBuilder? _circuitBuilder;
    private readonly Mesh.IMeshPeerManager? _peerManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SecurityController"/> class.
    /// </summary>
    public SecurityController(
        SecurityServices? security = null,
        ISecurityEventSink? eventSink = null,
        AdversarialOptions? adversarialOptions = null,
        AnonymityTransportSelector? transportSelector = null,
        Mesh.MeshCircuitBuilder? circuitBuilder = null,
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

        var duration = request.Duration.HasValue
            ? TimeSpan.FromMinutes(request.Duration.Value)
            : (TimeSpan?)null;

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

        var duration = request.Duration.HasValue
            ? TimeSpan.FromMinutes(request.Duration.Value)
            : (TimeSpan?)null;

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

        _security?.PeerReputation?.SetScore(username, request.Score, request.Reason ?? "Manual adjustment");

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
        if (!Enum.TryParse<TrustTier>(request.Tier, true, out var tier))
        {
            return BadRequest("Invalid trust tier");
        }

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
}

/// <summary>
/// Security dashboard overview.
/// </summary>
public sealed class SecurityDashboard
{
    /// <summary>Gets or sets event statistics.</summary>
    public SecurityEventStats? EventStats { get; set; }

    /// <summary>Gets or sets network guard statistics.</summary>
    public NetworkGuardStats? NetworkGuardStats { get; set; }

    /// <summary>Gets or sets violation statistics.</summary>
    public ViolationStats? ViolationStats { get; set; }

    /// <summary>Gets or sets reputation statistics.</summary>
    public ReputationStats? ReputationStats { get; set; }

    /// <summary>Gets or sets paranoid mode statistics.</summary>
    public ParanoidStats? ParanoidStats { get; set; }

    /// <summary>Gets or sets fingerprint detection statistics.</summary>
    public ReconnaissanceStats? FingerprintStats { get; set; }

    /// <summary>Gets or sets honeypot statistics.</summary>
    public HoneypotStats? HoneypotStats { get; set; }

    /// <summary>Gets or sets canary statistics.</summary>
    public CanaryStats? CanaryStats { get; set; }

    /// <summary>Gets or sets entropy statistics.</summary>
    public EntropyStats? EntropyStats { get; set; }

    /// <summary>Gets or sets consensus statistics.</summary>
    public ConsensusStats? ConsensusStats { get; set; }

    /// <summary>Gets or sets verification statistics.</summary>
    public VerificationStats? VerificationStats { get; set; }

    /// <summary>Gets or sets disclosure statistics.</summary>
    public DisclosureStats? DisclosureStats { get; set; }

    /// <summary>Gets or sets temporal consistency statistics.</summary>
    public TemporalStats? TemporalStats { get; set; }
}

    /// <summary>
    /// Get current adversarial settings.
    /// </summary>
    [HttpGet("adversarial")]
    public ActionResult<AdversarialOptions> GetAdversarialSettings()
    {
        if (_adversarialOptions == null)
        {
            return NotFound("Adversarial features are not configured");
        }

        return Ok(_adversarialOptions);
    }

    /// <summary>
    /// Gets the current Tor connectivity status.
    /// </summary>
    /// <returns>The Tor transport status.</returns>
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

    /// <summary>
    /// Tests Tor connectivity and returns the current status.
    /// </summary>
    /// <returns>The Tor transport status after connectivity test.</returns>
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

    /// <summary>
    /// Update adversarial settings.
    /// </summary>
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

    /// <summary>
    /// Get adversarial statistics.
    /// </summary>
    [HttpGet("adversarial/stats")]
    public ActionResult<AdversarialStats> GetAdversarialStats()
    {
        return Ok(new AdversarialStats
        {
            Enabled = _adversarialOptions?.Enabled ?? false,
            Profile = _adversarialOptions?.Profile ?? AdversarialProfile.Disabled,
            PrivacyEnabled = _adversarialOptions?.Privacy?.Enabled ?? false,
            AnonymityEnabled = _adversarialOptions?.Anonymity?.Enabled ?? false,
            TransportEnabled = _adversarialOptions?.Transport?.Enabled ?? false,
            OnionRoutingEnabled = _adversarialOptions?.OnionRouting?.Enabled ?? false,
            CensorshipResistanceEnabled = _adversarialOptions?.CensorshipResistance?.Enabled ?? false,
            PlausibleDeniabilityEnabled = _adversarialOptions?.PlausibleDeniability?.Enabled ?? false,
        });
    }

    /// <summary>
    /// Get transport selector status and available transports.
    /// </summary>
    [HttpGet("transports/status")]
    public ActionResult<TransportSelectorStatus> GetTransportStatus()
    {
        if (_transportSelector == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Transport selector not available");
        }

        return Ok(_transportSelector.GetSelectorStatus());
    }

    /// <summary>
    /// Get detailed status of all configured transports.
    /// </summary>
    [HttpGet("transports")]
    public ActionResult<Dictionary<AnonymityTransportType, AnonymityTransportStatus>> GetAllTransportStatuses()
    {
        if (_transportSelector == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Transport selector not available");
        }

        return Ok(_transportSelector.GetAllStatuses());
    }

    /// <summary>
    /// Test connectivity for all configured transports.
    /// </summary>
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

    /// <summary>
    /// Get mesh circuit statistics.
    /// </summary>
    [HttpGet("circuits/stats")]
    public ActionResult<CircuitStatistics> GetCircuitStats()
    {
        if (_circuitBuilder == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Circuit builder not available");
        }

        return Ok(_circuitBuilder.GetStatistics());
    }

    /// <summary>
    /// Get information about all active circuits.
    /// </summary>
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

    /// <summary>
    /// Build a new circuit to a target peer.
    /// </summary>
    [HttpPost("circuits")]
    public async Task<ActionResult<CircuitInfo>> BuildCircuit([FromBody] BuildCircuitRequest request)
    {
        if (_circuitBuilder == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Circuit builder not available");
        }

        try
        {
            var circuit = await _circuitBuilder.BuildCircuitAsync(
                request.TargetPeerId,
                request.CircuitLength ?? 3);

            return Ok(circuit.GetInfo());
        }
        catch (Exception ex)
        {
            return StatusCode((int)HttpStatusCode.BadRequest, $"Circuit building failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Destroy a circuit by ID.
    /// </summary>
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

    /// <summary>
    /// Get mesh peer statistics.
    /// </summary>
    [HttpGet("peers/stats")]
    public ActionResult<PeerStatistics> GetPeerStats()
    {
        if (_peerManager == null)
        {
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Peer manager not available");
        }

        return Ok(_peerManager.GetStatistics());
    }

    /// <summary>
    /// Get all available mesh peers.
    /// </summary>
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

/// <summary>
/// Request to ban an IP.
/// </summary>
public sealed class BanIpRequest
{
    /// <summary>Gets or sets the IP address.</summary>
    public required string IpAddress { get; set; }

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets duration in minutes.</summary>
    public int? Duration { get; set; }

    /// <summary>Gets or sets whether permanent.</summary>
    public bool Permanent { get; set; }
}

/// <summary>
/// Request to ban a username.
/// </summary>
public sealed class BanUsernameRequest
{
    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; set; }

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }

    /// <summary>Gets or sets duration in minutes.</summary>
    public int? Duration { get; set; }

    /// <summary>Gets or sets whether permanent.</summary>
    public bool Permanent { get; set; }
}

/// <summary>
/// Request to set reputation.
/// </summary>
public sealed class SetReputationRequest
{
    /// <summary>Gets or sets the score (0-100).</summary>
    public required int Score { get; set; }

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Request to set trust tier.
/// </summary>
public sealed class SetTrustTierRequest
{
    /// <summary>Gets or sets the tier name.</summary>
    public required string Tier { get; set; }

    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Adversarial features statistics.
/// </summary>
public sealed class AdversarialStats
{
    /// <summary>Gets or sets whether adversarial features are enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the current adversarial profile.</summary>
    public AdversarialProfile Profile { get; set; }

    /// <summary>Gets or sets whether privacy layer is enabled.</summary>
    public bool PrivacyEnabled { get; set; }

    /// <summary>Gets or sets whether anonymity layer is enabled.</summary>
    public bool AnonymityEnabled { get; set; }

    /// <summary>Gets or sets whether obfuscated transport is enabled.</summary>
    public bool TransportEnabled { get; set; }

    /// <summary>Gets or sets whether onion routing is enabled.</summary>
    public bool OnionRoutingEnabled { get; set; }

    /// <summary>Gets or sets whether censorship resistance is enabled.</summary>
    public bool CensorshipResistanceEnabled { get; set; }

    /// <summary>Gets or sets whether plausible deniability is enabled.</summary>
    public bool PlausibleDeniabilityEnabled { get; set; }
}

/// <summary>
/// Request to build a new circuit.
/// </summary>
public sealed class BuildCircuitRequest
{
    /// <summary>Gets or sets the target peer ID.</summary>
    public required string TargetPeerId { get; set; }

    /// <summary>Gets or sets the circuit length (number of hops).</summary>
    public int? CircuitLength { get; set; }
}

/// <summary>
/// Information about a mesh peer.
/// </summary>
public sealed class PeerInfo
{
    /// <summary>Gets or sets the peer ID.</summary>
    public string PeerId { get; set; } = string.Empty;

    /// <summary>Gets or sets the peer addresses.</summary>
    public List<string> Addresses { get; set; } = new();

    /// <summary>Gets or sets when the peer was last seen.</summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>Gets or sets the trust score.</summary>
    public double TrustScore { get; set; }

    /// <summary>Gets or sets the latency in milliseconds.</summary>
    public int LatencyMs { get; set; }

    /// <summary>Gets or sets the bandwidth in Mbps.</summary>
    public double BandwidthMbps { get; set; }

    /// <summary>Gets or sets whether the peer supports onion routing.</summary>
    public bool SupportsOnionRouting { get; set; }

    /// <summary>Gets or sets the peer version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets or sets the quality score.</summary>
    public double QualityScore { get; set; }
}

