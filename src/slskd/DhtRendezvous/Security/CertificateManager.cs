// <copyright file="CertificateManager.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

/// <summary>
/// Manages TLS certificates for the overlay protocol.
/// Generates and stores self-signed certificates for TLS encryption.
/// </summary>
public sealed class CertificateManager
{
    private readonly ILogger<CertificateManager> _logger;
    private readonly string _certificatePath;
    private readonly string _passwordPath;
    private X509Certificate2? _serverCertificate;
    private string? _certificatePassword;
    private readonly object _lock = new();
    
    /// <summary>
    /// Certificate validity period.
    /// </summary>
    public static readonly TimeSpan CertificateValidity = TimeSpan.FromDays(365);
    
    /// <summary>
    /// RSA key size.
    /// </summary>
    public const int KeySize = 4096;
    
    public CertificateManager(ILogger<CertificateManager> logger, string appDirectory)
    {
        _logger = logger;
        _certificatePath = Path.Combine(appDirectory, "overlay_cert.pfx");
        _passwordPath = Path.Combine(appDirectory, "overlay_cert.key");
    }
    
    /// <summary>
    /// Get or create the server certificate.
    /// </summary>
    /// <returns>The server X509 certificate.</returns>
    public X509Certificate2 GetOrCreateServerCertificate()
    {
        lock (_lock)
        {
            if (_serverCertificate is not null && _serverCertificate.NotAfter > DateTime.UtcNow.AddDays(30))
            {
                return _serverCertificate;
            }
            
            // Try to load existing certificate
            if (File.Exists(_certificatePath))
            {
                try
                {
                    _serverCertificate = LoadCertificate(_certificatePath);
                    
                    // Check if certificate is still valid (with 30-day buffer)
                    if (_serverCertificate.NotAfter > DateTime.UtcNow.AddDays(30))
                    {
                        _logger.LogInformation(
                            "Loaded existing overlay certificate, expires {ExpiryDate}",
                            _serverCertificate.NotAfter);
                        return _serverCertificate;
                    }
                    
                    _logger.LogWarning(
                        "Existing certificate expires soon ({ExpiryDate}), generating new one",
                        _serverCertificate.NotAfter);
                    _serverCertificate.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load existing certificate, generating new one");
                }
            }
            
            // Generate new certificate
            _serverCertificate = GenerateSelfSignedCertificate();
            SaveCertificate(_serverCertificate, _certificatePath);
            
            _logger.LogInformation(
                "Generated new overlay certificate, expires {ExpiryDate}",
                _serverCertificate.NotAfter);
            
            return _serverCertificate;
        }
    }
    
    /// <summary>
    /// Get the certificate thumbprint (SHA256).
    /// </summary>
    public string GetCertificateThumbprint()
    {
        var cert = GetOrCreateServerCertificate();
        return cert.GetCertHashString(HashAlgorithmName.SHA256);
    }
    
    /// <summary>
    /// Generate a self-signed certificate.
    /// </summary>
    private X509Certificate2 GenerateSelfSignedCertificate()
    {
        _logger.LogDebug("Generating self-signed certificate with {KeySize}-bit RSA key", KeySize);
        
        using var rsa = RSA.Create(KeySize);
        
        var request = new CertificateRequest(
            "CN=slskdn-overlay,O=slskdn",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        
        // Add extensions
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                true));
        
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new("1.3.6.1.5.5.7.3.1"), // Server auth
                    new("1.3.6.1.5.5.7.3.2"), // Client auth
                },
                true));
        
        // Add SAN (Subject Alternative Name)
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName("localhost");
        sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
        sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        request.CertificateExtensions.Add(sanBuilder.Build());
        
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        var notAfter = DateTimeOffset.UtcNow.Add(CertificateValidity);
        
        var certificate = request.CreateSelfSigned(notBefore, notAfter);
        
        // Export and reimport to get a certificate with private key in usable form
        var pfxBytes = certificate.Export(X509ContentType.Pfx);
        return new X509Certificate2(pfxBytes, (string?)null, X509KeyStorageFlags.Exportable);
    }
    
    /// <summary>
    /// Save certificate to file with password protection.
    /// </summary>
    private void SaveCertificate(X509Certificate2 certificate, string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // SECURITY: Generate a random password for the PFX
        var password = GenerateRandomPassword();
        _certificatePassword = password;
        
        var pfxBytes = certificate.Export(X509ContentType.Pfx, password);
        File.WriteAllBytes(path, pfxBytes);
        
        // Save password to separate file
        File.WriteAllText(_passwordPath, password);
        
        // Set restrictive permissions (Unix only)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                File.SetUnixFileMode(_passwordPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set certificate file permissions");
            }
        }
        
        _logger.LogDebug("Saved certificate to {Path} with password protection", path);
    }
    
    /// <summary>
    /// Load certificate from file.
    /// </summary>
    private X509Certificate2 LoadCertificate(string path)
    {
        // Try to load password from file
        string? password = null;
        if (File.Exists(_passwordPath))
        {
            try
            {
                password = File.ReadAllText(_passwordPath).Trim();
                _certificatePassword = password;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read certificate password file");
            }
        }
        
        // Try loading with password first, then without (for backwards compatibility)
        try
        {
            return new X509Certificate2(path, password, X509KeyStorageFlags.Exportable);
        }
        catch (CryptographicException) when (password != null)
        {
            _logger.LogWarning("Certificate password mismatch, trying without password (legacy)");
            return new X509Certificate2(path);
        }
    }
    
    /// <summary>
    /// Generate a cryptographically random password.
    /// </summary>
    private static string GenerateRandomPassword()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>
