// <copyright file="MeshServiceDescriptorValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Security.Cryptography;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Validates mesh service descriptors for security and correctness.
/// </summary>
public interface IMeshServiceDescriptorValidator
{
    /// <summary>
    /// Validates a service descriptor.
    /// </summary>
    /// <param name="descriptor">The descriptor to validate.</param>
    /// <param name="reason">Output parameter describing why validation failed.</param>
    /// <returns>True if valid, false otherwise.</returns>
    bool Validate(MeshServiceDescriptor descriptor, out string reason);

    /// <summary>
    /// Checks if a peer is banned or quarantined.
    /// </summary>
    bool IsPeerAllowed(string peerId);
}

public class MeshServiceDescriptorValidator : IMeshServiceDescriptorValidator
{
    private readonly ILogger<MeshServiceDescriptorValidator> _logger;
    private readonly MeshServiceFabricOptions _options;
    
    // TODO: Integrate with actual SecurityCore when available
    // private readonly ISecurityCore _securityCore;

    public MeshServiceDescriptorValidator(
        ILogger<MeshServiceDescriptorValidator> logger,
        Microsoft.Extensions.Options.IOptions<MeshServiceFabricOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool Validate(MeshServiceDescriptor descriptor, out string reason)
    {
        // 1. Check required fields
        if (string.IsNullOrWhiteSpace(descriptor.ServiceId))
        {
            reason = "ServiceId is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(descriptor.ServiceName))
        {
            reason = "ServiceName is empty";
            return false;
        }

        if (string.IsNullOrWhiteSpace(descriptor.OwnerPeerId))
        {
            reason = "OwnerPeerId is empty";
            return false;
        }

        // 2. Validate ServiceId derivation
        var expectedId = MeshServiceDescriptor.DeriveServiceId(descriptor.ServiceName, descriptor.OwnerPeerId);
        if (descriptor.ServiceId != expectedId)
        {
            reason = $"ServiceId mismatch: expected {expectedId}, got {descriptor.ServiceId}";
            return false;
        }

        // 3. Check timestamps
        var now = DateTimeOffset.UtcNow;
        var skew = TimeSpan.FromSeconds(_options.MaxTimestampSkewSeconds);

        if (descriptor.CreatedAt > now.Add(skew))
        {
            reason = $"CreatedAt is in the future (skew > {_options.MaxTimestampSkewSeconds}s)";
            return false;
        }

        if (descriptor.ExpiresAt < now.Subtract(skew))
        {
            reason = $"Descriptor has expired (ExpiresAt={descriptor.ExpiresAt}, now={now})";
            return false;
        }

        if (descriptor.CreatedAt >= descriptor.ExpiresAt)
        {
            reason = "CreatedAt must be before ExpiresAt";
            return false;
        }

        // 4. Check metadata size
        if (descriptor.Metadata != null)
        {
            if (descriptor.Metadata.Count > _options.MaxMetadataEntries)
            {
                reason = $"Too many metadata entries ({descriptor.Metadata.Count} > {_options.MaxMetadataEntries})";
                return false;
            }

            // Check for PII-like patterns (basic check)
            foreach (var kvp in descriptor.Metadata)
            {
                if (kvp.Key.Contains("username", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Contains("ip", StringComparison.OrdinalIgnoreCase))
                {
                    reason = $"Metadata key '{kvp.Key}' may contain PII";
                    return false;
                }
            }
        }

        // 5. Check serialized size
        try
        {
            var serialized = MessagePackSerializer.Serialize(descriptor);
            if (serialized.Length > _options.MaxDescriptorBytes)
            {
                reason = $"Descriptor too large ({serialized.Length} > {_options.MaxDescriptorBytes} bytes)";
                return false;
            }
        }
        catch (Exception ex)
        {
            reason = $"Failed to serialize descriptor: {ex.Message}";
            return false;
        }

        // 6. Validate signature (if present)
        if (descriptor.Signature != null && descriptor.Signature.Length > 0)
        {
            if (descriptor.Signature.Length != 64) // Ed25519 signature is 64 bytes
            {
                reason = $"Invalid signature length ({descriptor.Signature.Length}, expected 64)";
                return false;
            }

            // TODO: Actually validate Ed25519 signature when crypto infrastructure is available
            // For now, just check that it exists and has correct length
            _logger.LogDebug("Signature validation skipped (crypto not yet integrated)");
        }

        // 7. Check if peer is allowed (ban list integration)
        if (!IsPeerAllowed(descriptor.OwnerPeerId))
        {
            reason = $"Peer {descriptor.OwnerPeerId} is banned or quarantined";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool IsPeerAllowed(string peerId)
    {
        // TODO: Integrate with actual SecurityCore/ban list
        // For now, just do basic validation
        if (string.IsNullOrWhiteSpace(peerId))
            return false;

        // Placeholder: In real implementation, check against ban list
        // return !_securityCore.IsBanned(peerId) && !_securityCore.IsQuarantined(peerId);
        return true;
    }
}
