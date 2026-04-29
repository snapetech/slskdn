// <copyright file="OutboundUriGuard.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.Security;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Shared SSRF guard for outbound HTTP requests to operator-configured URLs.
/// </summary>
/// <remarks>
///     Rejects any URL whose host resolves to loopback, link-local, private, multicast,
///     broadcast, cloud-metadata, or reserved addresses. Used by webhook delivery and
///     ActivityPub inbox POSTs so a malicious configuration cannot trick the daemon
///     into probing internal network services or cloud metadata endpoints.
/// </remarks>
public static class OutboundUriGuard
{
    /// <summary>
    ///     Checks whether the URL is safe to fetch. Only http/https are permitted;
    ///     the host must not be empty, and every resolved IP must be in a routable
    ///     public range.
    /// </summary>
    /// <param name="uri">The URL to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of (safe, reason). When safe is false, reason describes why.</returns>
    public static async Task<(bool Safe, string Reason)> CheckAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri == null)
        {
            return (false, "uri is null");
        }

        if (!uri.IsAbsoluteUri)
        {
            return (false, "uri is not absolute");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"scheme '{uri.Scheme}' not allowed (only http/https)");
        }

        var host = uri.DnsSafeHost?.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            return (false, "host is empty");
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"host '{host}' is a local/internal name");
        }

        if (IPAddress.TryParse(host, out var literal))
        {
            if (IpRangeClassifier.IsBlocked(literal) || IpRangeClassifier.IsPrivate(literal))
            {
                return (false, $"IP literal '{literal}' is in a non-public range");
            }

            return (true, string.Empty);
        }

        IPAddress[] addrs;
        try
        {
            addrs = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return (false, $"DNS resolution failed for '{host}': {ex.Message}");
        }

        if (addrs == null || addrs.Length == 0)
        {
            return (false, $"DNS returned no addresses for '{host}'");
        }

        foreach (var addr in addrs)
        {
            if (IpRangeClassifier.IsBlocked(addr) || IpRangeClassifier.IsPrivate(addr))
            {
                return (false, $"host '{host}' resolves to non-public address {addr}");
            }
        }

        return (true, string.Empty);
    }

    /// <summary>
    ///     Convenience wrapper returning only the boolean result.
    /// </summary>
    public static async Task<bool> IsSafeAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var (safe, _) = await CheckAsync(uri, cancellationToken).ConfigureAwait(false);
        return safe;
    }
}
