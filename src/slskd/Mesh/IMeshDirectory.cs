// <copyright file="IMeshDirectory.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

/// <summary>
/// Mesh directory for standard discovery flows (DHT-first, balanced).
/// </summary>
public interface IMeshDirectory
{
    Task<MeshPeerDescriptor?> FindPeerByIdAsync(string peerId, CancellationToken ct = default);
    Task<IReadOnlyList<MeshPeerDescriptor>> FindPeersByContentAsync(string contentId, CancellationToken ct = default);
    Task<IReadOnlyList<MeshContentDescriptor>> FindContentByPeerAsync(string peerId, CancellationToken ct = default);
}

/// <summary>
/// Descriptor for mesh peer.
/// </summary>
public record MeshPeerDescriptor(string PeerId, string? Address = null, int? Port = null, string? Transport = null);

/// <summary>
/// Descriptor for mesh content (ID, hashes, size).
/// </summary>
public record MeshContentDescriptor(string ContentId, string? Hash = null, long? SizeBytes = null, string? Codec = null);
