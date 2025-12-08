// <copyright file="SecurityServices.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Aggregate class providing access to all security services.
/// Injected as a single dependency for components that need multiple security features.
/// </summary>
public sealed class SecurityServices
{
    /// <summary>
    /// Gets or sets the network guard service.
    /// </summary>
    public NetworkGuard? NetworkGuard { get; init; }

    /// <summary>
    /// Gets or sets the violation tracker service.
    /// </summary>
    public ViolationTracker? ViolationTracker { get; init; }

    /// <summary>
    /// Gets or sets the connection fingerprint service.
    /// </summary>
    public ConnectionFingerprint? ConnectionFingerprint { get; init; }

    /// <summary>
    /// Gets or sets the peer reputation service.
    /// </summary>
    public PeerReputation? PeerReputation { get; init; }

    /// <summary>
    /// Gets or sets the paranoid mode service.
    /// </summary>
    public ParanoidMode? ParanoidMode { get; init; }

    /// <summary>
    /// Gets or sets the cryptographic commitment service.
    /// </summary>
    public CryptographicCommitment? CryptographicCommitment { get; init; }

    /// <summary>
    /// Gets or sets the proof of storage service.
    /// </summary>
    public ProofOfStorage? ProofOfStorage { get; init; }

    /// <summary>
    /// Gets or sets the Byzantine consensus service.
    /// </summary>
    public ByzantineConsensus? ByzantineConsensus { get; init; }

    /// <summary>
    /// Gets or sets the probabilistic verification service.
    /// </summary>
    public ProbabilisticVerification? ProbabilisticVerification { get; init; }

    /// <summary>
    /// Gets or sets the entropy monitor service.
    /// </summary>
    public EntropyMonitor? EntropyMonitor { get; init; }

    /// <summary>
    /// Gets or sets the temporal consistency service.
    /// </summary>
    public TemporalConsistency? TemporalConsistency { get; init; }

    /// <summary>
    /// Gets or sets the fingerprint detection service.
    /// </summary>
    public FingerprintDetection? FingerprintDetection { get; init; }

    /// <summary>
    /// Gets or sets the honeypot service.
    /// </summary>
    public Honeypot? Honeypot { get; init; }

    /// <summary>
    /// Gets or sets the canary traps service.
    /// </summary>
    public CanaryTraps? CanaryTraps { get; init; }

    /// <summary>
    /// Gets or sets the asymmetric disclosure service.
    /// </summary>
    public AsymmetricDisclosure? AsymmetricDisclosure { get; init; }

    /// <summary>
    /// Gets or sets the security event sink.
    /// </summary>
    public ISecurityEventSink? EventSink { get; init; }

    /// <summary>
    /// Gets whether security is fully disabled.
    /// </summary>
    public bool IsDisabled =>
        NetworkGuard is null &&
        ViolationTracker is null &&
        PeerReputation is null;

    /// <summary>
    /// Gets whether minimal security is enabled.
    /// </summary>
    public bool IsMinimal =>
        NetworkGuard is not null &&
        ViolationTracker is not null &&
        ParanoidMode is null;

    /// <summary>
    /// Gets whether standard security is enabled.
    /// </summary>
    public bool IsStandard =>
        NetworkGuard is not null &&
        ViolationTracker is not null &&
        PeerReputation is not null &&
        ParanoidMode is null;

    /// <summary>
    /// Gets whether maximum security is enabled.
    /// </summary>
    public bool IsMaximum =>
        NetworkGuard is not null &&
        ViolationTracker is not null &&
        PeerReputation is not null &&
        ParanoidMode is not null &&
        Honeypot is not null;

    /// <summary>
    /// Get aggregate statistics from all services.
    /// </summary>
    public SecurityAggregateStats GetAggregateStats()
    {
        return new SecurityAggregateStats
        {
            NetworkGuardStats = NetworkGuard?.GetStats(),
            ViolationStats = ViolationTracker?.GetStats(),
            ReputationStats = PeerReputation?.GetStats(),
            ParanoidStats = ParanoidMode?.GetStats(),
            EventStats = EventSink?.GetStats(),
            FingerprintStats = FingerprintDetection?.GetStats(),
            HoneypotStats = Honeypot?.GetStats(),
            CanaryStats = CanaryTraps?.GetStats(),
            EntropyStats = EntropyMonitor?.GetStats(),
            ConsensusStats = ByzantineConsensus?.GetStats(),
            VerificationStats = ProbabilisticVerification?.GetStats(),
            DisclosureStats = AsymmetricDisclosure?.GetStats(),
            TemporalStats = TemporalConsistency?.GetStats(),
        };
    }

