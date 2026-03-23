// <copyright file="CapabilityFileService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Capabilities;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Soulseek;
using slskd.Transfers.MultiSource;

/// <summary>
/// Service for sharing and requesting capability files via Soulseek.
/// Uses virtual files at reserved paths to negotiate slskdn capabilities.
/// </summary>
public sealed class CapabilityFileService
{
    private readonly ILogger<CapabilityFileService> _logger;
    private readonly ICapabilityService _capabilityService;
    private readonly ISoulseekClient _soulseekClient;
    private readonly ConcurrentDictionary<string, CachedCapabilityFile> _cache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);

    /// <summary>
    /// Virtual file path for capability discovery.
    /// </summary>
    public const string CapabilityFilePath = "@@slskdn/__caps__.json";

    /// <summary>
    /// Alternative paths for compatibility.
    /// </summary>
    public static readonly string[] AlternativePaths = new[]
    {
        "__slskdn_caps__.json",
        "@@slskdn/capabilities.json",
        ".slskdn/caps.json",
    };

    public CapabilityFileService(
        ILogger<CapabilityFileService> logger,
        ICapabilityService capabilityService,
        ISoulseekClient soulseekClient)
    {
        _logger = logger;
        _capabilityService = capabilityService;
        _soulseekClient = soulseekClient;
    }

    /// <summary>
    /// Generate the capability file content for our client.
    /// </summary>
    public byte[] GenerateCapabilityFile()
    {
        var json = _capabilityService.GetCapabilityFileContent();
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Check if a file request is for our capability file.
    /// </summary>
    public bool IsCapabilityFileRequest(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }

        filename = NormalizeCapabilityPath(filename);
        if (string.Equals(filename, CapabilityFilePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var alt in AlternativePaths)
        {
            if (filename.EndsWith(alt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Request capability file from a peer to discover their capabilities.
    /// </summary>
    /// <param name="username">The peer's username.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parsed capabilities, or null if not available.</returns>
    public async Task<CapabilityFileContent?> RequestCapabilityFileAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        username = username.Trim();

        // Check cache first
        if (_cache.TryGetValue(username, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.FetchedAt < _cacheExpiry)
            {
                return cached.Content;
            }
            else
            {
                _cache.TryRemove(username, out _);
            }
        }

        // Try each path until one works
        var pathsToTry = new[] { CapabilityFilePath }.Concat(AlternativePaths);

        foreach (var path in pathsToTry)
        {
            try
            {
                _logger.LogDebug("Requesting capability file from {Username} at {Path}", username, path);

                var data = await DownloadSmallFileWithTimeoutAsync(username, path, 4096, TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);

                if (data is not null && data.Length > 0)
                {
                    var content = ParseCapabilityFile(data);
                    if (content is not null)
                    {
                        _logger.LogInformation("Got capability file from {Username}: {Client} v{Version}",
                            username, content.Client, content.Version);

                        // Cache the result
                        _cache[username] = new CachedCapabilityFile
                        {
                            Content = content,
                            FetchedAt = DateTimeOffset.UtcNow,
                        };

                        // Also update the capability service
                        var peerCaps = new PeerCapabilities
                        {
                            Username = username,
                            Flags = content.Capabilities,
                            ClientVersion = $"{content.Client}/{content.Version}",
                            ProtocolVersion = content.ProtocolVersion,
                        };
                        _capabilityService.SetPeerCapabilities(username, peerCaps);

                        return content;
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Failed to get capability file from {Username} at {Path}", username, path);
            }
        }

        try
        {
            var userInfo = await GetUserInfoWithTimeoutAsync(username, TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            var parsedCaps = _capabilityService.ParseCapabilityTag(userInfo?.Description ?? string.Empty);
            if (parsedCaps != null)
            {
                parsedCaps.Username = username;
                _capabilityService.SetPeerCapabilities(username, parsedCaps);

                var fallback = BuildCapabilityFileFromPeerCapabilities(parsedCaps);
                _cache[username] = new CachedCapabilityFile
                {
                    Content = fallback,
                    FetchedAt = DateTimeOffset.UtcNow,
                };

                _logger.LogInformation("Recovered capabilities for {Username} from UserInfo description tags", username);
                return fallback;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Failed to recover capabilities from UserInfo for {Username}", username);
        }

        _logger.LogDebug("No capability file available from {Username}", username);
        return null;
    }

    private async Task<byte[]?> DownloadSmallFileWithTimeoutAsync(
        string username,
        string filename,
        int maxBytes,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return await DownloadSmallFileAsync(username, filename, maxBytes, cts.Token).ConfigureAwait(false);
    }

    private async Task<UserInfo?> GetUserInfoWithTimeoutAsync(
        string username,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return await _soulseekClient.GetUserInfoAsync(username, cts.Token).ConfigureAwait(false);
    }

    /// <summary>
    /// Parse capability file JSON.
    /// </summary>
    public CapabilityFileContent? ParseCapabilityFile(byte[] data)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            var content = JsonSerializer.Deserialize<CapabilityFileContent>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (content != null)
            {
                content.Client = content.Client?.Trim() ?? string.Empty;
                content.Version = content.Version?.Trim() ?? string.Empty;
                content.Features = content.Features?
                    .Where(feature => !string.IsNullOrWhiteSpace(feature))
                    .Select(feature => feature.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray() ?? Array.Empty<string>();
                if (content.Capabilities == PeerCapabilityFlags.None && content.Features.Length > 0)
                {
                    content.Capabilities = ParseCapabilitiesFromFeatures(content.Features);
                }
            }

            if (content == null ||
                string.IsNullOrWhiteSpace(content.Client) ||
                string.IsNullOrWhiteSpace(content.Version) ||
                content.ProtocolVersion <= 0)
            {
                _logger.LogDebug("Rejected malformed capability file");
                return null;
            }

            if (content.OverlayPort < 0 || content.OverlayPort > 65535)
            {
                _logger.LogDebug("Rejected capability file with invalid overlay port {Port}", content.OverlayPort);
                return null;
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse capability file");
            return null;
        }
    }

    /// <summary>
    /// Clear cached capabilities for a user.
    /// </summary>
    public void ClearCache(string username)
    {
        _cache.TryRemove(username, out _);
    }

    /// <summary>
    /// Clear all cached capabilities.
    /// </summary>
    public void ClearAllCache()
    {
        _cache.Clear();
    }

    private static CapabilityFileContent BuildCapabilityFileFromPeerCapabilities(PeerCapabilities capabilities)
    {
        var (client, version) = ParseClientIdentity(capabilities.ClientVersion);
        return new CapabilityFileContent
        {
            Client = client,
            Version = version,
            ProtocolVersion = capabilities.ProtocolVersion,
            Capabilities = capabilities.Flags,
            Features = GetFeatures(capabilities.Flags),
            OverlayPort = 0,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }

    private static string[] GetFeatures(PeerCapabilityFlags flags)
    {
        var features = new List<string>();

        if (flags.HasFlag(PeerCapabilityFlags.SupportsDHT))
            features.Add("dht");
        if (flags.HasFlag(PeerCapabilityFlags.SupportsHashExchange))
            features.Add("hash_exchange");
        if (flags.HasFlag(PeerCapabilityFlags.SupportsPartialDownload))
            features.Add("partial_download");
        if (flags.HasFlag(PeerCapabilityFlags.SupportsMeshSync))
            features.Add("mesh_sync");
        if (flags.HasFlag(PeerCapabilityFlags.SupportsFlacHashDb))
            features.Add("flac_hash_db");
        if (flags.HasFlag(PeerCapabilityFlags.SupportsSwarm))
            features.Add("swarm_download");

        return features.ToArray();
    }

    private async Task<byte[]?> DownloadSmallFileAsync(
        string username,
        string filename,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            var browseResult = await _soulseekClient.BrowseAsync(username, cancellationToken: cancellationToken).ConfigureAwait(false);
            var normalizedFilename = NormalizeCapabilityPath(filename);
            var remoteFile = browseResult.Directories
                .SelectMany(directory => directory.Files.Select(file => new
                {
                    RemoteFilename = NormalizeCapabilityPath(string.IsNullOrWhiteSpace(directory.Name)
                        ? file.Filename
                        : $"{directory.Name}\\{file.Filename}"),
                    file.Size,
                }))
                .FirstOrDefault(file =>
                    string.Equals(file.RemoteFilename, normalizedFilename, StringComparison.OrdinalIgnoreCase) ||
                    file.RemoteFilename.EndsWith(normalizedFilename, StringComparison.OrdinalIgnoreCase));
            if (remoteFile == null)
            {
                _logger.LogDebug("Capability file {Path} not exposed by {Username} browse result", filename, username);
                return null;
            }

            if (remoteFile.Size <= 0 || remoteFile.Size > maxBytes)
            {
                _logger.LogDebug("Capability file {Path} from {Username} has unsupported size {Size}", filename, username, remoteFile.Size);
                return null;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var memoryStream = new MemoryStream((int)remoteFile.Size);
            using var limitedStream = new LimitedWriteStream(memoryStream, maxBytes, linkedCts);

            try
            {
                await _soulseekClient.DownloadAsync(
                    username: username,
                    remoteFilename: remoteFile.RemoteFilename,
                    outputStreamFactory: () => Task.FromResult<Stream>(limitedStream),
                    size: remoteFile.Size,
                    startOffset: 0,
                    cancellationToken: linkedCts.Token,
                    options: new TransferOptions(
                        maximumLingerTime: 1000,
                        disposeOutputStreamOnCompletion: false)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (limitedStream.LimitReached)
            {
                _logger.LogDebug("Capability file download from {Username} reached size limit after {Bytes} bytes", username, limitedStream.BytesWritten);
            }

            var bytes = memoryStream.ToArray();
            return bytes.Length == 0 ? null : bytes;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error downloading capability file");
            return null;
        }
    }

    private sealed class CachedCapabilityFile
    {
        public required CapabilityFileContent Content { get; init; }
        public required DateTimeOffset FetchedAt { get; init; }
    }

    private static PeerCapabilityFlags ParseCapabilitiesFromFeatures(IEnumerable<string> features)
    {
        var flags = PeerCapabilityFlags.None;
        foreach (var feature in features)
        {
            switch (feature.Trim().ToLowerInvariant())
            {
                case "dht":
                    flags |= PeerCapabilityFlags.SupportsDHT;
                    break;
                case "hash_exchange":
                    flags |= PeerCapabilityFlags.SupportsHashExchange;
                    break;
                case "partial_download":
                    flags |= PeerCapabilityFlags.SupportsPartialDownload;
                    break;
                case "mesh_sync":
                    flags |= PeerCapabilityFlags.SupportsMeshSync;
                    break;
                case "flac_hash_db":
                    flags |= PeerCapabilityFlags.SupportsFlacHashDb;
                    break;
                case "swarm_download":
                    flags |= PeerCapabilityFlags.SupportsSwarm;
                    break;
            }
        }

        return flags;
    }

    private static string NormalizeCapabilityPath(string path)
    {
        return path.Trim().Replace('\\', '/');
    }

    private static (string Client, string Version) ParseClientIdentity(string? clientVersion)
    {
        if (string.IsNullOrWhiteSpace(clientVersion))
        {
            return ("slskdn", "unknown");
        }

        clientVersion = clientVersion.Trim();
        var separator = clientVersion.IndexOf('/');
        if (separator > 0 && separator < clientVersion.Length - 1)
        {
            return (clientVersion[..separator].Trim(), clientVersion[(separator + 1)..].Trim());
        }

        return ("slskdn", clientVersion);
    }
}

/// <summary>
/// Content of the capability file.
/// </summary>
public sealed class CapabilityFileContent
{
    /// <summary>Client name (e.g., "slskdn").</summary>
    public string Client { get; set; } = "slskdn";

    /// <summary>Client version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Capability protocol version.</summary>
    public int ProtocolVersion { get; set; } = 1;

    /// <summary>Capability flags.</summary>
    public PeerCapabilityFlags Capabilities { get; set; }

    /// <summary>List of supported feature names.</summary>
    public string[] Features { get; set; } = Array.Empty<string>();

    /// <summary>Overlay port for mesh connections.</summary>
    public int OverlayPort { get; set; }

    /// <summary>When this file was generated.</summary>
    public DateTimeOffset Timestamp { get; set; }
}
