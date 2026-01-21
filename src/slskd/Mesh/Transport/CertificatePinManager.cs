// <copyright file="CertificatePinManager.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

namespace slskd.Mesh.Transport;

/// <summary>
/// Manages SPKI certificate pins for peer identity verification.
/// Provides pin persistence, rotation, and validation services.
/// </summary>
public class CertificatePinManager
{
    private readonly ConcurrentDictionary<string, PeerCertificateInfo> _peerCertificates = new();
    private readonly ILogger<CertificatePinManager> _logger;
    private readonly string _pinStoragePath;

    public CertificatePinManager(ILogger<CertificatePinManager> logger, IOptions<MeshOptions> meshOptions)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Store pins in the mesh data directory
        var meshDataPath = Path.Combine(meshOptions.Value.DataDirectory ?? "data", "mesh");
        _pinStoragePath = Path.Combine(meshDataPath, "certificate-pins.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_pinStoragePath)!);
        LoadPersistedPins();
    }

    /// <summary>
    /// Validates a certificate against stored pins for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="certificate">The certificate to validate.</param>
    /// <returns>True if the certificate is pinned and valid.</returns>
    public bool ValidateCertificatePin(string peerId, X509Certificate2 certificate)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            throw new ArgumentException("Peer ID cannot be null or empty", nameof(peerId));
        }

        if (certificate == null)
        {
            return false;
        }

        var certInfo = _peerCertificates.GetOrAdd(peerId, _ => new PeerCertificateInfo(peerId));

        lock (certInfo)
        {
            // Extract current SPKI pin
            var currentPin = SecurityUtils.ExtractSpkiPin(certificate);
            if (string.IsNullOrEmpty(currentPin))
            {
                _logger.LogWarning("Failed to extract SPKI pin from certificate for peer {PeerId}", peerId);
                return false;
            }

            // Check if this is a new peer (no pins stored yet)
            if (!certInfo.HasAnyPins())
            {
                // For new peers, accept the certificate and pin it
                AddPin(peerId, currentPin, CertificatePinType.Current);
                        _logger.LogDebugSafe("Pinned new certificate for peer {PeerId}: {Pin}", LoggingUtils.SafePeerId(peerId), "[redacted]");
                return true;
            }

            // Check current pins
            if (certInfo.CurrentPins.Contains(currentPin))
            {
                certInfo.LastValidation = DateTimeOffset.UtcNow;
                return true;
            }

            // Check previous pins (allow during transition period)
            if (certInfo.PreviousPins.Contains(currentPin))
            {
                var timeSinceRotation = DateTimeOffset.UtcNow - certInfo.LastRotation;
                if (timeSinceRotation < TimeSpan.FromDays(30)) // Allow previous pins for 30 days
                {
                    _logger.LogInformation("Accepted previous pin for peer {PeerId} during transition period", peerId);
                    certInfo.LastValidation = DateTimeOffset.UtcNow;
                    return true;
                }
                else
                {
                    _logger.LogWarning("Previous pin expired for peer {PeerId}", peerId);
                    return false;
                }
            }

            // Pin mismatch - potential MITM attack
                    _logger.LogWarning("Certificate pin mismatch for peer {PeerId}. Pin validation failed.",
                LoggingUtils.SafePeerId(peerId));
            return false;
        }
    }

    /// <summary>
    /// Adds or updates a certificate pin for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="pin">The SPKI pin to add.</param>
    /// <param name="pinType">The type of pin (current or previous).</param>
    public void AddPin(string peerId, string pin, CertificatePinType pinType = CertificatePinType.Current)
    {
        if (string.IsNullOrWhiteSpace(peerId))
        {
            throw new ArgumentException("Peer ID cannot be null or empty", nameof(peerId));
        }

        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("Pin cannot be null or empty", nameof(pin));
        }

        var certInfo = _peerCertificates.GetOrAdd(peerId, _ => new PeerCertificateInfo(peerId));

        lock (certInfo)
        {
            switch (pinType)
            {
                case CertificatePinType.Current:
                    if (!certInfo.CurrentPins.Contains(pin))
                    {
                        // Move existing current pins to previous
                        certInfo.PreviousPins.UnionWith(certInfo.CurrentPins);
                        certInfo.CurrentPins.Clear();
                        certInfo.CurrentPins.Add(pin);
                        certInfo.LastRotation = DateTimeOffset.UtcNow;

                        _logger.LogInformation("Updated current pin for peer {PeerId}: {Pin}", peerId, pin);
                    }
                    break;

                case CertificatePinType.Previous:
                    certInfo.PreviousPins.Add(pin);
                    _logger.LogDebug("Added previous pin for peer {PeerId}: {Pin}", peerId, pin);
                    break;
            }

            PersistPins();
        }
    }

    /// <summary>
    /// Rotates pins for a peer when their certificate changes.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <param name="newPin">The new certificate pin.</param>
    public void RotatePin(string peerId, string newPin)
    {
        AddPin(peerId, newPin, CertificatePinType.Current);
    }

    /// <summary>
    /// Gets the certificate information for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    /// <returns>The peer's certificate information, or null if not found.</returns>
    public PeerCertificateInfo? GetPeerCertificateInfo(string peerId)
    {
        _peerCertificates.TryGetValue(peerId, out var certInfo);
        return certInfo;
    }

    /// <summary>
    /// Removes all pins for a peer.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    public void RemovePeerPins(string peerId)
    {
        if (_peerCertificates.TryRemove(peerId, out _))
        {
            _logger.LogInformation("Removed all certificate pins for peer {PeerId}", peerId);
            PersistPins();
        }
    }

    /// <summary>
    /// Cleans up expired previous pins.
    /// </summary>
    public void CleanupExpiredPins()
    {
        var expiredPeers = new List<string>();
        var cutoffDate = DateTimeOffset.UtcNow - TimeSpan.FromDays(30);

        foreach (var kvp in _peerCertificates)
        {
            var certInfo = kvp.Value;
            lock (certInfo)
            {
                if (certInfo.LastRotation < cutoffDate && certInfo.PreviousPins.Any())
                {
                    certInfo.PreviousPins.Clear();
                    _logger.LogDebug("Cleaned up expired previous pins for peer {PeerId}", kvp.Key);
                }

                // Remove peers with no pins and no recent activity
                if (!certInfo.HasAnyPins() &&
                    certInfo.LastValidation < DateTimeOffset.UtcNow - TimeSpan.FromDays(90))
                {
                    expiredPeers.Add(kvp.Key);
                }
            }
        }

        foreach (var peerId in expiredPeers)
        {
            _peerCertificates.TryRemove(peerId, out _);
            _logger.LogDebug("Removed expired peer certificate info for {PeerId}", peerId);
        }

        if (expiredPeers.Any())
        {
            PersistPins();
        }
    }

    /// <summary>
    /// Gets statistics about certificate pin management.
    /// </summary>
    /// <returns>Certificate pin statistics.</returns>
    public CertificatePinStatistics GetStatistics()
    {
        var totalPeers = _peerCertificates.Count;
        var peersWithCurrentPins = _peerCertificates.Count(kvp => kvp.Value.CurrentPins.Any());
        var peersWithPreviousPins = _peerCertificates.Count(kvp => kvp.Value.PreviousPins.Any());
        var totalCurrentPins = _peerCertificates.Sum(kvp => kvp.Value.CurrentPins.Count);
        var totalPreviousPins = _peerCertificates.Sum(kvp => kvp.Value.PreviousPins.Count);

        return new CertificatePinStatistics
        {
            TotalPeers = totalPeers,
            PeersWithCurrentPins = peersWithCurrentPins,
            PeersWithPreviousPins = peersWithPreviousPins,
            TotalCurrentPins = totalCurrentPins,
            TotalPreviousPins = totalPreviousPins
        };
    }

    private void LoadPersistedPins()
    {
        try
        {
            if (!File.Exists(_pinStoragePath))
            {
                return;
            }

            var json = File.ReadAllText(_pinStoragePath);
            var persistedData = System.Text.Json.JsonSerializer.Deserialize<PersistedPinData>(json);

            if (persistedData?.PeerCertificates != null)
            {
                foreach (var certInfo in persistedData.PeerCertificates)
                {
                    _peerCertificates[certInfo.PeerId] = certInfo;
                }

                _logger.LogInformation("Loaded {Count} persisted certificate pins", persistedData.PeerCertificates.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load persisted certificate pins");
        }
    }

    private void PersistPins()
    {
        try
        {
            var persistedData = new PersistedPinData
            {
                PeerCertificates = _peerCertificates.Values.ToList(),
                LastUpdated = DateTimeOffset.UtcNow
            };

            var json = System.Text.Json.JsonSerializer.Serialize(persistedData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_pinStoragePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist certificate pins");
        }
    }
}

/// <summary>
/// Certificate pin types.
/// </summary>
public enum CertificatePinType
{
    /// <summary>
    /// Current active certificate pin.
    /// </summary>
    Current,

    /// <summary>
    /// Previous certificate pin (kept during rotation).
    /// </summary>
    Previous
}

/// <summary>
/// Information about a peer's certificates and pins.
/// </summary>
public class PeerCertificateInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PeerCertificateInfo"/> class.
    /// </summary>
    /// <param name="peerId">The peer ID.</param>
    public PeerCertificateInfo(string peerId)
    {
        PeerId = peerId;
        CurrentPins = new HashSet<string>();
        PreviousPins = new HashSet<string>();
    }

    /// <summary>
    /// Gets the peer ID.
    /// </summary>
    public string PeerId { get; }

    /// <summary>
    /// Gets the current certificate pins.
    /// </summary>
    public HashSet<string> CurrentPins { get; }

    /// <summary>
    /// Gets the previous certificate pins (kept during rotation).
    /// </summary>
    public HashSet<string> PreviousPins { get; }

    /// <summary>
    /// Gets or sets the last time pins were rotated.
    /// </summary>
    public DateTimeOffset LastRotation { get; set; }

    /// <summary>
    /// Gets or sets the last time a certificate was validated.
    /// </summary>
    public DateTimeOffset LastValidation { get; set; }

    /// <summary>
    /// Checks if the peer has any certificate pins.
    /// </summary>
    /// <returns>True if any pins exist.</returns>
    public bool HasAnyPins() => CurrentPins.Any() || PreviousPins.Any();
}

/// <summary>
/// Statistics about certificate pin management.
/// </summary>
public class CertificatePinStatistics
{
    /// <summary>
    /// Gets or sets the total number of peers with certificate info.
    /// </summary>
    public int TotalPeers { get; set; }

    /// <summary>
    /// Gets or sets the number of peers with current pins.
    /// </summary>
    public int PeersWithCurrentPins { get; set; }

    /// <summary>
    /// Gets or sets the number of peers with previous pins.
    /// </summary>
    public int PeersWithPreviousPins { get; set; }

    /// <summary>
    /// Gets or sets the total number of current pins.
    /// </summary>
    public int TotalCurrentPins { get; set; }

    /// <summary>
    /// Gets or sets the total number of previous pins.
    /// </summary>
    public int TotalPreviousPins { get; set; }
}

/// <summary>
/// Data structure for persisting certificate pins to disk.
/// </summary>
internal class PersistedPinData
{
    /// <summary>
    /// Gets or sets the list of peer certificate information.
    /// </summary>
    public List<PeerCertificateInfo> PeerCertificates { get; set; } = new();

    /// <summary>
    /// Gets or sets when the data was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}
