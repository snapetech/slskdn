using MessagePack;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Describes a service instance available on the mesh overlay.
/// </summary>
[MessagePackObject]
public sealed record MeshServiceDescriptor
{
    /// <summary>
    /// Deterministic service ID: hash("svc:" + ServiceName + ":" + OwnerPeerId).
    /// </summary>
    [Key(0)]
    public string ServiceId { get; init; } = string.Empty;

    /// <summary>
    /// Stable functional service name (e.g., "pods", "shadow-index", "mesh-introspect").
    /// Must not contain PII.
    /// </summary>
    [Key(1)]
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Service version (semver format).
    /// </summary>
    [Key(2)]
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Peer ID of the node hosting this service.
    /// </summary>
    [Key(3)]
    public string OwnerPeerId { get; init; } = string.Empty;

    /// <summary>
    /// Endpoint for accessing this service.
    /// </summary>
    [Key(4)]
    public MeshServiceEndpoint Endpoint { get; init; } = new();

    /// <summary>
    /// Optional metadata (max 10 entries, max 4KB total serialized size).
    /// Must not contain PII.
    /// </summary>
    [Key(5)]
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// UTC timestamp when this descriptor was created.
    /// </summary>
    [Key(6)]
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// UTC timestamp when this descriptor expires.
    /// </summary>
    [Key(7)]
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Ed25519 signature of the descriptor content, signed by owner's key.
    /// </summary>
    [Key(8)]
    public byte[] Signature { get; init; } = Array.Empty<byte>();

    /// <summary>
    /// Derives a deterministic ServiceId from the service name and owner peer ID.
    /// </summary>
    public static string DeriveServiceId(string serviceName, string ownerPeerId)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Service name cannot be empty", nameof(serviceName));
        if (string.IsNullOrWhiteSpace(ownerPeerId))
            throw new ArgumentException("Owner peer ID cannot be empty", nameof(ownerPeerId));

        var input = $"svc:{serviceName}:{ownerPeerId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Creates the canonical byte representation of this descriptor for signing/validation.
    /// </summary>
    public byte[] GetBytesForSigning()
    {
        // Serialize all fields except Signature for canonical representation
        var sb = new StringBuilder();
        sb.Append(ServiceId);
        sb.Append('|');
        sb.Append(ServiceName);
        sb.Append('|');
        sb.Append(Version);
        sb.Append('|');
        sb.Append(OwnerPeerId);
        sb.Append('|');
        sb.Append(Endpoint.ToString());
        sb.Append('|');
        
        // Metadata in sorted order for deterministic output
        if (Metadata != null && Metadata.Count > 0)
        {
            var sorted = Metadata.OrderBy(kvp => kvp.Key);
            foreach (var kvp in sorted)
            {
                sb.Append(kvp.Key);
                sb.Append('=');
                sb.Append(kvp.Value);
                sb.Append(';');
            }
        }
        
        sb.Append('|');
        sb.Append(CreatedAt.ToUnixTimeSeconds());
        sb.Append('|');
        sb.Append(ExpiresAt.ToUnixTimeSeconds());

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

/// <summary>
/// Represents the network endpoint for a mesh service.
/// </summary>
[MessagePackObject]
public sealed record MeshServiceEndpoint
{
    /// <summary>
    /// Protocol for accessing the service (e.g., "quic", "udp").
    /// </summary>
    [Key(0)]
    public string Protocol { get; init; } = "quic";

    /// <summary>
    /// Host address or overlay node ID.
    /// </summary>
    [Key(1)]
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Port number (0 = use default for protocol).
    /// </summary>
    [Key(2)]
    public int Port { get; init; }

    /// <summary>
    /// Optional path component.
    /// </summary>
    [Key(3)]
    public string Path { get; init; } = string.Empty;

    public override string ToString()
    {
        var portPart = Port > 0 ? $":{Port}" : "";
        var pathPart = !string.IsNullOrEmpty(Path) ? $"/{Path}" : "";
        return $"{Protocol}://{Host}{portPart}{pathPart}";
    }
}
