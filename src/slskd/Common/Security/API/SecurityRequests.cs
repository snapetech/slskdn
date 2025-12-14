// <copyright file="SecurityRequests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using slskd.Common.Security;

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
    }

    /// <summary>
    /// Information about a peer.
    /// </summary>
    public sealed class PeerInfo
    {
        /// <summary>
        /// Gets or sets the peer ID.
        /// </summary>
        public string PeerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the addresses.
        /// </summary>
        public List<string> Addresses { get; set; } = new();

        /// <summary>
        /// Gets or sets the last seen time.
        /// </summary>
        public DateTimeOffset LastSeen { get; set; }

        /// <summary>
        /// Gets or sets the trust score.
        /// </summary>
        public double TrustScore { get; set; }

        /// <summary>
        /// Gets or sets the latency in milliseconds.
        /// </summary>
        public int LatencyMs { get; set; }

        /// <summary>
        /// Gets or sets the bandwidth in Mbps.
        /// </summary>
        public double BandwidthMbps { get; set; }

        /// <summary>
        /// Gets or sets whether the peer supports onion routing.
        /// </summary>
        public bool SupportsOnionRouting { get; set; }

        /// <summary>
        /// Gets or sets the peer version.
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quality score.
        /// </summary>
        public double QualityScore { get; set; }
    }

    /// <summary>
    /// Adversarial statistics.
    /// </summary>
    public sealed class AdversarialStats
    {
        /// <summary>
        /// Gets or sets temporal consistency statistics.
        /// </summary>
        public TemporalStats? TemporalStats { get; set; }

        /// <summary>
        /// Gets or sets fingerprint detection statistics.
        /// </summary>
        public ReconnaissanceStats? FingerprintStats { get; set; }

        /// <summary>
        /// Gets or sets honeypot statistics.
        /// </summary>
        public HoneypotStats? HoneypotStats { get; set; }

        /// <summary>
        /// Gets or sets canary statistics.
        /// </summary>
        public CanaryStats? CanaryStats { get; set; }

        /// <summary>
        /// Gets or sets entropy statistics.
        /// </summary>
        public EntropyStats? EntropyStats { get; set; }

        /// <summary>
        /// Gets or sets consensus statistics.
        /// </summary>
        public ConsensusStats? ConsensusStats { get; set; }

        /// <summary>
        /// Gets or sets disclosure statistics.
        /// </summary>
        public DisclosureStats? DisclosureStats { get; set; }
    }

}
