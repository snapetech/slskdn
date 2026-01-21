// <copyright file="MessageSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service for signing and verifying pod messages.
/// </summary>
public class MessageSigner : IMessageSigner
{
    private readonly ILogger<MessageSigner> _logger;

    // Statistics tracking
    private long _totalSignaturesCreated;
    private long _totalSignaturesVerified;
    private long _successfulVerifications;
    private long _failedVerifications;
    private long _totalSigningTimeMs;
    private long _totalVerificationTimeMs;
    private DateTimeOffset _lastSignatureOperation = DateTimeOffset.MinValue;

    public MessageSigner(ILogger<MessageSigner> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PodMessage> SignMessageAsync(PodMessage message, string privateKey, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogDebug("[MessageSigner] Signing message {MessageId}", message.MessageId);

            // Create the message data to sign
            var messageData = CreateMessageData(message);

            // Generate signature (placeholder - in production this would use Ed25519)
            var signature = await GenerateSignatureAsync(messageData, privateKey, cancellationToken);

            // Create signed message
            var signedMessage = new PodMessage
            {
                MessageId = message.MessageId,
                ChannelId = message.ChannelId,
                SenderPeerId = message.SenderPeerId,
                Body = message.Body,
                TimestampUnixMs = message.TimestampUnixMs,
                Signature = signature
            };

            var duration = DateTimeOffset.UtcNow - startTime;
            Interlocked.Increment(ref _totalSignaturesCreated);
            Interlocked.Add(ref _totalSigningTimeMs, (long)duration.TotalMilliseconds);
            _lastSignatureOperation = DateTimeOffset.UtcNow;

            _logger.LogTrace("[MessageSigner] Signed message {MessageId} in {Duration}ms", message.MessageId, duration.TotalMilliseconds);

            return signedMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageSigner] Error signing message {MessageId}", message.MessageId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyMessageAsync(PodMessage message, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            Interlocked.Increment(ref _totalSignaturesVerified);

            if (string.IsNullOrEmpty(message.Signature))
            {
                _logger.LogWarning("[MessageSigner] Message {MessageId} has no signature", message.MessageId);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            // Create the expected message data
            var messageData = CreateMessageData(message);

            // Verify signature (placeholder - in production this would verify Ed25519 signature)
            var isValid = await VerifySignatureAsync(messageData, message.Signature, message.SenderPeerId, cancellationToken);

            var duration = DateTimeOffset.UtcNow - startTime;
            Interlocked.Add(ref _totalVerificationTimeMs, (long)duration.TotalMilliseconds);

            if (isValid)
            {
                Interlocked.Increment(ref _successfulVerifications);
                _logger.LogTrace("[MessageSigner] Verified message {MessageId} in {Duration}ms", message.MessageId, duration.TotalMilliseconds);
            }
            else
            {
                Interlocked.Increment(ref _failedVerifications);
                _logger.LogWarning("[MessageSigner] Invalid signature for message {MessageId}", message.MessageId);
            }

            _lastSignatureOperation = DateTimeOffset.UtcNow;

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageSigner] Error verifying message {MessageId}", message.MessageId);
            Interlocked.Increment(ref _failedVerifications);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<KeyPair> GenerateKeyPairAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("[MessageSigner] Generating new key pair");

            // Generate Ed25519 key pair (placeholder - in production this would use real crypto)
            using var rng = RandomNumberGenerator.Create();
            var privateKeyBytes = new byte[32];
            var publicKeyBytes = new byte[32];

            rng.GetBytes(privateKeyBytes);
            rng.GetBytes(publicKeyBytes);

            var privateKey = Convert.ToBase64String(privateKeyBytes);
            var publicKey = Convert.ToBase64String(publicKeyBytes);

            _logger.LogInformation("[MessageSigner] Generated new key pair");

            return new KeyPair(publicKey, privateKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageSigner] Error generating key pair");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SignatureStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var averageSigningTime = _totalSignaturesCreated > 0
            ? (double)_totalSigningTimeMs / _totalSignaturesCreated
            : 0.0;

        var averageVerificationTime = _totalSignaturesVerified > 0
            ? (double)_totalVerificationTimeMs / _totalSignaturesVerified
            : 0.0;

        return new SignatureStats(
            TotalSignaturesCreated: _totalSignaturesCreated,
            TotalSignaturesVerified: _totalSignaturesVerified,
            SuccessfulVerifications: _successfulVerifications,
            FailedVerifications: _failedVerifications,
            AverageSigningTimeMs: averageSigningTime,
            AverageVerificationTimeMs: averageVerificationTime,
            LastSignatureOperation: _lastSignatureOperation);
    }

    // Helper methods
    private string CreateMessageData(PodMessage message)
    {
        // Create a canonical representation of the message for signing
        // This ensures the same message always produces the same signature data
        var data = $"{message.MessageId}:{message.ChannelId}:{message.SenderPeerId}:{message.Body}:{message.TimestampUnixMs}";
        return data;
    }

    private async Task<string> GenerateSignatureAsync(string messageData, string privateKey, CancellationToken cancellationToken)
    {
        try
        {
            // Placeholder signature generation
            // In production, this would use Ed25519.Sign(privateKey, messageData)
            using var sha256 = SHA256.Create();
            var dataBytes = Encoding.UTF8.GetBytes(messageData + privateKey);
            var hashBytes = sha256.ComputeHash(dataBytes);
            var signature = Convert.ToBase64String(hashBytes);

            await Task.Delay(1, cancellationToken); // Simulate crypto operation delay

            return signature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageSigner] Error generating signature");
            throw;
        }
    }

    private async Task<bool> VerifySignatureAsync(string messageData, string signature, string senderPeerId, CancellationToken cancellationToken)
    {
        try
        {
            // Placeholder signature verification
            // In production, this would verify Ed25519 signature against sender's public key
            // For now, just check that signature exists and has reasonable length
            if (string.IsNullOrEmpty(signature) || signature.Length < 10)
            {
                return false;
            }

            // Simple verification: signature should be valid base64
            try
            {
                Convert.FromBase64String(signature);
            }
            catch
            {
                return false;
            }

            await Task.Delay(1, cancellationToken); // Simulate crypto operation delay

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MessageSigner] Error verifying signature");
            return false;
        }
    }
}
