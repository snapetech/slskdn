// <copyright file="MeshSecurityOptions.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

/// <summary>
/// Mesh security options (PR-01, §8). Enforce remote payload limits and safe deserialization
/// in overlay/transport and service RPC.
/// </summary>
public class MeshSecurityOptions
{
    /// <summary>
    /// When true, enforce <see cref="MaxRemotePayloadSize"/> and safe deserialization (Parse*Safely)
    /// on overlay, transport, and mesh service handlers. Default: true.
    /// </summary>
    public bool EnforceRemotePayloadLimits { get; set; } = true;

    /// <summary>
    /// Maximum size in bytes for remote JSON/MessagePack payloads. Overlay reads, DHT values,
    /// and mesh service call payloads are rejected when larger. Default: 1048576 (1 MiB).
    /// </summary>
    public int MaxRemotePayloadSize { get; set; } = 1024 * 1024;

    /// <summary>
    /// Effective max payload size: when <see cref="EnforceRemotePayloadLimits"/> is true, returns
    /// <see cref="MaxRemotePayloadSize"/>; when false, returns a relaxed cap (min of 10× that and 10 MiB).
    /// </summary>
    public int GetEffectiveMaxPayloadSize() =>
        EnforceRemotePayloadLimits
            ? MaxRemotePayloadSize
            : Math.Min(MaxRemotePayloadSize * 10, 10 * 1024 * 1024);
}
