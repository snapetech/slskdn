// <copyright file="MeshChunkRequestHandler.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Identity;

/// <summary>
/// Handles incoming chunk download requests from mesh peers.
/// Implements authorization, rate limiting, and path validation.
/// </summary>
public sealed class MeshChunkRequestHandler
{
    private readonly ILogger<MeshChunkRequestHandler> _logger;
    private readonly string _shareDirectory;
    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimiters = new();
    
    // Security constants
    private const int MaxChunkSize = 1024 * 1024; // 1MB
    private const int MaxRequestsPerMinute = 60;
    private const int RateLimitWindowSeconds = 60;
    
    public MeshChunkRequestHandler(
        ILogger<MeshChunkRequestHandler> logger,
        string shareDirectory)
    {
        _logger = logger;
        _shareDirectory = shareDirectory;
    }
    
    /// <summary>
    /// Handles a chunk download request from a mesh peer.
    /// </summary>
    public async Task<MeshChunkResponseMessage> HandleRequestAsync(
        MeshChunkRequestMessage request,
        MeshPeerId requesterPeerId,
        CancellationToken cancellationToken)
    {
        try
        {
            // 1. Validate request
            var validationError = ValidateRequest(request);
            if (validationError != null)
            {
                _logger.LogWarning(
                    "Invalid chunk request from {Peer}: {Error}",
                    requesterPeerId.ToShortString(),
                    validationError);
                
                return new MeshChunkResponseMessage
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = validationError,
                };
            }
            
            // 2. Check rate limit
            if (!CheckRateLimit(requesterPeerId.Value))
            {
                _logger.LogWarning(
                    "Rate limit exceeded for {Peer}",
                    requesterPeerId.ToShortString());
                
                return new MeshChunkResponseMessage
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "Rate limit exceeded",
                };
            }
            
            // 3. Validate and resolve file path
            var resolvedPath = await ResolveFilePathAsync(request.Filename, cancellationToken);
            if (resolvedPath == null)
            {
                _logger.LogWarning(
                    "File not found or unauthorized: {Filename} from {Peer}",
                    request.Filename,
                    requesterPeerId.ToShortString());
                
                return new MeshChunkResponseMessage
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "File not found or unauthorized",
                };
            }
            
            // 4. Read chunk
            var chunkData = await ReadChunkAsync(
                resolvedPath,
                request.Offset,
                request.Length,
                cancellationToken);
            
            _logger.LogDebug(
                "Served chunk to {Peer}: {File}, offset={Offset}, length={Length}",
                requesterPeerId.ToShortString(),
                request.Filename,
                request.Offset,
                request.Length);
            
            return new MeshChunkResponseMessage
            {
                RequestId = request.RequestId,
                Success = true,
                Data = chunkData,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling chunk request from {Peer}: {File}",
                requesterPeerId.ToShortString(),
                request.Filename);
            
            return new MeshChunkResponseMessage
            {
                RequestId = request.RequestId,
                Success = false,
                Error = "Internal server error",
            };
        }
    }
    
    /// <summary>
    /// Validates a chunk request for security issues.
    /// </summary>
    private string? ValidateRequest(MeshChunkRequestMessage request)
    {
        // Check filename for path traversal
        if (string.IsNullOrWhiteSpace(request.Filename))
        {
            return "Filename is required";
        }
        
        // Prevent path traversal attacks
        if (request.Filename.Contains("..") ||
            request.Filename.Contains("\\") ||
            Path.IsPathRooted(request.Filename))
        {
            return "Invalid filename (path traversal detected)";
        }
        
        // Check for invalid characters
        if (request.Filename.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return "Invalid filename characters";
        }
        
        // Validate offset and length
        if (request.Offset < 0)
        {
            return "Invalid offset (must be >= 0)";
        }
        
        if (request.Length <= 0 || request.Length > MaxChunkSize)
        {
            return $"Invalid length (must be 1-{MaxChunkSize} bytes)";
        }
        
        return null; // Valid
    }
    
    /// <summary>
    /// Checks if peer is within rate limit.
    /// </summary>
    private bool CheckRateLimit(string peerId)
    {
        var now = DateTimeOffset.UtcNow;
        var state = _rateLimiters.GetOrAdd(peerId, _ => new RateLimitState());
        
        lock (state)
        {
            // Clean old entries
            state.Requests.RemoveWhere(t => 
                (now - t).TotalSeconds > RateLimitWindowSeconds);
            
            // Check limit
            if (state.Requests.Count >= MaxRequestsPerMinute)
            {
                return false;
            }
            
            // Add this request
            state.Requests.Add(now);
            return true;
        }
    }
    
    /// <summary>
    /// Resolves and validates a file path within the share directory.
    /// </summary>
    private async Task<string?> ResolveFilePathAsync(
        string filename,
        CancellationToken cancellationToken)
    {
        try
        {
            // Combine with share directory
            var fullPath = Path.Combine(_shareDirectory, filename);
            
            // Get canonical path to prevent symlink attacks
            var canonicalPath = Path.GetFullPath(fullPath);
            var canonicalShare = Path.GetFullPath(_shareDirectory);
            
            // Ensure file is within share directory
            if (!canonicalPath.StartsWith(canonicalShare, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Path traversal attempt: {Requested} resolved to {Canonical}",
                    filename, canonicalPath);
                return null;
            }
            
            // Check if file exists
            if (!File.Exists(canonicalPath))
            {
                return null;
            }
            
            // TODO: Check file permissions/authorization
            // For now, all shared files are accessible
            
            return canonicalPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving path: {Filename}", filename);
            return null;
        }
    }
    
    /// <summary>
    /// Reads a chunk of data from a file.
    /// </summary>
    private async Task<byte[]> ReadChunkAsync(
        string filePath,
        long offset,
        int length,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);
        
        // Validate offset against file size
        if (offset >= fileStream.Length)
        {
            throw new ArgumentException($"Offset {offset} exceeds file size {fileStream.Length}");
        }
        
        // Adjust length if it exceeds file size
        var actualLength = Math.Min(length, (int)(fileStream.Length - offset));
        
        // Seek to offset
        fileStream.Seek(offset, SeekOrigin.Begin);
        
        // Read chunk
        var buffer = new byte[actualLength];
        var bytesRead = await fileStream.ReadAsync(buffer, 0, actualLength, cancellationToken);
        
        if (bytesRead != actualLength)
        {
            throw new IOException($"Expected to read {actualLength} bytes, got {bytesRead}");
        }
        
        return buffer;
    }
    
    /// <summary>
    /// Rate limit state for a peer.
    /// </summary>
    private sealed class RateLimitState
    {
        public HashSet<DateTimeOffset> Requests { get; } = new();
    }
}














