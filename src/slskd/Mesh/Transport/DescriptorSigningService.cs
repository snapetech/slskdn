// <copyright file="DescriptorSigningService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using slskd.Mesh.Dht;

namespace slskd.Mesh.Transport;

/// <summary>
/// Service for signing and verifying mesh peer descriptors.
/// Implements anti-rollback protection, expiry validation, and cryptographic integrity.
/// </summary>
public class DescriptorSigningService
{
    private readonly ILogger<DescriptorSigningService> _logger;
    private readonly Dictionary<string, long> _lastAcceptedSequences = new();
    private readonly object _sequencesLock = new();

    public DescriptorSigningService(ILogger<DescriptorSigningService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Signs a peer descriptor with the provided private key.
    /// </summary>
    /// <param name="descriptor">The descriptor to sign.</param>
    /// <param name="privateKey">The Ed25519 private key for signing.</param>
    /// <returns>The signature as a base64 string.</returns>
    public string SignDescriptor(slskd.Mesh.Dht.MeshPeerDescriptor descriptor, byte[] privateKey)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        if (privateKey == null || privateKey.Length != 32)
        {
            throw new ArgumentException("Ed25519 private key must be 32 bytes", nameof(privateKey));
        }

        // Ensure descriptor has required fields for signing
        if (string.IsNullOrWhiteSpace(descriptor.PeerId))
        {
            throw new ArgumentException("Descriptor must have a PeerId", nameof(descriptor));
        }

        // Get the canonical data to sign
        var signableData = descriptor.GetSignableData();

        // Sign using Ed25519
        using var ed25519 = new Ed25519Signer();
        var signature = ed25519.Sign(signableData, privateKey);

        _logger.LogDebug("Signed descriptor for peer {PeerId} with sequence {Sequence}",
            descriptor.PeerId, descriptor.SequenceNumber);

        return Convert.ToBase64String(signature);
    }

    /// <summary>
    /// Verifies a descriptor signature and validates sequence/expiry.
    /// </summary>
    /// <param name="descriptor">The descriptor to verify.</param>
    /// <param name="signature">The signature as a base64 string.</param>
    /// <param name="publicKey">The Ed25519 public key for verification.</param>
    /// <returns>True if the descriptor is valid.</returns>
    public bool VerifyDescriptor(slskd.Mesh.Dht.MeshPeerDescriptor descriptor, string signature, byte[] publicKey)
    {
        if (descriptor == null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            throw new ArgumentException("Signature cannot be null or empty", nameof(signature));
        }

        if (publicKey == null || publicKey.Length != 32)
        {
            throw new ArgumentException("Ed25519 public key must be 32 bytes", nameof(publicKey));
        }

        try
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(descriptor.PeerId))
            {
                _logger.LogWarning("Descriptor verification failed: missing PeerId");
                return false;
            }

            // Check expiry
            if (descriptor.IsExpired())
            {
                _logger.LogWarning("Descriptor verification failed: descriptor expired for peer {PeerId}", descriptor.PeerId);
                return false;
            }

            // Verify sequence number (anti-rollback)
            if (!ValidateSequenceNumber(descriptor.PeerId, descriptor.SequenceNumber))
            {
                _logger.LogWarning("Descriptor verification failed: invalid sequence number {Sequence} for peer {PeerId}",
                    descriptor.SequenceNumber, descriptor.PeerId);
                return false;
            }

            // Verify cryptographic signature
            var signableData = descriptor.GetSignableData();
            var signatureBytes = Convert.FromBase64String(signature);

            using var ed25519 = new Ed25519Signer();
            var isValidSignature = ed25519.Verify(signableData, signatureBytes, publicKey);

            if (!isValidSignature)
            {
                _logger.LogWarning("Descriptor verification failed: invalid signature for peer {PeerId}", descriptor.PeerId);
                return false;
            }

            // Accept the sequence number
            AcceptSequenceNumber(descriptor.PeerId, descriptor.SequenceNumber);

            _logger.LogDebug("Descriptor verification successful for peer {PeerId} with sequence {Sequence}",
                descriptor.PeerId, descriptor.SequenceNumber);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Descriptor verification failed with exception for peer {PeerId}", descriptor.PeerId);
            return false;
        }
    }

    /// <summary>
    /// Validates a sequence number for anti-rollback protection.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="sequenceNumber">The sequence number to validate.</param>
    /// <returns>True if the sequence number is valid (not a rollback).</returns>
    public bool ValidateSequenceNumber(string peerId, long sequenceNumber)
    {
        lock (_sequencesLock)
        {
            if (_lastAcceptedSequences.TryGetValue(peerId, out var lastAccepted))
            {
                // Sequence must be greater than last accepted (strictly increasing)
                if (sequenceNumber <= lastAccepted)
                {
                    _logger.LogWarning("Sequence number rollback detected for peer {PeerId}: {NewSequence} <= {LastAccepted}",
                        peerId, sequenceNumber, lastAccepted);
                    return false;
                }
            }
            else
            {
                // First descriptor for this peer, allow any sequence >= 1
                if (sequenceNumber < 1)
                {
                    _logger.LogWarning("Invalid initial sequence number {Sequence} for peer {PeerId}",
                        sequenceNumber, peerId);
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Accepts a sequence number after successful verification.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="sequenceNumber">The accepted sequence number.</param>
    public void AcceptSequenceNumber(string peerId, long sequenceNumber)
    {
        lock (_sequencesLock)
        {
            _lastAcceptedSequences[peerId] = sequenceNumber;
            _logger.LogDebug("Accepted sequence number {Sequence} for peer {PeerId}", sequenceNumber, peerId);
        }
    }

    /// <summary>
    /// Gets the last accepted sequence number for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The last accepted sequence number, or 0 if none.</returns>
    public long GetLastAcceptedSequence(string peerId)
    {
        lock (_sequencesLock)
        {
            return _lastAcceptedSequences.TryGetValue(peerId, out var sequence) ? sequence : 0;
        }
    }

    /// <summary>
    /// Clears the sequence number state for a peer (useful for testing).
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    public void ClearSequenceState(string peerId)
    {
        lock (_sequencesLock)
        {
            _lastAcceptedSequences.Remove(peerId);
        }
    }

    /// <summary>
    /// Ed25519 signing and verification utility.
    /// </summary>
}
