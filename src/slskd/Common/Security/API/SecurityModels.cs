// <copyright file="SecurityModels.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;

    /// <summary>
    /// Request to ban an IP address.
    /// </summary>
    public sealed class BanIpRequest
    {
        /// <summary>
        /// Gets or sets the IP address to ban.
        /// </summary>
        [Required]
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for the ban.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the duration of the ban (TimeSpan).
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets whether the ban is permanent.
        /// </summary>
        public bool Permanent { get; set; } = false;
    }

    /// <summary>
    /// Request to ban a username.
    /// </summary>
    public sealed class BanUsernameRequest
    {
        /// <summary>
        /// Gets or sets the username to ban.
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reason for the ban.
        /// </summary>
        public string? Reason { get; set; }

        /// <summary>
        /// Gets or sets the duration of the ban (TimeSpan).
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Gets or sets whether the ban is permanent.
        /// </summary>
        public bool Permanent { get; set; } = false;
    }

    /// <summary>
    /// Request to set peer reputation.
    /// </summary>
    public sealed class SetReputationRequest
    {
        /// <summary>
        /// Gets or sets the peer ID.
        /// </summary>
        [Required]
        public string PeerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the reputation score.
        /// </summary>
        [Required]
        public double Score { get; set; }
    }

    /// <summary>
    /// Request to set trust tier.
    /// </summary>
    public sealed class SetTrustTierRequest
    {
        /// <summary>
        /// Gets or sets the peer ID.
        /// </summary>
        [Required]
        public string PeerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the trust tier.
        /// </summary>
        [Required]
        public int Tier { get; set; }
    }

    /// <summary>
    /// Request to build a circuit.
    /// </summary>
    public sealed class BuildCircuitRequest
    {
        /// <summary>
        /// Gets or sets the target peer ID.
        /// </summary>
        public string? TargetPeerId { get; set; }

        /// <summary>
        /// Gets or sets the circuit length.
        /// </summary>
        public int CircuitLength { get; set; } = 3;

        /// <summary>
        /// Gets or sets the circuit length (number of hops).
        /// </summary>
        public int? Length { get; set; }
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

        /// <summary>Gets or sets temporal statistics.</summary>
        public TemporalStats? TemporalStats { get; set; }

        /// <summary>Gets or sets adversarial statistics.</summary>
        public AdversarialStats? AdversarialStats { get; set; }
    }

    /// <summary>
    /// Adversarial statistics.
    /// </summary>
    public sealed class AdversarialStats
    {
        /// <summary>Gets or sets whether adversarial mode is enabled.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets the current adversarial profile.</summary>
        public string? Profile { get; set; }

        /// <summary>Gets or sets whether privacy features are enabled.</summary>
        public bool PrivacyEnabled { get; set; }

        /// <summary>Gets or sets whether anonymity features are enabled.</summary>
        public bool AnonymityEnabled { get; set; }

        /// <summary>Gets or sets whether transport obfuscation is enabled.</summary>
        public bool TransportEnabled { get; set; }

        /// <summary>Gets or sets whether plausible deniability features are enabled.</summary>
        public bool PlausibleDeniabilityEnabled { get; set; }

        /// <summary>Gets or sets whether onion routing is enabled.</summary>
        public bool OnionRoutingEnabled { get; set; }

        /// <summary>Gets or sets temporal consistency statistics.</summary>
        public TemporalStats? TemporalStats { get; set; }

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
    }
}
