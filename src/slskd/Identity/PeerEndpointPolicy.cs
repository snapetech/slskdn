// <copyright file="PeerEndpointPolicy.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Identity;

using System;
using System.Net;
using slskd.Common.Security;

/// <summary>
///     HARDENING-2026-04-20 H10: PeerProfile is served anonymously via
///     <c>GET /api/v0/profile/{peerId}</c>, so any <see cref="PeerEndpoint.Address"/> entry it
///     carries becomes publicly readable. This policy rejects endpoint addresses whose host
///     component is a loopback, link-local, RFC1918, ULA, cloud-metadata, or broadcast target
///     — publishing those would silently disclose the operator's internal network topology.
/// </summary>
public static class PeerEndpointPolicy
{
    /// <summary>
    ///     Returns <c>true</c> when <paramref name="address"/> names a host that must not be
    ///     published in a public peer profile. Unparseable addresses are treated as leaky
    ///     (fail closed — we'd rather drop a malformed operator entry than serve a surprise).
    /// </summary>
    public static bool IsLeakyAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return true;
        }

        // Addresses come in several shapes: "https://host:port", "quic://host:port",
        // "relay://relayId/peerId", etc. Normalize through Uri parsing; if that fails,
        // treat it as leaky rather than passing it through.
        string host;
        try
        {
            var uri = new Uri(address, UriKind.Absolute);
            host = uri.DnsSafeHost?.Trim() ?? string.Empty;
        }
        catch (UriFormatException)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Non-IP hostname — can't classify without DNS, and blocking on DNS here would couple
        // profile writes to network availability. Accept hostname literals and trust the operator.
        if (!IPAddress.TryParse(host, out var ip))
        {
            return false;
        }

        return IpRangeClassifier.IsBlocked(ip) || IpRangeClassifier.IsPrivate(ip);
    }
}
