// <copyright file="TransportSelector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;

namespace slskd.Mesh.Transport;

/// <summary>
/// Selects and establishes transport connections based on peer descriptors and local configuration.
/// Implements the negotiation logic described in the Tor/I2P integration design.
/// </summary>
public class TransportSelector
{
    private readonly MeshTransportOptions _localOptions;
    private readonly IEnumerable<ITransportDialer> _dialers;
    private readonly TransportPolicyManager _policyManager;
    private readonly TransportDowngradeProtector _downgradeProtector;
    private readonly ConnectionThrottler _connectionThrottler;
    private readonly ILogger<TransportSelector> _logger;

    public TransportSelector(
        MeshTransportOptions localOptions,
        IEnumerable<ITransportDialer> dialers,
        TransportPolicyManager policyManager,
        TransportDowngradeProtector downgradeProtector,
        ConnectionThrottler connectionThrottler,
        ILogger<TransportSelector> logger)
    {
        _localOptions = localOptions ?? throw new ArgumentNullException(nameof(localOptions));
        _dialers = dialers ?? throw new ArgumentNullException(nameof(dialers));
        _policyManager = policyManager ?? throw new ArgumentNullException(nameof(policyManager));
        _downgradeProtector = downgradeProtector ?? throw new ArgumentNullException(nameof(downgradeProtector));
        _connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Selects the best transport endpoint and establishes a connection to the target peer.
    /// </summary>
    /// <param name="targetPeerId">The target peer ID.</param>
    /// <param name="remoteDescriptor">The remote peer's descriptor.</param>
    /// <param name="preferredScope">Preferred scope (Control or Data).</param>
    /// <param name="isolationKey">Optional isolation key for stream separation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected transport and established connection stream.</returns>
    public async Task<(ITransportDialer Transport, Stream Stream)> SelectAndConnectAsync(
        string targetPeerId,
        MeshPeerDescriptor remoteDescriptor,
        TransportScope preferredScope = TransportScope.Control,
        string? isolationKey = null,
        CancellationToken cancellationToken = default)
    {
        return await SelectAndConnectWithPinsAsync(
            targetPeerId,
            remoteDescriptor,
            remoteDescriptor.CertificatePins,
            preferredScope,
            isolationKey,
            cancellationToken);
    }

    /// <summary>
    /// Selects the best transport endpoint and establishes a connection with certificate pinning.
    /// </summary>
    /// <param name="targetPeerId">The target peer ID.</param>
    /// <param name="remoteDescriptor">The remote peer's descriptor.</param>
    /// <param name="certificatePins">Certificate pins for validation.</param>
    /// <param name="preferredScope">Preferred scope (Control or Data).</param>
    /// <param name="isolationKey">Optional isolation key for stream separation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected transport and established connection stream.</returns>
    public async Task<(ITransportDialer Transport, Stream Stream)> SelectAndConnectWithPinsAsync(
        string targetPeerId,
        MeshPeerDescriptor remoteDescriptor,
        IEnumerable<string> certificatePins,
        TransportScope preferredScope = TransportScope.Control,
        string? isolationKey = null,
        CancellationToken cancellationToken = default)
    {
        return await SelectAndConnectWithPinsAndPolicyAsync(
            targetPeerId, remoteDescriptor, certificatePins, null, preferredScope, isolationKey, cancellationToken);
    }

    /// <summary>
    /// Selects and establishes a connection with policy consideration.
    /// </summary>
    public async Task<(ITransportDialer Transport, Stream Stream)> SelectAndConnectWithPinsAndPolicyAsync(
        string targetPeerId,
        MeshPeerDescriptor remoteDescriptor,
        IEnumerable<string> certificatePins,
        string? podId,
        TransportScope preferredScope = TransportScope.Control,
        string? isolationKey = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetPeerId))
        {
            throw new ArgumentException("Target peer ID cannot be null or empty", nameof(targetPeerId));
        }

        if (remoteDescriptor == null)
        {
            throw new ArgumentNullException(nameof(remoteDescriptor));
        }

        _logger.LogDebug("Selecting transport for peer {PeerId}, pod {PodId} with policies", targetPeerId, podId ?? "none");

        // Get applicable transport policy
        var policy = _policyManager.GetApplicablePolicy(targetPeerId, podId);

        // Check fail-closed conditions
        if (ShouldFailClosed(targetPeerId, remoteDescriptor, podId, preferredScope))
        {
            throw new InvalidOperationException(
                $"Connection to peer {targetPeerId} failed closed: clearnet disabled but no private transports available");
        }

