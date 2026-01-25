// <copyright file="ControlEnvelopeValidator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.Dht;
using slskd.Mesh.Transport;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Validates control envelopes. Extracted for testability (mock without parameterless ctor).
/// </summary>
public interface IControlEnvelopeValidator
{
    /// <summary>Validates a control envelope against a peer descriptor.</summary>
    EnvelopeValidationResult ValidateEnvelope(ControlEnvelope envelope, slskd.Mesh.Dht.MeshPeerDescriptor peerDescriptor, string peerId);
}

/// <summary>
/// Validates control envelopes with replay protection and peer-bound signature verification.
/// </summary>
public class ControlEnvelopeValidator : IControlEnvelopeValidator
{
    private readonly DescriptorSigningService _descriptorSigning;
    private readonly Transport.ConnectionThrottler _connectionThrottler;
    private readonly ReplayCache _replayCache;
    private readonly ILogger<ControlEnvelopeValidator> _logger;
    private readonly int _maxPayload;

    public ControlEnvelopeValidator(
        DescriptorSigningService descriptorSigning,
        Transport.ConnectionThrottler connectionThrottler,
        ILogger<ControlEnvelopeValidator> logger,
        IOptions<MeshOptions>? meshOptions = null)
    {
        _descriptorSigning = descriptorSigning ?? throw new ArgumentNullException(nameof(descriptorSigning));
        _connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxPayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;

        // Initialize replay cache with 5-minute TTL
        _replayCache = new ReplayCache(TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Validates a control envelope against a peer descriptor.
    /// </summary>
    /// <param name="envelope">The envelope to validate.</param>
    /// <param name="peerDescriptor">The validated peer descriptor.</param>
    /// <param name="peerId">The peer ID for replay cache.</param>
    /// <returns>Validation result with success/failure details.</returns>
    public EnvelopeValidationResult ValidateEnvelope(
        ControlEnvelope envelope,
        slskd.Mesh.Dht.MeshPeerDescriptor peerDescriptor,
        string peerId)
    {
        if (envelope == null)
        {
            return EnvelopeValidationResult.Failure("Envelope is null");
        }

        // Check envelope processing throttling
        if (!_connectionThrottler.ShouldAllowEnvelopeProcessing(peerId, envelope.Type))
        {
            _logger.LogWarning("[ControlEnvelopeValidator] Envelope processing blocked by rate limiting for peer {PeerId}, type {Type}",
                peerId, envelope.Type);
            return EnvelopeValidationResult.Failure($"Rate limit exceeded for envelope type {envelope.Type}");
        }

        if (peerDescriptor == null)
        {
            return EnvelopeValidationResult.Failure("Peer descriptor is null");
        }

        // 1. Check envelope size limits
        if (envelope.Payload.Length > _maxPayload)
        {
            return EnvelopeValidationResult.Failure($"Payload exceeds maximum size of {_maxPayload} bytes");
        }

        // 2. Validate timestamp skew
        if (!envelope.IsTimestampValid(maxSkewSeconds: 120))
        {
            return EnvelopeValidationResult.Failure("Envelope timestamp is outside acceptable skew window (±120s)");
        }

        // 3. Check replay cache
        var cacheKey = $"{peerId}:{envelope.MessageId}";
        if (_replayCache.IsReplay(cacheKey))
        {
            return EnvelopeValidationResult.Failure($"Envelope MessageId {envelope.MessageId} is a replay");
        }

        // 4. Validate signature against allowed keys from descriptor
        var signatureValid = ValidateEnvelopeSignature(envelope, peerDescriptor);
        if (!signatureValid)
        {
            return EnvelopeValidationResult.Failure("Envelope signature verification failed");
        }

        // 5. Accept the message (add to replay cache)
        _replayCache.IsReplay(cacheKey); // This adds it to cache

        _logger.LogDebug("Envelope validation successful for peer {PeerId}, message {MessageId}", peerId, envelope.MessageId);
        return EnvelopeValidationResult.Success();
    }

    /// <summary>
    /// Validates envelope signature against the descriptor's allowed signing keys.
    /// </summary>
    /// <param name="envelope">The envelope to validate.</param>
    /// <param name="peerDescriptor">The peer descriptor with allowed keys.</param>
    /// <returns>True if signature is valid.</returns>
    private bool ValidateEnvelopeSignature(ControlEnvelope envelope, slskd.Mesh.Dht.MeshPeerDescriptor peerDescriptor)
    {
        // Get the allowed signing keys from the descriptor
        var allowedKeys = peerDescriptor.ControlSigningKeys;
        if (allowedKeys == null || !allowedKeys.Any())
        {
            _logger.LogWarning("Peer descriptor has no control signing keys for peer {PeerId}",
                peerDescriptor.PeerId);
            return false;
        }

        // Try each allowed key
        foreach (var keyBase64 in allowedKeys)
        {
            try
            {
                var publicKey = Convert.FromBase64String(keyBase64);
                if (publicKey.Length != 32)
                {
                    _logger.LogWarning("Invalid control signing key length for peer {PeerId}",
                        peerDescriptor.PeerId);
                    continue;
                }

                var signatureBytes = Convert.FromBase64String(envelope.Signature);
                using var verifier = new Ed25519Signer();

                // Try canonical (GetSignableData) then legacy for backward compatibility
                if (verifier.Verify(envelope.GetSignableData(), signatureBytes, publicKey))
                {
                    return true;
                }

                if (verifier.Verify(envelope.GetLegacySignableData(), signatureBytes, publicKey))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating signature with key for peer {PeerId}",
                    peerDescriptor.PeerId);
            }
        }

        return false;
    }

    /// <summary>
    /// Gets replay cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    public int GetReplayCacheSize() => _replayCache.CacheSize;

}

/// <summary>
/// Result of envelope validation.
/// </summary>
public class EnvelopeValidationResult
{
    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid { get; private set; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    private EnvelopeValidationResult() { }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static EnvelopeValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with an error message.
    /// </summary>
    public static EnvelopeValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}
