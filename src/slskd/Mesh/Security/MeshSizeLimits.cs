// <copyright file="MeshSizeLimits.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.Overlay;

/// <summary>
/// Enforces strict size limits before MessagePack deserialization to prevent DoS attacks.
/// </summary>
public static class MeshSizeLimits
{
    // Size limits (bytes)
    public const int MaxControlEnvelopeBytes = 64 * 1024; // 64 KB
    public const int MaxDescriptorBytes = 128 * 1024; // 128 KB
    public const int MaxEndpoints = 10;
    public const int MaxControlSigningKeys = 3;
    public const int MaxTlsPins = 2;

    /// <summary>
    /// Safely deserializes a ControlEnvelope with size validation.
    /// </summary>
    public static bool TryDeserializeControlEnvelope(
        byte[] data,
        ILogger logger,
        out ControlEnvelope? envelope)
    {
        envelope = null;

        if (data.Length > MaxControlEnvelopeBytes)
        {
            logger.LogWarning("[MeshSizeLimits] Control envelope exceeds max size: {Size} bytes", data.Length);
            return false;
        }

        try
        {
            envelope = MessagePackSerializer.Deserialize<ControlEnvelope>(data);

            // Validate sub-field sizes
            if (envelope.Payload.Length > MaxControlEnvelopeBytes / 2)
            {
                logger.LogWarning("[MeshSizeLimits] Envelope payload too large: {Size} bytes", envelope.Payload.Length);
                envelope = null;
                return false;
            }

            return true;
        }
        catch (MessagePackSerializationException ex)
        {
            logger.LogWarning(ex, "[MeshSizeLimits] Failed to deserialize control envelope");
            return false;
        }
    }

    /// <summary>
    /// Safely deserializes a MeshPeerDescriptor with size and field count validation.
    /// </summary>
    public static bool TryDeserializeDescriptor(
        byte[] data,
        ILogger logger,
        out MeshPeerDescriptor? descriptor)
    {
        descriptor = null;

        if (data.Length > MaxDescriptorBytes)
        {
            logger.LogWarning("[MeshSizeLimits] Descriptor exceeds max size: {Size} bytes", data.Length);
            return false;
        }

        try
        {
            descriptor = MessagePackSerializer.Deserialize<MeshPeerDescriptor>(data);

            // Validate field counts
            if (descriptor.Endpoints.Count > MaxEndpoints)
            {
                logger.LogWarning("[MeshSizeLimits] Too many endpoints: {Count}", descriptor.Endpoints.Count);
                descriptor = null;
                return false;
            }

            if (descriptor.ControlSigningKeys.Count > MaxControlSigningKeys)
            {
                logger.LogWarning("[MeshSizeLimits] Too many control signing keys: {Count}", descriptor.ControlSigningKeys.Count);
                descriptor = null;
                return false;
            }

            if (descriptor.TlsControlPins.Count > MaxTlsPins || descriptor.TlsDataPins.Count > MaxTlsPins)
            {
                logger.LogWarning("[MeshSizeLimits] Too many TLS pins");
                descriptor = null;
                return false;
            }

            return true;
        }
        catch (MessagePackSerializationException ex)
        {
            logger.LogWarning(ex, "[MeshSizeLimits] Failed to deserialize descriptor");
            return false;
        }
    }
}

