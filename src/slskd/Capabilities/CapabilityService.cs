// <copyright file="CapabilityService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.Capabilities
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Serilog;

    /// <summary>
    ///     Service for managing slskdn peer capabilities.
    /// </summary>
    public class CapabilityService : ICapabilityService
    {
        private const string SlskdnVersion = "1.0.0";
        private const int ProtocolVersion = 1;
        private const string CapabilityTagPrefix = "slskdn_caps:";
        private const string VersionPrefix = "slskdn/";

        // Our capabilities
        private static readonly PeerCapabilityFlags OurCapabilities =
            PeerCapabilityFlags.SupportsDHT |
            PeerCapabilityFlags.SupportsHashExchange |
            PeerCapabilityFlags.SupportsPartialDownload |
            PeerCapabilityFlags.SupportsMeshSync |
            PeerCapabilityFlags.SupportsFlacHashDb |
            PeerCapabilityFlags.SupportsSwarm;

        private readonly ConcurrentDictionary<string, PeerCapabilities> peerCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ILogger log = Log.ForContext<CapabilityService>();

        // Regex to parse capability tag: slskdn_caps:v1;dht=1;mesh=1;swarm=1
        private static readonly Regex CapTagRegex = new(
            @"slskdn_caps:v(\d+);?(.*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Regex to parse version string: slskdn/1.0.0+dht+mesh+swarm
        private static readonly Regex VersionRegex = new(
            @"slskdn/(\d+\.\d+\.\d+)(\+.*)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <inheritdoc/>
        public string VersionString => $"{VersionPrefix}{SlskdnVersion}+dht+mesh+swarm";

        /// <inheritdoc/>
        public string GetCapabilityTag()
        {
            var flags = new List<string>();

            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsDHT))
                flags.Add("dht=1");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsMeshSync))
                flags.Add("mesh=1");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsSwarm))
                flags.Add("swarm=1");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsHashExchange))
                flags.Add("hashx=1");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsFlacHashDb))
                flags.Add("flacdb=1");

            return $"{CapabilityTagPrefix}v{ProtocolVersion};{string.Join(";", flags)}";
        }

        /// <inheritdoc/>
        public string GetDescriptionWithCapabilities(string baseDescription)
        {
            var tag = GetCapabilityTag();

            if (string.IsNullOrWhiteSpace(baseDescription))
            {
                return tag;
            }

            // Remove any existing capability tag
            var cleaned = RemoveCapabilityTag(baseDescription);

            return $"{cleaned} | {tag}";
        }

        /// <inheritdoc/>
        public PeerCapabilities ParseCapabilityTag(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var match = CapTagRegex.Match(description);
            if (!match.Success)
            {
                return null;
            }

            var caps = new PeerCapabilities
            {
                ProtocolVersion = int.Parse(match.Groups[1].Value),
                LastCapCheck = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
            };

            var flagsStr = match.Groups[2].Value;
            if (!string.IsNullOrEmpty(flagsStr))
            {
                var parts = flagsStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && kv[1] == "1")
                    {
                        caps.Flags |= kv[0].ToLowerInvariant() switch
                        {
                            "dht" => PeerCapabilityFlags.SupportsDHT,
                            "mesh" => PeerCapabilityFlags.SupportsMeshSync,
                            "swarm" => PeerCapabilityFlags.SupportsSwarm,
                            "hashx" => PeerCapabilityFlags.SupportsHashExchange,
                            "flacdb" => PeerCapabilityFlags.SupportsFlacHashDb,
                            "partial" => PeerCapabilityFlags.SupportsPartialDownload,
                            _ => PeerCapabilityFlags.None,
                        };
                    }
                }
            }

            return caps.Flags != PeerCapabilityFlags.None ? caps : null;
        }

        /// <inheritdoc/>
        public PeerCapabilities ParseVersionString(string versionString)
        {
            if (string.IsNullOrWhiteSpace(versionString))
            {
                return null;
            }

            var match = VersionRegex.Match(versionString);
            if (!match.Success)
            {
                return null;
            }

            var caps = new PeerCapabilities
            {
                ClientVersion = match.Groups[1].Value,
                LastCapCheck = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
            };

            // Parse capability tokens (e.g., +dht+mesh+swarm)
            var tokens = match.Groups[2].Value;
            if (!string.IsNullOrEmpty(tokens))
            {
                var parts = tokens.Split('+', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    caps.Flags |= part.ToLowerInvariant() switch
                    {
                        "dht" => PeerCapabilityFlags.SupportsDHT,
                        "mesh" => PeerCapabilityFlags.SupportsMeshSync,
                        "swarm" => PeerCapabilityFlags.SupportsSwarm,
                        "hashx" => PeerCapabilityFlags.SupportsHashExchange,
                        "flacdb" => PeerCapabilityFlags.SupportsFlacHashDb,
                        "partial" => PeerCapabilityFlags.SupportsPartialDownload,
                        _ => PeerCapabilityFlags.None,
                    };
                }
            }

            return caps.Flags != PeerCapabilityFlags.None ? caps : null;
        }

        /// <inheritdoc/>
        public PeerCapabilities GetPeerCapabilities(string username)
        {
            return peerCache.TryGetValue(username, out var caps) ? caps : null;
        }

        /// <inheritdoc/>
        public void SetPeerCapabilities(string username, PeerCapabilities capabilities)
        {
            if (capabilities == null)
            {
                peerCache.TryRemove(username, out _);
                return;
            }

            capabilities.Username = username;
            capabilities.LastSeen = DateTime.UtcNow;

            peerCache.AddOrUpdate(username, capabilities, (_, existing) =>
            {
                // Merge: keep the higher capability set
                existing.Flags |= capabilities.Flags;
                existing.LastSeen = DateTime.UtcNow;
                existing.LastCapCheck = capabilities.LastCapCheck;

                if (!string.IsNullOrEmpty(capabilities.ClientVersion))
                {
                    existing.ClientVersion = capabilities.ClientVersion;
                }

                if (capabilities.MeshSeqId > existing.MeshSeqId)
                {
                    existing.MeshSeqId = capabilities.MeshSeqId;
                }

                return existing;
            });

            log.Debug("Updated capabilities for {Username}: {Flags}", username, capabilities.Flags);
        }

        /// <inheritdoc/>
        public IEnumerable<PeerCapabilities> GetAllSlskdnPeers()
        {
            return peerCache.Values
                .Where(p => p.IsSlskdnClient)
                .OrderByDescending(p => p.LastSeen)
                .ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<PeerCapabilities> GetMeshCapablePeers()
        {
            return peerCache.Values
                .Where(p => p.CanMeshSync)
                .OrderByDescending(p => p.LastSeen)
                .ToList();
        }

        /// <inheritdoc/>
        public string GetCapabilityFileContent()
        {
            var caps = new
            {
                client = "slskdn",
                version = SlskdnVersion,
                features = GetFeatureList(),
                protocol_version = ProtocolVersion,
                capabilities = (int)OurCapabilities,
                mesh_seq_id = 0L, // TODO: Get from hash DB when implemented
            };

            return JsonSerializer.Serialize(caps, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            });
        }

        private static string RemoveCapabilityTag(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            // Remove " | slskdn_caps:..." or just "slskdn_caps:..."
            var idx = description.IndexOf(CapabilityTagPrefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return description;
            }

            // Also remove the " | " separator if present
            var start = idx;
            if (start >= 3 && description.Substring(start - 3, 3) == " | ")
            {
                start -= 3;
            }

            return description.Substring(0, start).TrimEnd();
        }

        private static string[] GetFeatureList()
        {
            var features = new List<string>();

            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsDHT))
                features.Add("dht");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsHashExchange))
                features.Add("hash_exchange");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsMeshSync))
                features.Add("mesh_sync");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsFlacHashDb))
                features.Add("flac_hash_db");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsSwarm))
                features.Add("swarm_download");
            if (OurCapabilities.HasFlag(PeerCapabilityFlags.SupportsPartialDownload))
                features.Add("partial_download");

            return features.ToArray();
        }
    }
}


