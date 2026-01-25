// <copyright file="MessageSigner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Transport;

/// <summary>
/// Service for signing and verifying pod messages. PR-12: Ed25519, canonical payload, membership pubkey.
/// </summary>
public class MessageSigner : IMessageSigner
{
    private const int CanonicalVersion = 1;
    private const string SignaturePrefix = "ed25519:";
    private const long TimestampSkewMs = 5 * 60 * 1000; // 5 minutes

    private readonly ILogger<MessageSigner> _logger;
    private readonly IPodService _podService;
    private readonly Ed25519Signer _ed25519;
    private readonly IOptionsMonitor<PodMessageSignerOptions>? _options;

    private long _totalSignaturesCreated;
    private long _totalSignaturesVerified;
    private long _successfulVerifications;
    private long _failedVerifications;
    private long _totalSigningTimeMs;
    private long _totalVerificationTimeMs;
    private DateTimeOffset _lastSignatureOperation = DateTimeOffset.MinValue;

    public MessageSigner(
        ILogger<MessageSigner> logger,
        IPodService podService,
        Ed25519Signer ed25519,
        IOptionsMonitor<PodMessageSignerOptions>? options = null)
    {
        _logger = logger;
        _podService = podService ?? throw new ArgumentNullException(nameof(podService));
        _ed25519 = ed25519 ?? throw new ArgumentNullException(nameof(ed25519));
        _options = options;
    }

    private SignatureMode GetSignatureMode() => _options?.CurrentValue?.SignatureMode ?? SignatureMode.Off;

    /// <inheritdoc/>
    public async Task<PodMessage> SignMessageAsync(PodMessage message, string privateKey, CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        try
        {
            _logger.LogDebug("[MessageSigner] Signing message {MessageId}", message.MessageId);

            var data = CreateCanonicalPayload(message);
            var dataBytes = Encoding.UTF8.GetBytes(data);

            byte[] privKey;
            try
            {
                privKey = Convert.FromBase64String(privateKey);
            }
            catch
            {
                throw new ArgumentException("Private key must be valid base64.", nameof(privateKey));
            }

            if (privKey.Length != 32)
                throw new ArgumentException("Ed25519 private key must be 32 bytes.", nameof(privateKey));

            var sig = _ed25519.Sign(dataBytes, privKey);
            var sigB64 = Convert.ToBase64String(sig);

            var podId = GetPodId(message);
            var signedMessage = new PodMessage
            {
                MessageId = message.MessageId,
                PodId = podId,
                ChannelId = message.ChannelId,
                SenderPeerId = message.SenderPeerId,
                Body = message.Body,
                TimestampUnixMs = message.TimestampUnixMs,
                Signature = SignaturePrefix + sigB64,
                SigVersion = CanonicalVersion
            };

            var duration = DateTimeOffset.UtcNow - startTime;
            Interlocked.Increment(ref _totalSignaturesCreated);
            Interlocked.Add(ref _totalSigningTimeMs, (long)duration.TotalMilliseconds);
            _lastSignatureOperation = DateTimeOffset.UtcNow;

            _logger.LogTrace("[MessageSigner] Signed message {MessageId} in {Duration}ms", message.MessageId, duration.TotalMilliseconds);

            await Task.CompletedTask;
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
        var mode = GetSignatureMode();

        try
        {
            Interlocked.Increment(ref _totalSignaturesVerified);

            if (string.IsNullOrEmpty(message.Signature))
            {
                if (mode == SignatureMode.Enforce)
                {
                    _logger.LogWarning("[MessageSigner] Message {MessageId} has no signature (Enforce)", message.MessageId);
                    Interlocked.Increment(ref _failedVerifications);
                    return false;
                }
                if (mode == SignatureMode.Warn)
                    _logger.LogWarning("[MessageSigner] Message {MessageId} has no signature (Warn)", message.MessageId);
                Interlocked.Increment(ref _successfulVerifications);
                return true;
            }

            if (!message.Signature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (mode == SignatureMode.Enforce)
                {
                    Interlocked.Increment(ref _failedVerifications);
                    return false;
                }
                if (mode == SignatureMode.Warn)
                    _logger.LogWarning("[MessageSigner] Message {MessageId} has non-ed25519 signature (Warn)", message.MessageId);
                return true;
            }

            var sigB64 = message.Signature.Substring(SignaturePrefix.Length);
            byte[] sigBytes;
            try
            {
                sigBytes = Convert.FromBase64String(sigB64);
            }
            catch
            {
                _logger.LogWarning("[MessageSigner] Message {MessageId} has invalid base64 in signature", message.MessageId);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            if (sigBytes.Length != 64)
            {
                _logger.LogWarning("[MessageSigner] Message {MessageId} signature length {Length} != 64", message.MessageId, sigBytes.Length);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(now - message.TimestampUnixMs) > TimestampSkewMs)
            {
                _logger.LogWarning("[MessageSigner] Message {MessageId} timestamp skew too large", message.MessageId);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            var podId = GetPodId(message);
            if (string.IsNullOrEmpty(podId))
            {
                _logger.LogWarning("[MessageSigner] Message {MessageId} has no PodId", message.MessageId);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            var members = await _podService.GetMembersAsync(podId, cancellationToken);
            var sender = members.FirstOrDefault(m => m.PeerId == message.SenderPeerId);
            if (sender?.PublicKey == null || sender.PublicKey.Length == 0)
            {
                _logger.LogWarning("[MessageSigner] Sender {PeerId} has no PublicKey in pod {PodId}", message.SenderPeerId, podId);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            byte[] pubKey;
            try
            {
                pubKey = Convert.FromBase64String(sender.PublicKey);
            }
            catch
            {
                _logger.LogWarning("[MessageSigner] Sender {PeerId} has invalid PublicKey base64", message.SenderPeerId);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            if (pubKey.Length != 32)
            {
                _logger.LogWarning("[MessageSigner] Sender {PeerId} PublicKey length {Length} != 32", message.SenderPeerId, pubKey.Length);
                Interlocked.Increment(ref _failedVerifications);
                return false;
            }

            var data = CreateCanonicalPayload(message);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var isValid = _ed25519.Verify(dataBytes, sigBytes, pubKey);

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

            var (priv, pub) = _ed25519.GenerateKeyPair();
            var privateKey = Convert.ToBase64String(priv);
            var publicKey = Convert.ToBase64String(pub);

            _logger.LogInformation("[MessageSigner] Generated new key pair");

            await Task.CompletedTask;
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

    // PR-12 canonical: SigVersion|PodId|ChannelId|MessageId|SenderPeerId|TimestampUnixMs|BodySha256 (base64)
    private string CreateCanonicalPayload(PodMessage message)
    {
        var bodySha256 = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(message.Body ?? "")));
        var podId = GetPodId(message);
        return $"{CanonicalVersion}|{podId}|{message.ChannelId ?? ""}|{message.MessageId ?? ""}|{message.SenderPeerId ?? ""}|{message.TimestampUnixMs}|{bodySha256}";
    }

    private static string GetPodId(PodMessage message)
    {
        if (!string.IsNullOrEmpty(message.PodId))
            return message.PodId;
        if (string.IsNullOrEmpty(message.ChannelId))
            return "";
        var parts = message.ChannelId.Split(':', 2);
        return parts.Length > 0 ? (parts[0] ?? "") : "";
    }
}
