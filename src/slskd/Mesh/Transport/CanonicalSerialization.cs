// <copyright file="CanonicalSerialization.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using MessagePack;
using slskd.Mesh.Dht;

namespace slskd.Mesh.Transport;

/// <summary>
/// Provides canonical serialization for cryptographic operations.
/// Ensures consistent byte representation for signing and verification.
/// </summary>
public static class CanonicalSerialization
{
    /// <summary>
    /// Serializes a descriptor to canonical MessagePack bytes for signing.
    /// </summary>
    /// <param name="descriptor">The descriptor to serialize.</param>
    /// <returns>Canonical byte representation.</returns>
    public static byte[] SerializeForSigning(Dht.MeshPeerDescriptor descriptor)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        // Create a canonical DTO with fixed field ordering
        var canonicalDescriptor = new CanonicalDescriptor
        {
            PeerId = descriptor.PeerId,
            SequenceNumber = descriptor.SequenceNumber,
            ExpiresAtUnixMs = descriptor.ExpiresAtUnixMs,

            // Canonical ordering for endpoints
            Endpoints = (descriptor.Endpoints ?? Array.Empty<string>())
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToArray(),

            // Canonical ordering for transport endpoints
            TransportEndpoints = (descriptor.TransportEndpoints ?? Array.Empty<TransportEndpoint>())
                .Select(ep => new CanonicalTransportEndpoint
                {
                    TransportType = ep.TransportType,
                    Host = ep.Host,
                    Port = ep.Port,
                    Scope = ep.Scope,
                    Preference = ep.Preference,
                    Cost = ep.Cost,
                    ValidFromUnixMs = ep.ValidFromUnixMs,
                    ValidToUnixMs = ep.ValidToUnixMs
                })
                .OrderBy(ep => ep.TransportType.ToString())
                .ThenBy(ep => ep.Host)
                .ThenBy(ep => ep.Port)
                .ToArray(),

            // Canonical ordering for certificate pins
            CertificatePins = (descriptor.CertificatePins ?? Array.Empty<string>())
                .OrderBy(pin => pin, StringComparer.Ordinal)
                .ToArray(),

            // Canonical ordering for signing keys
            ControlSigningKeys = (descriptor.ControlSigningKeys ?? Array.Empty<string>())
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray(),

            // Legacy fields for backward compatibility
            NatType = descriptor.NatType,
            RelayRequired = descriptor.RelayRequired,
            TimestampUnixMs = descriptor.TimestampUnixMs
        };

        // Use MessagePack with canonical options
        var options = MessagePackSerializerOptions.Standard
            .WithSecurity(MessagePackSecurity.TrustedData) // We're controlling the input
            .WithCompression(MessagePackCompression.None); // Ensure deterministic output

        return MessagePackSerializer.Serialize(canonicalDescriptor, options);
    }

    /// <summary>
    /// Serializes a control envelope to canonical bytes for signing.
    /// </summary>
    /// <param name="envelope">The envelope to serialize.</param>
    /// <returns>Canonical byte representation.</returns>
    public static byte[] SerializeEnvelopeForSigning(Overlay.ControlEnvelope envelope)
    {
        if (envelope == null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        // Create canonical representation: Type|MessageId|Timestamp|PayloadHash
        var payloadHash = System.Security.Cryptography.SHA256.HashData(envelope.Payload);
        var canonicalString = $"{envelope.Type}|{envelope.MessageId}|{envelope.TimestampUnixMs}|{Convert.ToBase64String(payloadHash)}";

        return System.Text.Encoding.UTF8.GetBytes(canonicalString);
    }

    /// <summary>
    /// Verifies that two descriptors serialize to identical bytes.
    /// </summary>
    /// <param name="descriptor1">First descriptor.</param>
    /// <param name="descriptor2">Second descriptor.</param>
    /// <returns>True if they serialize identically.</returns>
    public static bool AreEquivalent(MeshPeerDescriptor descriptor1, MeshPeerDescriptor descriptor2)
    {
        var bytes1 = SerializeForSigning(descriptor1);
        var bytes2 = SerializeForSigning(descriptor2);

        return bytes1.SequenceEqual(bytes2);
    }
}

/// <summary>
/// Canonical representation of a mesh peer descriptor for signing.
/// Fields are ordered to ensure deterministic serialization.
/// </summary>
[MessagePackObject]
internal class CanonicalDescriptor
{
    [Key(0)] public string PeerId { get; set; } = string.Empty;
    [Key(1)] public long SequenceNumber { get; set; }
    [Key(2)] public long ExpiresAtUnixMs { get; set; }
    [Key(3)] public string[] Endpoints { get; set; } = Array.Empty<string>();
    [Key(4)] public CanonicalTransportEndpoint[] TransportEndpoints { get; set; } = Array.Empty<CanonicalTransportEndpoint>();
    [Key(5)] public string[] CertificatePins { get; set; } = Array.Empty<string>();
    [Key(6)] public string[] ControlSigningKeys { get; set; } = Array.Empty<string>();

    // Legacy fields for backward compatibility
    [Key(7)] public string? NatType { get; set; }
    [Key(8)] public bool RelayRequired { get; set; }
    [Key(9)] public long TimestampUnixMs { get; set; }
}

/// <summary>
/// Canonical representation of a transport endpoint.
/// </summary>
[MessagePackObject]
internal class CanonicalTransportEndpoint
{
    [Key(0)] public TransportType TransportType { get; set; }
    [Key(1)] public string Host { get; set; } = string.Empty;
    [Key(2)] public int Port { get; set; }
    [Key(3)] public TransportScope Scope { get; set; }
    [Key(4)] public int Preference { get; set; }
    [Key(5)] public int Cost { get; set; }
    [Key(6)] public long? ValidFromUnixMs { get; set; }
    [Key(7)] public long? ValidToUnixMs { get; set; }
}
