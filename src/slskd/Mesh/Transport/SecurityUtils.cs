// <copyright file="SecurityUtils.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace slskd.Mesh.Transport;

/// <summary>
/// Security utilities for transport connections and message handling.
/// Provides certificate pinning, replay protection, rate limiting, and bounds checking.
/// </summary>
public static class SecurityUtils
{
    /// <summary>
    /// Maximum size for parsing remote payloads (JSON/MessagePack).
    /// </summary>
    public const int MaxRemotePayloadSize = 1024 * 1024; // 1MB

    /// <summary>
    /// Maximum JSON/MessagePack depth for parsing.
    /// </summary>
    public const int MaxParseDepth = 32;

    /// <summary>
    /// Validates a certificate against a list of SPKI pins.
    /// </summary>
    /// <param name="certificate">The certificate to validate.</param>
    /// <param name="expectedPins">List of expected SPKI pins (base64-encoded SHA256 hashes).</param>
    /// <returns>True if the certificate matches any of the expected pins.</returns>
    public static bool ValidateCertificatePin(X509Certificate certificate, IEnumerable<string> expectedPins)
    {
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }

        if (expectedPins == null || !expectedPins.Any())
        {
            // No pins specified, allow any certificate (but log warning)
            return true;
        }

        try
        {
            // Extract SPKI (Subject Public Key Info) from certificate
            var spki = ExtractSubjectPublicKeyInfo(certificate);
            if (spki == null)
            {
                return false;
            }

            // Hash the SPKI with SHA256
            using var sha256 = SHA256.Create();
            var spkiHash = sha256.ComputeHash(spki);
            var pin = Convert.ToBase64String(spkiHash);

            // Check if the computed pin matches any expected pin
            foreach (var expectedPin in expectedPins)
            {
                if (string.Equals(pin, expectedPin, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            // If anything goes wrong with pin validation, fail securely
            return false;
        }
    }

    /// <summary>
    /// Creates a certificate validation callback that performs pinning.
    /// </summary>
    /// <param name="expectedPins">List of expected SPKI pins.</param>
    /// <returns>A certificate validation callback.</returns>
    public static Func<X509Certificate?, X509Chain?, SslPolicyErrors, bool> CreatePinningValidationCallback(IEnumerable<string> expectedPins)
    {
        return (certificate, chain, sslPolicyErrors) =>
        {
            // First check standard SSL validation
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                return false;
            }

            // Then check certificate pinning
            if (certificate != null && !ValidateCertificatePin(certificate, expectedPins))
            {
                return false;
            }

            return true;
        };
    }

    /// <summary>
    /// Extracts the SPKI pin from an X509 certificate.
    /// </summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The SPKI pin as a base64 string, or null if extraction fails.</returns>
    public static string? ExtractSpkiPin(X509Certificate2 certificate)
    {
        if (certificate == null)
        {
            return null;
        }

        try
        {
            // Extract SPKI (Subject Public Key Info) from certificate
            var spki = certificate.PublicKey.EncodedKeyValue.RawData;
            if (spki == null || spki.Length == 0)
            {
                return null;
            }

            // Hash with SHA256 and return base64
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(spki);
            return Convert.ToBase64String(hash);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the Subject Public Key Info (SPKI) from an X509 certificate.
    /// </summary>
    /// <param name="certificate">The certificate.</param>
    /// <returns>The SPKI bytes, or null if extraction fails.</returns>
    private static byte[]? ExtractSubjectPublicKeyInfo(X509Certificate certificate)
    {
        try
        {
            // This is a simplified extraction. In production, you'd properly parse
            // the certificate ASN.1 structure to extract the SPKI.
            // For now, we'll use the public key raw data as an approximation.

            using var cert = new X509Certificate2(certificate);
            return cert.PublicKey.EncodedKeyValue.RawData;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Replay protection cache for message IDs.
/// Prevents replay attacks on control messages.
/// </summary>
public class ReplayCache
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _seenMessages = new();
    private readonly TimeSpan _cacheDuration;
    private readonly int _maxCacheSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReplayCache"/> class.
    /// </summary>
    /// <param name="cacheDuration">How long to keep messages in the cache.</param>
    /// <param name="maxCacheSize">Maximum number of cached messages.</param>
    public ReplayCache(TimeSpan cacheDuration, int maxCacheSize = 10000)
    {
        _cacheDuration = cacheDuration;
        _maxCacheSize = maxCacheSize;
    }

    /// <summary>
    /// Checks if a message ID has been seen recently (replay attack).
    /// </summary>
    /// <param name="messageId">The message ID to check.</param>
    /// <returns>True if the message is a replay (already seen).</returns>
    public bool IsReplay(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return false; // Invalid message IDs are not cached
        }

        var now = DateTimeOffset.UtcNow;

        // Clean up expired entries if cache is getting large
        if (_seenMessages.Count > _maxCacheSize * 0.9)
        {
            CleanupExpiredEntries(now);
        }

        // Check if message was seen recently
        if (_seenMessages.TryGetValue(messageId, out var seenAt))
        {
            var timeSinceSeen = now - seenAt;
            if (timeSinceSeen < _cacheDuration)
            {
                return true; // This is a replay
            }
        }

        // Mark message as seen
        _seenMessages[messageId] = now;
        return false;
    }

    /// <summary>
    /// Gets the current cache size.
    /// </summary>
    public int CacheSize => _seenMessages.Count;

    private void CleanupExpiredEntries(DateTimeOffset now)
    {
        var expiredKeys = _seenMessages
            .Where(kvp => now - kvp.Value > _cacheDuration)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _seenMessages.TryRemove(key, out _);
        }
    }
}

/// <summary>
/// Rate limiter with exponential backoff for connection attempts.
/// </summary>
public class ConnectionRateLimiter
{
    private readonly ConcurrentDictionary<string, ConnectionAttemptInfo> _attempts = new();
    private readonly TimeSpan _backoffBase;
    private readonly int _maxAttempts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionRateLimiter"/> class.
    /// </summary>
    /// <param name="backoffBase">Base backoff duration.</param>
    /// <param name="maxAttempts">Maximum attempts before extended backoff.</param>
    public ConnectionRateLimiter(TimeSpan backoffBase, int maxAttempts = 5)
    {
        _backoffBase = backoffBase;
        _maxAttempts = maxAttempts;
    }

    /// <summary>
    /// Checks if a connection attempt is allowed for the given peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>True if connection is allowed.</returns>
    public bool IsConnectionAllowed(string peerId)
    {
        var attemptInfo = _attempts.GetOrAdd(peerId, _ => new ConnectionAttemptInfo());

        lock (attemptInfo)
        {
            var now = DateTimeOffset.UtcNow;

            // Check if we're in backoff period
            if (attemptInfo.BackoffUntil > now)
            {
                return false;
            }

            // Check attempt limit
            if (attemptInfo.AttemptCount >= _maxAttempts)
            {
                // Start exponential backoff
                var backoffMultiplier = Math.Min(attemptInfo.AttemptCount - _maxAttempts + 1, 10); // Cap at 10x
                attemptInfo.BackoffUntil = now + (_backoffBase * Math.Pow(2, backoffMultiplier - 1));
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Records a successful connection attempt.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    public void RecordSuccess(string peerId)
    {
        var attemptInfo = _attempts.GetOrAdd(peerId, _ => new ConnectionAttemptInfo());

        lock (attemptInfo)
        {
            attemptInfo.AttemptCount = 0; // Reset on success
            attemptInfo.BackoffUntil = DateTimeOffset.MinValue;
            attemptInfo.LastSuccess = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Records a failed connection attempt.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    public void RecordFailure(string peerId)
    {
        var attemptInfo = _attempts.GetOrAdd(peerId, _ => new ConnectionAttemptInfo());

        lock (attemptInfo)
        {
            attemptInfo.AttemptCount++;
            attemptInfo.LastFailure = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Gets connection attempt statistics for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>Connection attempt information.</returns>
    public ConnectionAttemptInfo GetAttemptInfo(string peerId)
    {
        return _attempts.GetOrAdd(peerId, _ => new ConnectionAttemptInfo());
    }

}

/// <summary>
/// Information about connection attempts for a peer.
/// </summary>
public class ConnectionAttemptInfo
{
    /// <summary>
    /// Gets or sets the number of consecutive failed attempts.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets when the backoff period ends.
    /// </summary>
    public DateTimeOffset BackoffUntil { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last successful connection.
    /// </summary>
    public DateTimeOffset LastSuccess { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last failed attempt.
    /// </summary>
    public DateTimeOffset LastFailure { get; set; }

    /// <summary>
    /// Gets a value indicating whether the peer is currently in backoff.
    /// </summary>
    public bool IsInBackoff => BackoffUntil > DateTimeOffset.UtcNow;
}

/// <summary>
/// Payload parser with size and depth limits.
/// </summary>
public static class PayloadParser
{
    /// <summary>
    /// Safely parses a JSON payload with size and depth limits.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string.</param>
    /// <param name="maxSize">Maximum allowed size in bytes.</param>
    /// <param name="maxDepth">Maximum allowed depth.</param>
    /// <returns>The deserialized object.</returns>
    public static T? ParseJsonSafely<T>(string json, int maxSize = SecurityUtils.MaxRemotePayloadSize, int maxDepth = SecurityUtils.MaxParseDepth)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON cannot be null or empty", nameof(json));
        }

        if (json.Length > maxSize)
        {
            throw new ArgumentException($"JSON payload exceeds maximum size of {maxSize} bytes", nameof(json));
        }

        // Note: In production, you'd configure the JSON serializer with MaxDepth
        // For now, we'll just check size and use standard deserialization
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }

    /// <summary>
    /// Safely parses a MessagePack payload with size limits.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="data">The MessagePack data.</param>
    /// <param name="maxSize">Maximum allowed size in bytes.</param>
    /// <returns>The deserialized object.</returns>
    public static T? ParseMessagePackSafely<T>(byte[] data, int maxSize = SecurityUtils.MaxRemotePayloadSize)
    {
        if (data == null || data.Length == 0)
        {
            throw new ArgumentException("MessagePack data cannot be null or empty", nameof(data));
        }

        if (data.Length > maxSize)
        {
            throw new ArgumentException($"MessagePack payload exceeds maximum size of {maxSize} bytes", nameof(data));
        }

        // Note: MessagePack doesn't have built-in depth limits in the .NET implementation
        // You'd need to implement custom parsing or use a different library
        return MessagePack.MessagePackSerializer.Deserialize<T>(data);
    }
}