/// Certificate pin store for TOFU (Trust On First Use) model.
/// Stores the SHA256 thumbprint of peer certificates after first successful connection.
/// </summary>
public sealed class CertificatePinStore
{
    private readonly ILogger<CertificatePinStore> _logger;
    private readonly string _pinStorePath;
    private readonly Dictionary<string, CertificatePin> _pins = new();
    private readonly object _lock = new();
    
    public CertificatePinStore(ILogger<CertificatePinStore> logger, string appDirectory)
    {
        _logger = logger;
        _pinStorePath = Path.Combine(appDirectory, "cert_pins.json");
        Load();
    }
    
    /// <summary>
    /// Check if a certificate is pinned for a peer.
    /// </summary>
    /// <param name="username">Soulseek username.</param>
    /// <param name="thumbprint">Certificate SHA256 thumbprint.</param>
    /// <returns>True if no pin exists (first use) or thumbprint matches.</returns>
    public PinCheckResult CheckPin(string username, string thumbprint)
    {
        lock (_lock)
        {
            if (!_pins.TryGetValue(username.ToLowerInvariant(), out var pin))
            {
                return PinCheckResult.NotPinned;
            }
            
            if (pin.Thumbprint.Equals(thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                return PinCheckResult.Valid;
            }
            
            _logger.LogWarning(
                "Certificate pin mismatch for {Username}! Expected {Expected}, got {Actual}",
                username,
                pin.Thumbprint[..16] + "...",
                thumbprint[..16] + "...");
            
            return PinCheckResult.Mismatch;
        }
    }
    
    /// <summary>
    /// Add or update a certificate pin.
    /// </summary>
    public void SetPin(string username, string thumbprint)
    {
        lock (_lock)
        {
            var normalizedUsername = username.ToLowerInvariant();
            _pins[normalizedUsername] = new CertificatePin
            {
                Username = username,
                Thumbprint = thumbprint,
                FirstSeen = DateTimeOffset.UtcNow,
                LastSeen = DateTimeOffset.UtcNow,
            };
            
            Save();
            _logger.LogInformation("Pinned certificate for {Username}", username);
        }
    }
    
    /// <summary>
    /// Update last seen time for a pinned certificate.
    /// </summary>
    public void TouchPin(string username)
    {
        lock (_lock)
        {
            var normalizedUsername = username.ToLowerInvariant();
            if (_pins.TryGetValue(normalizedUsername, out var pin))
            {
                pin.LastSeen = DateTimeOffset.UtcNow;
                Save();
            }
        }
    }
    
    /// <summary>
    /// Remove a certificate pin.
    /// </summary>
    public bool RemovePin(string username)
    {
        lock (_lock)
        {
            var removed = _pins.Remove(username.ToLowerInvariant());
            if (removed)
            {
                Save();
                _logger.LogInformation("Removed certificate pin for {Username}", username);
            }
            
            return removed;
        }
    }
    
    /// <summary>
    /// Get all pinned certificates.
    /// </summary>
    public IReadOnlyList<CertificatePin> GetAllPins()
    {
        lock (_lock)
        {
            return _pins.Values.ToList();
        }
    }
    
    private void Load()
    {
        if (!File.Exists(_pinStorePath))
        {
            return;
        }
        
        try
        {
            var json = File.ReadAllText(_pinStorePath);
            var pins = System.Text.Json.JsonSerializer.Deserialize<List<CertificatePin>>(json);
            
            if (pins is not null)
            {
                lock (_lock)
                {
                    foreach (var pin in pins)
                    {
                        _pins[pin.Username.ToLowerInvariant()] = pin;
                    }
                }
            }
            
            _logger.LogDebug("Loaded {Count} certificate pins", _pins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load certificate pins");
        }
    }
    
    private void Save()
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_pins.Values.ToList());
            File.WriteAllText(_pinStorePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save certificate pins");
        }
    }
}

/// <summary>
/// Stored certificate pin.
/// </summary>
public sealed class CertificatePin
{
    public required string Username { get; init; }
    public required string Thumbprint { get; init; }
    public required DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>
/// Result of certificate pin check.
/// </summary>
public enum PinCheckResult
{
    /// <summary>No pin exists (first use - should pin it).</summary>
    NotPinned,
    
    /// <summary>Pin exists and matches.</summary>
    Valid,
    
    /// <summary>Pin exists but doesn't match (potential MITM!).</summary>
    Mismatch,
}

