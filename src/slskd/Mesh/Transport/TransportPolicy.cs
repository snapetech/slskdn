// <copyright file="TransportPolicy.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh.Transport;

/// <summary>
/// Transport policy that can override global preferences for specific peers or pods.
/// </summary>
public class TransportPolicy
{
    /// <summary>
    /// Gets or sets the peer ID this policy applies to (optional).
    /// </summary>
    public string? PeerId { get; set; }

    /// <summary>
    /// Gets or sets the pod ID this policy applies to (optional).
    /// </summary>
    public string? PodId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether private transports (Tor/I2P) are preferred.
    /// </summary>
    public bool PreferPrivateTransports { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether clearnet transports are disabled.
    /// If true, only private transports will be allowed.
    /// </summary>
    public bool DisableClearnet { get; set; }

    /// <summary>
    /// Gets or sets the allowed transport types for this policy.
    /// If null or empty, all configured transports are allowed.
    /// </summary>
    public List<TransportType>? AllowedTransportTypes { get; set; }

    /// <summary>
    /// Gets or sets the transport preference order for this policy.
    /// Overrides the global preference order.
    /// </summary>
    public List<TransportType>? TransportPreferenceOrder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this policy is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Determines if this policy applies to the given peer and/or pod.
    /// </summary>
    /// <param name="peerId">The peer ID to check.</param>
    /// <param name="podId">The pod ID to check (optional).</param>
    /// <returns>True if this policy applies.</returns>
    public bool AppliesTo(string peerId, string? podId = null)
    {
        if (!IsEnabled)
        {
            return false;
        }

        // Check peer ID match
        if (!string.IsNullOrEmpty(PeerId) && PeerId != peerId)
        {
            return false;
        }

        // Check pod ID match (if specified)
        if (!string.IsNullOrEmpty(PodId) && PodId != podId)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the effective transport preference order for this policy.
    /// </summary>
    /// <param name="globalOrder">The global preference order as fallback.</param>
    /// <returns>The preference order to use.</returns>
    public List<TransportType> GetEffectivePreferenceOrder(List<TransportType> globalOrder)
    {
        return TransportPreferenceOrder ?? globalOrder;
    }

    /// <summary>
    /// Checks if a transport type is allowed by this policy.
    /// </summary>
    /// <param name="transportType">The transport type to check.</param>
    /// <param name="globalOptions">The global transport options.</param>
    /// <returns>True if the transport type is allowed.</returns>
    public bool IsTransportAllowed(TransportType transportType, MeshTransportOptions globalOptions)
    {
        // Check policy-specific restrictions
        if (AllowedTransportTypes != null && AllowedTransportTypes.Any())
        {
            if (!AllowedTransportTypes.Contains(transportType))
            {
                return false;
            }
        }

        // Check disable clearnet policy
        if (DisableClearnet && transportType == TransportType.DirectQuic)
        {
            return false;
        }

        // Check global configuration
        return transportType switch
        {
            TransportType.DirectQuic => globalOptions.EnableDirect,
            TransportType.TorOnionQuic => globalOptions.Tor.Enabled,
            TransportType.I2PQuic => globalOptions.I2P.Enabled,
            _ => false
        };
    }
}

/// <summary>
/// Manages transport policies for peers and pods.
/// </summary>
public class TransportPolicyManager
{
    private readonly List<TransportPolicy> _policies = new();
    private readonly object _policiesLock = new();
    private readonly ILogger<TransportPolicyManager> _logger;

    public TransportPolicyManager(ILogger<TransportPolicyManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Adds or updates a transport policy.
    /// </summary>
    /// <param name="policy">The policy to add or update.</param>
    public void AddOrUpdatePolicy(TransportPolicy policy)
    {
        lock (_policiesLock)
        {
            // Remove existing policy with same peer/pod combination
            _policies.RemoveAll(p =>
                p.PeerId == policy.PeerId &&
                p.PodId == policy.PodId);

            _policies.Add(policy);

            _logger.LogInformation("Added transport policy for peer {PeerId}, pod {PodId}: PrivatePreferred={Private}, ClearnetDisabled={Disabled}",
                policy.PeerId ?? "any", policy.PodId ?? "any", policy.PreferPrivateTransports, policy.DisableClearnet);
        }
    }

    /// <summary>
    /// Removes a transport policy.
    /// </summary>
    /// <param name="peerId">The peer ID of the policy to remove.</param>
    /// <param name="podId">The pod ID of the policy to remove (optional).</param>
    public void RemovePolicy(string peerId, string? podId = null)
    {
        lock (_policiesLock)
        {
            var removed = _policies.RemoveAll(p =>
                p.PeerId == peerId &&
                p.PodId == podId);

            if (removed > 0)
            {
                _logger.LogInformation("Removed {Count} transport policies for peer {PeerId}, pod {PodId}",
                    removed, peerId, podId ?? "any");
            }
        }
    }

    /// <summary>
    /// Gets the applicable transport policy for a peer and pod.
    /// Returns the most specific policy that applies.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="podId">The pod ID (optional).</param>
    /// <returns>The applicable policy, or null if no policy applies.</returns>
    public TransportPolicy? GetApplicablePolicy(string peerId, string? podId = null)
    {
        lock (_policiesLock)
        {
            // Find policies that apply, ordered by specificity (peer+pod > peer-only > pod-only > global)
            var applicablePolicies = _policies
                .Where(p => p.AppliesTo(peerId, podId))
                .OrderByDescending(p => GetPolicySpecificity(p))
                .ToList();

            return applicablePolicies.FirstOrDefault();
        }
    }

    /// <summary>
    /// Gets all transport policies.
    /// </summary>
    /// <returns>List of all policies.</returns>
    public List<TransportPolicy> GetAllPolicies()
    {
        lock (_policiesLock)
        {
            return _policies.ToList();
        }
    }

    private static int GetPolicySpecificity(TransportPolicy policy)
    {
        var specificity = 0;
        if (!string.IsNullOrEmpty(policy.PeerId)) specificity += 2;
        if (!string.IsNullOrEmpty(policy.PodId)) specificity += 1;
        return specificity;
    }
}

