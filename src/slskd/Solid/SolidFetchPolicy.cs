// <copyright file="SolidFetchPolicy.cs" company="slskdN Team">
// Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Solid;

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     SSRF hardening policy for WebID/Pod fetches.
/// </summary>
public sealed class SolidFetchPolicy : ISolidFetchPolicy
{
    private readonly IOptionsMonitor<slskd.Options> _options;
    private readonly ILogger<SolidFetchPolicy> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SolidFetchPolicy"/> class.
    /// </summary>
    public SolidFetchPolicy(IOptionsMonitor<slskd.Options> options, ILogger<SolidFetchPolicy> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Task ValidateAsync(Uri uri, CancellationToken ct)
    {
        var opts = _options.CurrentValue.Solid;

        if (!opts.AllowInsecureHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Solid fetch blocked: only https:// allowed.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("Solid fetch blocked: empty host.");
        }

        // Host allow-list. Empty list => deny all remote fetches.
        if (opts.AllowedHosts == null || opts.AllowedHosts.Length == 0)
        {
            throw new InvalidOperationException("Solid fetch blocked: no AllowedHosts configured.");
        }

        if (!opts.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Solid fetch blocked: host '{uri.Host}' not in AllowedHosts.");
        }

        // E2E/testing: allow localhost and loopback when explicitly enabled
        if (opts.AllowLocalhostForWebId)
        {
            return Task.CompletedTask;
        }

        // Block obvious local targets
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solid fetch blocked: localhost/.local not allowed.");
        }

        // If host is an IP literal, block private ranges
        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
            {
                throw new InvalidOperationException("Solid fetch blocked: loopback IP not allowed.");
            }

            if (IsPrivate(ip))
            {
                throw new InvalidOperationException("Solid fetch blocked: private IP not allowed.");
            }
        }

        return Task.CompletedTask;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        // IPv4 RFC1918 + link-local
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 10) return true;
            if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;
            if (b[0] == 192 && b[1] == 168) return true;
            if (b[0] == 169 && b[1] == 254) return true;
        }

        // IPv6 loopback/link-local/unique-local basic checks
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal) return true;
            var b = ip.GetAddressBytes();
            // fc00::/7 (ULA)
            if ((b[0] & 0xfe) == 0xfc) return true;
        }

        return false;
    }
}