    /// <summary>
    /// Check if a peer should be trusted for sensitive operations.
    /// </summary>
    /// <param name="username">The peer's username.</param>
    /// <returns>Trust assessment.</returns>
    public TrustAssessment AssessTrust(string username)
    {
        var reputation = PeerReputation?.GetScore(username);
        var isBanned = ViolationTracker?.IsUsernameBanned(username) ?? false;

        if (isBanned)
        {
            return new TrustAssessment
            {
                Username = username,
                IsTrusted = false,
                IsBanned = true,
                Reason = "User is banned",
            };
        }

        if (reputation.HasValue && reputation.Value < PeerReputation.UntrustedThreshold)
        {
            return new TrustAssessment
            {
                Username = username,
                IsTrusted = false,
                Score = reputation,
                Reason = "Low reputation score",
            };
        }

        if (reputation.HasValue && reputation.Value >= PeerReputation.TrustedThreshold)
        {
            return new TrustAssessment
            {
                Username = username,
                IsTrusted = true,
                Score = reputation,
            };
        }

        return new TrustAssessment
        {
            Username = username,
            IsTrusted = false, // Neutral is not trusted for sensitive ops
            Score = reputation,
            Reason = "Neutral reputation",
        };
    }

    /// <summary>
    /// Report a security event through all relevant services.
    /// </summary>
    public void ReportSecurityEvent(
        SecurityEventType type,
        SecuritySeverity severity,
        string message,
        string? sourceIp = null,
        string? username = null)
    {
        var evt = SecurityEvent.Create(type, severity, message, sourceIp, username);
        EventSink?.Report(evt);

        // Auto-track violations for high severity events
        if (severity >= SecuritySeverity.High && username != null)
        {
            var violationType = type switch
            {
                SecurityEventType.PathTraversal => ViolationType.PathTraversal,
                SecurityEventType.ContentSafety => ViolationType.DangerousContent,
                SecurityEventType.RateLimit => ViolationType.RateLimitExceeded,
                _ => ViolationType.Other,
            };

            ViolationTracker?.RecordUsernameViolation(username, violationType, message);
        }

        // Auto-adjust reputation for medium+ severity
        if (severity >= SecuritySeverity.Medium && username != null)
        {
            switch (type)
            {
                case SecurityEventType.PathTraversal:
                    PeerReputation?.RecordProtocolViolation(username, message);
                    break;
                case SecurityEventType.ContentSafety:
                    PeerReputation?.RecordContentMismatch(username, message);
                    break;
                case SecurityEventType.Verification:
                    PeerReputation?.RecordMalformedMessage(username);
                    break;
            }
        }
    }
}

/// <summary>
/// Aggregate statistics from all security services.
/// </summary>
public sealed class SecurityAggregateStats
{
    /// <summary>Gets or sets network guard stats.</summary>
    public NetworkGuardStats? NetworkGuardStats { get; init; }

    /// <summary>Gets or sets violation stats.</summary>
    public ViolationStats? ViolationStats { get; init; }

    /// <summary>Gets or sets reputation stats.</summary>
    public ReputationStats? ReputationStats { get; init; }

    /// <summary>Gets or sets paranoid mode stats.</summary>
    public ParanoidStats? ParanoidStats { get; init; }

    /// <summary>Gets or sets event stats.</summary>
    public SecurityEventStats? EventStats { get; init; }

    /// <summary>Gets or sets fingerprint stats.</summary>
    public ReconnaissanceStats? FingerprintStats { get; init; }

    /// <summary>Gets or sets honeypot stats.</summary>
    public HoneypotStats? HoneypotStats { get; init; }

    /// <summary>Gets or sets canary stats.</summary>
    public CanaryStats? CanaryStats { get; init; }

    /// <summary>Gets or sets entropy stats.</summary>
    public EntropyStats? EntropyStats { get; init; }

    /// <summary>Gets or sets consensus stats.</summary>
    public ConsensusStats? ConsensusStats { get; init; }

    /// <summary>Gets or sets verification stats.</summary>
    public VerificationStats? VerificationStats { get; init; }

    /// <summary>Gets or sets disclosure stats.</summary>
    public DisclosureStats? DisclosureStats { get; init; }

    /// <summary>Gets or sets temporal stats.</summary>
    public TemporalStats? TemporalStats { get; init; }
}

/// <summary>
/// Result of a trust assessment.
/// </summary>
public sealed class TrustAssessment
{
    /// <summary>Gets or sets the username.</summary>
    public required string Username { get; init; }

    /// <summary>Gets or sets whether trusted.</summary>
    public bool IsTrusted { get; init; }

    /// <summary>Gets or sets whether banned.</summary>
    public bool IsBanned { get; init; }

    /// <summary>Gets or sets reputation score.</summary>
    public int? Score { get; init; }

    /// <summary>Gets or sets reason for assessment.</summary>
    public string? Reason { get; init; }
}
