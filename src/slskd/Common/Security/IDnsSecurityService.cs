// <copyright file="IDnsSecurityService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Common.Security;

/// <summary>
/// Abstraction for DNS resolution and validation for tunnel services.
/// Allows tests to inject a mock that controls resolution outcomes (e.g. mixed allowed/blocked IPs).
/// </summary>
public interface IDnsSecurityService
{
    /// <summary>
    /// Resolves a hostname and validates all IPs against security policies.
    /// </summary>
    Task<DnsResolutionResult> ResolveAndValidateAsync(
        string hostname,
        bool allowPrivateRanges,
        bool allowPublicDestinations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins resolved IPs for a tunnel to prevent DNS rebinding attacks.
    /// </summary>
    void PinTunnelIPs(string tunnelId, string hostname, List<string> resolvedIPs);

    /// <summary>
    /// Releases IP pinning for a tunnel.
    /// </summary>
    void ReleaseTunnelPin(string tunnelId);

    /// <summary>
    /// Validates that an IP is still allowed for an existing tunnel (rebinding protection).
    /// </summary>
    bool ValidateTunnelIP(string tunnelId, string ipString);
}
