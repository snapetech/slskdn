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
        var caps = new CapabilityFileContent
        {
            Client = "slskdn",
            Version = Program.SemanticVersion,
            ProtocolVersion = 1,
            Capabilities = PeerCapabilityFlags.SupportsDHT | PeerCapabilityFlags.SupportsMeshSync | PeerCapabilityFlags.SupportsSwarm | PeerCapabilityFlags.SupportsFlacHashDb,
            Features = new[]
            {
                "dht",
                "mesh_sync",
                "swarm_download",
                "flac_hash_db",
                "multipart",
            },
            OverlayPort = 50305, // TODO: Get from config
            Timestamp = DateTimeOffset.UtcNow,
        };
        
        var json = JsonSerializer.Serialize(caps, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        
        return Encoding.UTF8.GetBytes(json);
    }
    
    /// <summary>
    /// Check if a file request is for our capability file.
    /// </summary>
    public bool IsCapabilityFileRequest(string filename)
    {
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
                
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));
                
                // Try to download the file
                var data = await DownloadSmallFileAsync(username, path, maxBytes: 4096, cts.Token);
                
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
        
        _logger.LogDebug("No capability file available from {Username}", username);
        return null;
    }
    
    /// <summary>
    /// Parse capability file JSON.
    /// </summary>
    public CapabilityFileContent? ParseCapabilityFile(byte[] data)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            return JsonSerializer.Deserialize<CapabilityFileContent>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
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
    
    private async Task<byte[]?> DownloadSmallFileAsync(
        string username,
        string filename,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        // Note: This is a simplified implementation
        // In practice, you'd use the Soulseek client's download functionality
        // with a limited byte count
        
        try
        {
            // Queue a download request for the capability file
            // The actual implementation would need to hook into the Soulseek download flow
            // For now, we return null to indicate not implemented
            // TODO: Implement via ISoulseekClient download APIs
            
            _logger.LogDebug("Capability file download not yet implemented for {Username}/{Path}",
                username, filename);
            return null;
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

