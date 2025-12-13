// <copyright file="DescriptorSeqTracker.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Mesh.Security;

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tracks the last accepted DescriptorSeq per PeerId to prevent rollback attacks.
/// Persists state to disk.
/// </summary>
public interface IDescriptorSeqTracker
{
    /// <summary>
    /// Checks if a new sequence number is valid (greater than last accepted).
    /// If valid, records it as the new accepted sequence.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="newSeq">The new sequence number to validate.</param>
    /// <returns>True if newSeq > lastAcceptedSeq (or if no previous seq exists).</returns>
    bool ValidateAndUpdate(string peerId, ulong newSeq);

    /// <summary>
    /// Gets the last accepted sequence number for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The last accepted seq, or 0 if none recorded.</returns>
    ulong GetLastAcceptedSeq(string peerId);

    /// <summary>
    /// Persists current state to disk.
    /// </summary>
    void Save();
}

public class DescriptorSeqTracker : IDescriptorSeqTracker
{
    private readonly ILogger<DescriptorSeqTracker> logger;
    private readonly string persistencePath;
    private readonly ConcurrentDictionary<string, ulong> seqMap = new();
    private readonly byte[] hmacKey;

    public DescriptorSeqTracker(ILogger<DescriptorSeqTracker> logger, string persistencePath)
    {
        this.logger = logger;
        this.persistencePath = persistencePath;

        // Derive HMAC key from machine-specific data (basic anti-tampering)
        // In production, consider using DPAPI on Windows or keyring on Linux
        var machineId = Environment.MachineName + Environment.UserName + persistencePath;
        hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(machineId));

        Load();
    }

    public bool ValidateAndUpdate(string peerId, ulong newSeq)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            logger.LogWarning("[DescriptorSeqTracker] Cannot validate with null/empty peerId");
            return false;
        }

        var lastSeq = seqMap.GetOrAdd(peerId, 0);

        if (newSeq <= lastSeq)
        {
            logger.Log(
                LogLevel.Warning,
                DhtRendezvous.Security.SecurityEventIds.DescriptorRollbackDetected,
                "Rollback attack detected: peerId={PeerId}, newSeq={NewSeq}, lastSeq={LastSeq}",
                peerId,
                newSeq,
                lastSeq);
            return false;
        }

        // Update to new sequence
        seqMap[peerId] = newSeq;
        logger.LogDebug("[DescriptorSeqTracker] Accepted seq={Seq} for peerId={PeerId}", newSeq, peerId);

        // Persist asynchronously (fire-and-forget for performance)
        _ = System.Threading.Tasks.Task.Run(() => Save());

        return true;
    }

    public ulong GetLastAcceptedSeq(string peerId)
    {
        return seqMap.TryGetValue(peerId, out var seq) ? seq : 0;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(persistencePath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var data = seqMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            var json = JsonSerializer.Serialize(data);
            
            // SECURITY P1-2: Sign the data with HMAC to detect tampering
            var hmac = ComputeHmac(json);
            var signedData = new SignedSeqData
            {
                Data = json,
                Signature = Convert.ToBase64String(hmac),
                Version = 1
            };
            
            var signedJson = JsonSerializer.Serialize(signedData);
            File.WriteAllText(persistencePath, signedJson);

            logger.LogDebug("[DescriptorSeqTracker] Saved {Count} seq entries to {Path}", seqMap.Count, persistencePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DescriptorSeqTracker] Failed to save state to {Path}", persistencePath);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(persistencePath))
            {
                logger.LogInformation("[DescriptorSeqTracker] No existing state file at {Path}", persistencePath);
                return;
            }

            var signedJson = File.ReadAllText(persistencePath);
            
            // Try to load as signed data (new format)
            try
            {
                var signedData = JsonSerializer.Deserialize<SignedSeqData>(signedJson);
                
                if (signedData == null || string.IsNullOrWhiteSpace(signedData.Data) || string.IsNullOrWhiteSpace(signedData.Signature))
                {
                    throw new InvalidDataException("Invalid signed data format");
                }
                
                // SECURITY P1-2: Verify HMAC signature
                var expectedHmac = ComputeHmac(signedData.Data);
                var actualHmac = Convert.FromBase64String(signedData.Signature);
                
                if (!CryptographicOperations.FixedTimeEquals(expectedHmac, actualHmac))
                {
                    logger.Log(
                        LogLevel.Error,
                        DhtRendezvous.Security.SecurityEventIds.HmacVerificationFailed,
                        "HMAC verification failed for {Path}! File may have been tampered with. Discarding data.",
                        persistencePath);
                    return;
                }
                
                // Load the verified data
                var data = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ulong>>(signedData.Data);
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        seqMap[kvp.Key] = kvp.Value;
                    }
                    logger.LogInformation("[DescriptorSeqTracker] Loaded {Count} seq entries from {Path} (signature verified)", data.Count, persistencePath);
                }
            }
            catch (JsonException)
            {
                // Fallback: Try to load as legacy unsigned format (backward compatibility)
                logger.LogWarning("[DescriptorSeqTracker] Loading legacy unsigned format from {Path} (will upgrade on next save)", persistencePath);
                var data = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ulong>>(signedJson);
                if (data != null)
                {
                    foreach (var kvp in data)
                    {
                        seqMap[kvp.Key] = kvp.Value;
                    }
                    logger.LogInformation("[DescriptorSeqTracker] Loaded {Count} seq entries from legacy format", data.Count);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[DescriptorSeqTracker] Failed to load state from {Path}, starting fresh", persistencePath);
        }
    }
    
    private byte[] ComputeHmac(string data)
    {
        using var hmac = new HMACSHA256(hmacKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }
    
    private class SignedSeqData
    {
        public string Data { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public int Version { get; set; }
    }
}