        // Check connection throttling
        var remoteEndpoint = $"{remoteDescriptor.Endpoints?.FirstOrDefault() ?? "unknown"}";
        if (!_connectionThrottler.ShouldAllowConnection(remoteEndpoint, TransportType.DirectQuic)) // Default to DirectQuic for throttling
        {
            throw new InvalidOperationException(
                $"Connection to peer {targetPeerId} blocked by rate limiting from endpoint {remoteEndpoint}");
        }

        // Get available transport endpoints for the target
        var candidates = GetCandidateEndpoints(remoteDescriptor, preferredScope, policy);
        if (!candidates.Any())
        {
            throw new InvalidOperationException($"No compatible transport endpoints found for peer {targetPeerId}");
        }

        // Determine minimum security level for downgrade protection
        var minimumSecurityLevel = _downgradeProtector.GetMinimumSecurityLevel(targetPeerId, true); // TODO: Pass actual trust status

        // Apply policy-based preference ordering
        var preferenceOrder = policy?.GetEffectivePreferenceOrder(_localOptions.PreferenceOrder) ?? _localOptions.PreferenceOrder;
        var orderedCandidates = candidates
            .OrderBy(ep => GetEndpointPreference(ep, preferenceOrder, policy?.PreferPrivateTransports ?? false))
            .ThenBy(ep => ep.Cost)
            .ToList();

        _logger.LogDebug("Trying {Count} transport candidates for peer {PeerId} (min security: {MinLevel}): {Candidates}",
            orderedCandidates.Count, targetPeerId, minimumSecurityLevel,
            string.Join(", ", orderedCandidates.Select(ep => $"{ep.TransportType}:{ep.Host}:{ep.Port}")));

        // Try each candidate in order
        foreach (var endpoint in orderedCandidates)
        {
            var dialer = GetDialerForEndpoint(endpoint);
            if (dialer == null)
            {
                _logger.LogDebug("No dialer available for {TransportType}, skipping", endpoint.TransportType);
                continue;
            }

            // Check if dialer is available
            if (!await dialer.IsAvailableAsync(cancellationToken))
            {
                _logger.LogDebug("Dialer for {TransportType} not available, skipping", endpoint.TransportType);
                continue;
            }

            try
            {
                // Check policy-based downgrade protection
                if (!IsDowngradeAllowed(endpoint.TransportType, targetPeerId, podId))
                {
                    _logger.LogWarning("Policy-based transport downgrade blocked: {TransportType} not allowed for peer {PeerId}",
                        endpoint.TransportType, targetPeerId);
                    continue; // Try next candidate
                }

                // Check security-level downgrade protection
                var downgradeValidation = _downgradeProtector.ValidateTransportSelection(
                    endpoint.TransportType, targetPeerId, minimumSecurityLevel, candidates);

                if (!downgradeValidation.IsValid)
                {
                    _logger.LogWarning("Security downgrade detected for peer {PeerId}: {Error}",
                        targetPeerId, downgradeValidation.ErrorMessage);
                    // Allow the connection but log the security concern
                    // In strict mode, this could be changed to 'continue' to block
                }

                _logger.LogDebugSafe("Attempting connection to peer {PeerId} via {Endpoint}",
                    LoggingUtils.SafePeerId(targetPeerId), LoggingUtils.SafeTransportEndpoint(endpoint));

                var stream = await dialer.DialWithPeerValidationAsync(endpoint, targetPeerId, isolationKey, cancellationToken);

                LoggingUtils.LogConnectionEstablished(_logger, targetPeerId, $"{endpoint.Host}:{endpoint.Port}", endpoint.TransportType);

                return (dialer, stream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to peer {PeerId} via {TransportType}://{Host}:{Port}",
                    targetPeerId, endpoint.TransportType, endpoint.Host, endpoint.Port);

                // Continue to next candidate
            }
        }

