// <copyright file="MeshDataPlane.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.DhtRendezvous;
using slskd.Mesh.Identity;

/// <summary>
/// Service for downloading file chunks over the mesh overlay network.
/// Implements range request protocol for multi-source swarm downloads.
/// </summary>
public interface IMeshDataPlane
{
    /// <summary>
    /// Downloads a chunk of a file from a mesh peer.
    /// </summary>
    /// <param name="meshPeerId">Mesh peer ID to download from</param>
    /// <param name="filename">File path/name on remote peer</param>
    /// <param name="offset">Byte offset to start reading from</param>
    /// <param name="length">Number of bytes to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chunk data</returns>
    Task<byte[]> DownloadChunkAsync(
        MeshPeerId meshPeerId,
        string filename,
        long offset,
        int length,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of mesh data plane using overlay connections.
/// </summary>
public sealed class MeshDataPlane : IMeshDataPlane
{
    private readonly ILogger<MeshDataPlane> _logger;
    private readonly MeshNeighborRegistry _neighborRegistry;
    
    public MeshDataPlane(
        ILogger<MeshDataPlane> logger,
        MeshNeighborRegistry neighborRegistry)
    {
        _logger = logger;
        _neighborRegistry = neighborRegistry;
    }
    
    /// <summary>
    /// Downloads a chunk of a file from a mesh peer.
    /// </summary>
    public async Task<byte[]> DownloadChunkAsync(
        MeshPeerId meshPeerId,
        string filename,
        long offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        // Get active overlay connection to this peer
        var connection = _neighborRegistry.GetConnectionByMeshPeerId(meshPeerId.ToString());
        
        if (connection == null || !connection.IsConnected)
        {
            throw new InvalidOperationException($"No active connection to mesh peer {meshPeerId.ToShortString()}");
        }
        
        _logger.LogDebug(
            "Requesting chunk from {MeshPeer}: file={File}, offset={Offset}, length={Length}",
            meshPeerId.ToShortString(),
            filename,
            offset,
            length);
        
        // Send chunk request message
        var request = new MeshChunkRequestMessage
        {
            RequestId = Guid.NewGuid().ToString(),
            Filename = filename,
            Offset = offset,
            Length = length,
        };
        
        await connection.WriteMessageAsync(request, cancellationToken);
        
        // Read chunk response
        var response = await connection.ReadMessageAsync<MeshChunkResponseMessage>(cancellationToken);
        
        if (!response.Success)
        {
            throw new IOException($"Chunk request failed: {response.Error}");
        }
        
        if (response.Data == null || response.Data.Length != length)
        {
            throw new IOException($"Received {response.Data?.Length ?? 0} bytes, expected {length}");
        }
        
        _logger.LogDebug(
            "Received chunk from {MeshPeer}: {Size} bytes at offset {Offset}",
            meshPeerId.ToShortString(),
            response.Data.Length,
            offset);
        
        return response.Data;
    }
}

/// <summary>
/// Request to download a chunk of a file.
/// </summary>
public sealed class MeshChunkRequestMessage
{
    public string RequestId { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public long Offset { get; set; }
    public int Length { get; set; }
}

/// <summary>
/// Response to a chunk download request.
/// </summary>
public sealed class MeshChunkResponseMessage
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public byte[]? Data { get; set; }
}
