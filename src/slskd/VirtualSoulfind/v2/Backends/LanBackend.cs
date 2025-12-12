// <copyright file="LanBackend.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.VirtualSoulfind.v2.Backends
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Sources;

    /// <summary>
    ///     Backend for LAN-based content discovery (SMB, NFS, local network shares).
    /// </summary>
    /// <remarks>
    ///     Phase 2 implementation: Local network file sharing.
    ///     Security: Private IP ranges only, no external access.
    /// </remarks>
    public sealed class LanBackend : IContentBackend
    {
        private readonly ISourceRegistry _sourceRegistry;
        private readonly IOptionsMonitor<LanBackendOptions> _options;

        public LanBackend(
            ISourceRegistry sourceRegistry,
            IOptionsMonitor<LanBackendOptions> options)
        {
            _sourceRegistry = sourceRegistry;
            _options = options;
        }

        public ContentBackendType Type => ContentBackendType.Lan;

        public ContentDomain? SupportedDomain => null; // Supports all domains

        /// <summary>
        ///     Find LAN candidates from source registry.
        /// </summary>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Array.Empty<SourceCandidate>();
            }

            var candidates = await _sourceRegistry.FindCandidatesForItemAsync(
                itemId,
                ContentBackendType.Lan,
                cancellationToken);

            // Filter by allowed network ranges
            var filtered = candidates
                .Where(c => IsAllowedLanPath(c.BackendRef, opts))
                .OrderByDescending(c => c.TrustScore)
                .ThenByDescending(c => c.ExpectedQuality)
                .Take(opts.MaxCandidatesPerItem)
                .ToList();

            return filtered;
        }

        /// <summary>
        ///     Validate LAN candidate.
        /// </summary>
        public Task<SourceCandidateValidationResult> ValidateCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate.Backend != ContentBackendType.Lan)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Not a LAN candidate"));
            }

            var opts = _options.CurrentValue;
            if (!opts.Enabled)
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("LAN backend disabled"));
            }

            // Validate path format and allowed ranges
            if (string.IsNullOrWhiteSpace(candidate.BackendRef))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Empty BackendRef"));
            }

            if (!IsAllowedLanPath(candidate.BackendRef, opts))
            {
                return Task.FromResult(SourceCandidateValidationResult.Invalid("Path not in allowed networks"));
            }

            // TODO: T-V2-P4-06 - Add actual SMB/NFS reachability check
            return Task.FromResult(SourceCandidateValidationResult.Valid(candidate.TrustScore, candidate.ExpectedQuality));
        }

        private static bool IsAllowedLanPath(string path, LanBackendOptions opts)
        {
            // UNC path format: \\hostname\share\path or smb://hostname/share/path
            if (path.StartsWith("\\\\", StringComparison.Ordinal) || 
                path.StartsWith("smb://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("nfs://", StringComparison.OrdinalIgnoreCase))
            {
                // Extract hostname/IP
                var hostname = ExtractHostname(path);
                if (string.IsNullOrEmpty(hostname))
                {
                    return false;
                }

                // If AllowedNetworks is empty, deny all
                if (opts.AllowedNetworks == null || opts.AllowedNetworks.Count == 0)
                {
                    return false;
                }

                // Check if hostname/IP is in allowed networks
                if (IPAddress.TryParse(hostname, out var ipAddress))
                {
                    return opts.AllowedNetworks.Any(range => IsIpInRange(ipAddress, range));
                }

                // If hostname (not IP), check against allowed hostnames
                return opts.AllowedHostnames?.Contains(hostname, StringComparer.OrdinalIgnoreCase) ?? false;
            }

            return false;
        }

        private static string ExtractHostname(string path)
        {
            if (path.StartsWith("\\\\", StringComparison.Ordinal))
            {
                // UNC: \\hostname\...
                var parts = path.Substring(2).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > 0 ? parts[0] : string.Empty;
            }

            if (path.StartsWith("smb://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("nfs://", StringComparison.OrdinalIgnoreCase))
            {
                // URI: smb://hostname/...
                if (Uri.TryCreate(path, UriKind.Absolute, out var uri))
                {
                    return uri.Host;
                }
            }

            return string.Empty;
        }

        private static bool IsIpInRange(IPAddress ip, string cidrRange)
        {
            // Simple CIDR check (e.g., "192.168.0.0/16")
            var parts = cidrRange.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[0], out var networkAddress))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            var ipBytes = ip.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            if (ipBytes.Length != networkBytes.Length)
            {
                return false;
            }

            var bytesToCheck = prefixLength / 8;
            var bitsToCheck = prefixLength % 8;

            for (int i = 0; i < bytesToCheck; i++)
            {
                if (ipBytes[i] != networkBytes[i])
                {
                    return false;
                }
            }

            if (bitsToCheck > 0)
            {
                var mask = (byte)(0xFF << (8 - bitsToCheck));
                if ((ipBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    ///     Configuration for LAN backend.
    /// </summary>
    public sealed class LanBackendOptions
    {
        /// <summary>
        ///     Enable LAN backend.
        /// </summary>
        public bool Enabled { get; init; } = false;

        /// <summary>
        ///     Allowed CIDR network ranges (e.g., "192.168.0.0/16", "10.0.0.0/8").
        /// </summary>
        public List<string> AllowedNetworks { get; init; } = new()
        {
            "192.168.0.0/16",
            "10.0.0.0/8",
            "172.16.0.0/12",
        };

        /// <summary>
        ///     Allowed hostnames (for non-IP UNC paths).
        /// </summary>
        public List<string> AllowedHostnames { get; init; } = new();

        /// <summary>
        ///     Maximum candidates to return per item.
        /// </summary>
        public int MaxCandidatesPerItem { get; init; } = 10;
    }
}