        throw new InvalidOperationException($"All transport candidates failed for peer {targetPeerId}");
    }
    {
        if (string.IsNullOrWhiteSpace(targetPeerId))
        {
            throw new ArgumentException("Target peer ID cannot be null or empty", nameof(targetPeerId));
        }

        if (remoteDescriptor == null)
        {
            throw new ArgumentNullException(nameof(remoteDescriptor));
        }

        _logger.LogDebug("Selecting transport for peer {PeerId} with {EndpointCount} endpoints",
            targetPeerId, remoteDescriptor.TransportEndpoints.Count);

        // Get available transport endpoints for the target
        var candidates = GetCandidateEndpoints(remoteDescriptor, preferredScope);
        if (!candidates.Any())
        {
            throw new InvalidOperationException($"No compatible transport endpoints found for peer {targetPeerId}");
        }

        // Sort candidates by preference (lower preference value = more preferred)
        var orderedCandidates = candidates
            .OrderBy(ep => ep.Preference)
            .ThenBy(ep => ep.Cost)
            .ToList();

        _logger.LogDebug("Trying {Count} transport candidates for peer {PeerId}: {Candidates}",
            orderedCandidates.Count, targetPeerId,
            string.Join(", ", orderedCandidates.Select(ep => $"{ep.TransportType}:{ep.Host}:{ep.Port}")));

        // Try each candidate in order
        foreach (var endpoint in orderedCandidates)
        {
            var dialer = GetDialerForEndpoint(endpoint);
            if (dialer == null)
            {
                _logger.LogDebug("No dialer available for {TransportType}, skipping", endpoint.TransportType);
                continue;
            }

            // Check if dialer is available
            if (!await dialer.IsAvailableAsync(cancellationToken))
            {
                _logger.LogDebug("Dialer for {TransportType} not available, skipping", endpoint.TransportType);
                continue;
            }

            try
            {
                _logger.LogInformation("Attempting connection to peer {PeerId} via {TransportType}://{Host}:{Port}",
                    targetPeerId, endpoint.TransportType, endpoint.Host, endpoint.Port);

                var stream = await dialer.DialAsync(endpoint, isolationKey, cancellationToken);

                _logger.LogInformation("Successfully connected to peer {PeerId} using {TransportType}",
                    targetPeerId, endpoint.TransportType);

                return (dialer, stream);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to peer {PeerId} via {TransportType}://{Host}:{Port}",
                    targetPeerId, endpoint.TransportType, endpoint.Host, endpoint.Port);

                // Continue to next candidate
            }
        }

        throw new InvalidOperationException($"All transport candidates failed for peer {targetPeerId}");
    }

    /// <summary>
    /// Gets the best transport endpoint for a peer without establishing a connection.
    /// </summary>
    /// <param name="remoteDescriptor">The remote peer's descriptor.</param>
    /// <param name="preferredScope">Preferred scope (Control or Data).</param>
    /// <returns>The best available endpoint, or null if none found.</returns>
    public TransportEndpoint? GetBestEndpoint(MeshPeerDescriptor remoteDescriptor, TransportScope preferredScope = TransportScope.Control)
    {
        var candidates = GetCandidateEndpoints(remoteDescriptor, preferredScope);
        return candidates
            .OrderBy(ep => ep.Preference)
            .ThenBy(ep => ep.Cost)
            .FirstOrDefault();
    }

    /// <summary>
    /// Gets all available transport endpoints for a peer that are compatible with local configuration and policy.
    /// </summary>
    /// <param name="remoteDescriptor">The remote peer's descriptor.</param>
    /// <param name="preferredScope">Preferred scope (Control or Data).</param>
    /// <param name="policy">The applicable transport policy (optional).</param>
    /// <returns>List of compatible transport endpoints.</returns>
    public List<TransportEndpoint> GetCandidateEndpoints(MeshPeerDescriptor remoteDescriptor, TransportScope preferredScope, TransportPolicy? policy = null)
    {
        if (remoteDescriptor.TransportEndpoints == null || !remoteDescriptor.TransportEndpoints.Any())
        {
            // Fallback to legacy endpoints if no transport endpoints are specified
            return GetLegacyEndpoints(remoteDescriptor);
        }

        return remoteDescriptor.TransportEndpoints
            .Where(ep => IsEndpointCompatible(ep, preferredScope, policy))
            .Where(ep => ep.IsValid()) // Check timestamp validity
            .ToList();
    }

    /// <summary>
    /// Checks if a transport endpoint is compatible with local configuration and policy.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to check.</param>
    /// <param name="preferredScope">The preferred scope.</param>
    /// <param name="policy">The applicable transport policy (optional).</param>
    /// <returns>True if the endpoint is compatible.</returns>
    public bool IsEndpointCompatible(TransportEndpoint endpoint, TransportScope preferredScope, TransportPolicy? policy = null)
    {
        // Check policy-based transport allowance first
        if (policy != null && !policy.IsTransportAllowed(endpoint.TransportType, _localOptions))
        {
            return false;
        }

        // Check if the transport type is enabled locally (fallback if no policy)
        var transportEnabled = endpoint.TransportType switch
        {
            TransportType.DirectQuic => _localOptions.EnableDirect,
            TransportType.TorOnionQuic => _localOptions.Tor.Enabled,
            TransportType.I2PQuic => _localOptions.I2P.Enabled,
            _ => false
        };

        if (!transportEnabled)
        {
            return false;
        }

        // Check if the scope is acceptable
        if ((endpoint.Scope & preferredScope) == 0)
        {
            return false;
        }

        // Additional scope-specific checks
        if (preferredScope == TransportScope.Data)
        {
            // For data plane, check additional restrictions
            if (endpoint.TransportType == TransportType.TorOnionQuic && !_localOptions.Tor.AllowDataOverTor)
            {
                return false;
            }

            if (endpoint.TransportType == TransportType.I2PQuic && !_localOptions.I2P.AllowDataOverI2p)
            {
                return false;
            }
        }

        return true;
    }

    private List<TransportEndpoint> GetLegacyEndpoints(MeshPeerDescriptor descriptor)
    {
        // Convert legacy string endpoints to TransportEndpoint objects
        // This provides backward compatibility with existing descriptors
        var endpoints = new List<TransportEndpoint>();

        if (descriptor.Endpoints != null)
        {
            foreach (var endpointStr in descriptor.Endpoints)
            {
                try
                {
                    var endpoint = ParseLegacyEndpoint(endpointStr);
                    if (endpoint != null && IsEndpointCompatible(endpoint, TransportScope.Control))
                    {
                        endpoints.Add(endpoint);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse legacy endpoint '{Endpoint}'", endpointStr);
                }
            }
        }

        return endpoints;
    }

    private TransportEndpoint? ParseLegacyEndpoint(string endpointStr)
    {
        // Parse legacy format like "quic://host:port" or "udp://host:port"
        // For now, assume QUIC endpoints are DirectQuic
        if (endpointStr.StartsWith("quic://"))
        {
            var parts = endpointStr.Substring(7).Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out var port))
            {
                return new TransportEndpoint
                {
                    TransportType = TransportType.DirectQuic,
                    Host = parts[0],
                    Port = port,
                    Scope = TransportScope.ControlAndData,
                    Preference = 0,
                    Cost = 0
                };
            }
        }

        return null;
    }

    private ITransportDialer? GetDialerForEndpoint(TransportEndpoint endpoint)
    {
        return _dialers.FirstOrDefault(d => d.CanHandle(endpoint));
    }

    private int GetEndpointPreference(TransportEndpoint endpoint, List<TransportType> preferenceOrder, bool preferPrivate)
    {
        // If private transports are preferred, boost Tor/I2P preference
        if (preferPrivate && (endpoint.TransportType == TransportType.TorOnionQuic || endpoint.TransportType == TransportType.I2PQuic))
        {
            return endpoint.Preference - 10; // Significant preference boost
        }

        // Find position in preference order
        var position = preferenceOrder.IndexOf(endpoint.TransportType);
        if (position >= 0)
        {
            return position * 10 + endpoint.Preference; // Combine order position with endpoint preference
        }

        // Fallback for unknown transport types
        return 999 + endpoint.Preference;
    }

    /// <summary>
    /// Determines if a connection should fail closed based on policy restrictions.
    /// </summary>
    /// <param name="targetPeerId">The target peer ID.</param>
    /// <param name="remoteDescriptor">The remote peer's descriptor.</param>
    /// <param name="podId">The pod ID (optional).</param>
    /// <param name="preferredScope">The preferred scope.</param>
    /// <returns>True if connection should fail closed (no compatible transports available).</returns>
    public bool ShouldFailClosed(string targetPeerId, MeshPeerDescriptor remoteDescriptor, string? podId = null, TransportScope preferredScope = TransportScope.Control)
    {
        var policy = _policyManager.GetApplicablePolicy(targetPeerId, podId);
        if (policy == null || !policy.DisableClearnet)
        {
            return false; // No restrictive policy, allow fallback
        }

        // If clearnet is disabled, check if any private transports are available
        var candidates = GetCandidateEndpoints(remoteDescriptor, preferredScope, policy);
        var privateTransports = candidates.Where(ep =>
            ep.TransportType == TransportType.TorOnionQuic ||
            ep.TransportType == TransportType.I2PQuic);

        return !privateTransports.Any();
    }

    /// <summary>
    /// Validates that a selected transport meets downgrade protection requirements.
    /// </summary>
    /// <param name="selectedTransport">The selected transport type.</param>
    /// <param name="targetPeerId">The target peer ID.</param>
    /// <param name="podId">The pod ID (optional).</param>
    /// <returns>True if the transport selection is allowed (no downgrade).</returns>
    public bool IsDowngradeAllowed(TransportType selectedTransport, string targetPeerId, string? podId = null)
    {
        var policy = _policyManager.GetApplicablePolicy(targetPeerId, podId);
        if (policy == null)
        {
            return true; // No policy restrictions
        }

        // Check if transport is explicitly allowed
        if (policy.AllowedTransportTypes != null && policy.AllowedTransportTypes.Any())
        {
            return policy.AllowedTransportTypes.Contains(selectedTransport);
        }

        // Check clearnet restrictions
        if (policy.DisableClearnet && selectedTransport == TransportType.DirectQuic)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets transport selector statistics.
    /// </summary>
    /// <returns>Statistics about all dialers.</returns>
    public Dictionary<TransportType, DialerStatistics> GetStatistics()
    {
        return _dialers.ToDictionary(
            d => d.TransportType,
            d => d.GetStatistics());
    }
}
