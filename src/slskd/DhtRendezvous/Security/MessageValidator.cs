// <copyright file="MessageValidator.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using slskd.DhtRendezvous.Messages;

/// <summary>
/// Validates all incoming overlay messages to prevent injection attacks.
/// ALL peer data is untrusted and must be validated before use.
/// </summary>
public static partial class MessageValidator
{
    /// <summary>
    /// Maximum username length.
    /// </summary>
    public const int MaxUsernameLength = 64;
    
    /// <summary>
    /// Maximum number of features in handshake.
    /// </summary>
    public const int MaxFeatures = 20;
    
    /// <summary>
    /// Maximum length of each feature string.
    /// </summary>
    public const int MaxFeatureLength = 32;
    
    /// <summary>
    /// Maximum nonce length.
    /// </summary>
    public const int MaxNonceLength = 64;
    
    /// <summary>
    /// Maximum disconnect reason length.
    /// </summary>
    public const int MaxReasonLength = 256;
    
    /// <summary>
    /// Valid protocol versions (for forward compatibility).
    /// </summary>
    public const int MinVersion = 1;
    public const int MaxVersion = 100;
    
    /// <summary>
    /// Valid port range.
    /// </summary>
    public const int MinPort = 1;
    public const int MaxPort = 65535;
    
    /// <summary>
    /// Valid FLAC key length (64-bit = 16 hex chars).
    /// </summary>
    public const int FlacKeyLength = 16;
    
    /// <summary>
    /// Valid SHA256 hash length (64 hex chars).
    /// </summary>
    public const int Sha256HexLength = 64;
    
    // Compiled regex patterns for security-critical validation
    [GeneratedRegex(@"^[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled)]
    private static partial Regex UsernameRegex();
    
    [GeneratedRegex(@"^[a-z0-9_]+$", RegexOptions.Compiled)]
    private static partial Regex FeatureRegex();
    
    [GeneratedRegex(@"^[a-fA-F0-9]+$", RegexOptions.Compiled)]
    private static partial Regex HexRegex();
    
    [GeneratedRegex(@"^[a-zA-Z0-9\-_]+$", RegexOptions.Compiled)]
    private static partial Regex NonceRegex();

    // Pre-computed magic bytes for constant-time comparison
    private static readonly byte[] ExpectedMagicBytes = Encoding.UTF8.GetBytes(OverlayProtocol.Magic);
    
    /// <summary>
    /// Constant-time string comparison to prevent timing attacks.
    /// SECURITY: Use this for any security-critical string comparisons.
    /// </summary>
    public static bool ConstantTimeEquals(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }
        
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
    
    /// <summary>
    /// Validates the protocol magic string using constant-time comparison.
    /// </summary>
    private static bool ValidateMagic(string? magic)
    {
        if (magic is null)
        {
            return false;
        }
        
        var magicBytes = Encoding.UTF8.GetBytes(magic);
        return CryptographicOperations.FixedTimeEquals(magicBytes, ExpectedMagicBytes);
    }
    
    /// <summary>
    /// Validates a mesh hello message.
    /// </summary>
    /// <param name="message">The message to validate.</param>
    /// <returns>Validation result with error details if invalid.</returns>
    public static ValidationResult ValidateMeshHello(MeshHelloMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }
        
        // SECURITY: Use constant-time comparison for magic to prevent timing attacks
        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }
        
        // Version bounds
        if (message.Version < MinVersion || message.Version > MaxVersion)
        {
            return ValidationResult.Fail($"Invalid version: {message.Version} (expected {MinVersion}-{MaxVersion})");
        }
        
        // Username validation - CRITICAL for security
        var usernameResult = ValidateUsername(message.Username);
        if (!usernameResult.IsValid)
        {
            return usernameResult;
        }
        
        // Features validation
        var featuresResult = ValidateFeatures(message.Features);
        if (!featuresResult.IsValid)
        {
            return featuresResult;
        }
        
        // Ports validation
        if (message.SoulseekPorts is not null)
        {
            var portsResult = ValidatePorts(message.SoulseekPorts);
            if (!portsResult.IsValid)
            {
                return portsResult;
            }
        }
        
        // Nonce validation (optional)
        if (message.Nonce is not null)
        {
            var nonceResult = ValidateNonce(message.Nonce);
            if (!nonceResult.IsValid)
            {
                return nonceResult;
            }
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a mesh hello ack message.
    /// </summary>
    public static ValidationResult ValidateMeshHelloAck(MeshHelloAckMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }
        
        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }
        
        if (message.Version < MinVersion || message.Version > MaxVersion)
        {
            return ValidationResult.Fail($"Invalid version: {message.Version}");
        }
        
        var usernameResult = ValidateUsername(message.Username);
        if (!usernameResult.IsValid)
        {
            return usernameResult;
        }
        
        var featuresResult = ValidateFeatures(message.Features);
        if (!featuresResult.IsValid)
        {
            return featuresResult;
        }
        
        if (message.SoulseekPorts is not null)
        {
            var portsResult = ValidatePorts(message.SoulseekPorts);
            if (!portsResult.IsValid)
            {
                return portsResult;
            }
        }
        
        if (message.NonceEcho is not null)
        {
            var nonceResult = ValidateNonce(message.NonceEcho);
            if (!nonceResult.IsValid)
            {
                return nonceResult;
            }
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a ping message.
    /// </summary>
    public static ValidationResult ValidatePing(PingMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }
        
        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }
        
        // Timestamp must be reasonable (within last 24 hours to prevent replay)
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var diff = Math.Abs(now - message.Timestamp);
        if (diff > 86400000) // 24 hours in ms
        {
            return ValidationResult.Fail("Timestamp too old or in future");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a pong message.
    /// </summary>
    public static ValidationResult ValidatePong(PongMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }
        
        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a disconnect message.
    /// </summary>
    public static ValidationResult ValidateDisconnect(DisconnectMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }
        
        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }
        
        if (message.Reason is not null && message.Reason.Length > MaxReasonLength)
        {
            return ValidationResult.Fail($"Reason too long: {message.Reason.Length} > {MaxReasonLength}");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a Soulseek username.
    /// </summary>
    public static ValidationResult ValidateUsername(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return ValidationResult.Fail("Username is empty");
        }
        
        if (username.Length > MaxUsernameLength)
        {
            return ValidationResult.Fail($"Username too long: {username.Length} > {MaxUsernameLength}");
        }
        
        if (!UsernameRegex().IsMatch(username))
        {
            return ValidationResult.Fail("Username contains invalid characters");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a list of features.
    /// </summary>
    public static ValidationResult ValidateFeatures(IList<string>? features)
    {
        if (features is null)
        {
            return ValidationResult.Success; // Features are optional
        }
        
        if (features.Count > MaxFeatures)
        {
            return ValidationResult.Fail($"Too many features: {features.Count} > {MaxFeatures}");
        }
        
        foreach (var feature in features)
        {
            if (string.IsNullOrEmpty(feature))
            {
                return ValidationResult.Fail("Empty feature string");
            }
            
            if (feature.Length > MaxFeatureLength)
            {
                return ValidationResult.Fail($"Feature too long: {feature.Length} > {MaxFeatureLength}");
            }
            
            if (!FeatureRegex().IsMatch(feature))
            {
                return ValidationResult.Fail($"Feature contains invalid characters: {feature}");
            }
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates Soulseek ports.
    /// </summary>
    public static ValidationResult ValidatePorts(SoulseekPorts ports)
    {
        if (ports.Peer < 0 || ports.Peer > MaxPort)
        {
            return ValidationResult.Fail($"Invalid peer port: {ports.Peer}");
        }
        
        if (ports.File < 0 || ports.File > MaxPort)
        {
            return ValidationResult.Fail($"Invalid file port: {ports.File}");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a nonce string.
    /// </summary>
    public static ValidationResult ValidateNonce(string nonce)
    {
        if (nonce.Length > MaxNonceLength)
        {
            return ValidationResult.Fail($"Nonce too long: {nonce.Length} > {MaxNonceLength}");
        }
        
        if (!NonceRegex().IsMatch(nonce))
        {
            return ValidationResult.Fail("Nonce contains invalid characters");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a FLAC key (16 hex chars = 64-bit truncated hash).
    /// </summary>
    public static ValidationResult ValidateFlacKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return ValidationResult.Fail("FLAC key is empty");
        }
        
        if (key.Length != FlacKeyLength)
        {
            return ValidationResult.Fail($"FLAC key wrong length: {key.Length} != {FlacKeyLength}");
        }
        
        if (!HexRegex().IsMatch(key))
        {
            return ValidationResult.Fail("FLAC key is not valid hex");
        }
        
        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates a SHA256 hash (64 hex chars).
    /// </summary>
    public static ValidationResult ValidateSha256Hash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return ValidationResult.Fail("Hash is empty");
        }
        
        if (hash.Length != Sha256HexLength)
        {
            return ValidationResult.Fail($"Hash wrong length: {hash.Length} != {Sha256HexLength}");
        }
        
        if (!HexRegex().IsMatch(hash))
        {
            return ValidationResult.Fail("Hash is not valid hex");
        }
        
        return ValidationResult.Success;
    }

    /// <summary>
    /// Maximum search text length for mesh_search_req.
    /// </summary>
    public const int MaxMeshSearchTextLength = 256;

    /// <summary>
    /// Minimum max_results for mesh_search_req.
    /// </summary>
    public const int MinMeshSearchMaxResults = 1;

    /// <summary>
    /// Maximum max_results for mesh_search_req.
    /// </summary>
    public const int MaxMeshSearchMaxResults = 200;

    /// <summary>
    /// Maximum number of files in a mesh_search_resp (amplification limit).
    /// </summary>
    public const int MaxMeshSearchResponseFiles = 500;

    /// <summary>
    /// Validates a mesh search request message.
    /// </summary>
    public static ValidationResult ValidateMeshSearchReq(MeshSearchRequestMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }

        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }

        if (message.Version < MinVersion || message.Version > MaxVersion)
        {
            return ValidationResult.Fail($"Invalid version: {message.Version}");
        }

        if (string.IsNullOrEmpty(message.RequestId) || !Guid.TryParse(message.RequestId, out _))
        {
            return ValidationResult.Fail("request_id must be a valid GUID");
        }

        if (string.IsNullOrWhiteSpace(message.SearchText))
        {
            return ValidationResult.Fail("search_text must be non-empty");
        }

        if (message.SearchText.Length > MaxMeshSearchTextLength)
        {
            return ValidationResult.Fail($"search_text too long: {message.SearchText.Length} > {MaxMeshSearchTextLength}");
        }

        if (message.MaxResults < MinMeshSearchMaxResults || message.MaxResults > MaxMeshSearchMaxResults)
        {
            return ValidationResult.Fail($"max_results must be between {MinMeshSearchMaxResults} and {MaxMeshSearchMaxResults}, got {message.MaxResults}");
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates a mesh search response message.
    /// </summary>
    public static ValidationResult ValidateMeshSearchResp(MeshSearchResponseMessage? message)
    {
        if (message is null)
        {
            return ValidationResult.Fail("Message is null");
        }

        if (!ValidateMagic(message.Magic))
        {
            return ValidationResult.Fail("Invalid protocol magic");
        }

        if (string.IsNullOrEmpty(message.RequestId) || !Guid.TryParse(message.RequestId, out _))
        {
            return ValidationResult.Fail("request_id must be a valid GUID");
        }

        if (message.Files is not null && message.Files.Count > MaxMeshSearchResponseFiles)
        {
            return ValidationResult.Fail($"files count exceeds limit: {message.Files.Count} > {MaxMeshSearchResponseFiles}");
        }

        if (message.Files is not null)
        {
            foreach (var f in message.Files)
            {
                if (f is null)
                {
                    return ValidationResult.Fail("files must not contain null");
                }

                if (string.IsNullOrEmpty(f.Filename))
                {
                    return ValidationResult.Fail("file filename must be non-empty");
                }

                if (f.Size < 0 || f.Size > 10_000_000_000)
                {
                    return ValidationResult.Fail($"file size out of range: {f.Size}");
                }
            }
        }

        return ValidationResult.Success;
    }
    
    /// <summary>
    /// Validates file size is reasonable.
    /// </summary>
    public static ValidationResult ValidateFileSize(long size)
    {
        if (size <= 0)
        {
            return ValidationResult.Fail("File size must be positive");
        }
        
        // Max 10 GB - reasonable for FLAC files
        if (size > 10_000_000_000)
        {
            return ValidationResult.Fail($"File size too large: {size}");
        }
        
        return ValidationResult.Success;
    }
}

/// <summary>
/// Result of message validation.
/// </summary>
public readonly struct ValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public bool IsValid { get; }
    
    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? Error { get; }
    
    private ValidationResult(bool isValid, string? error)
    {
        IsValid = isValid;
        Error = error;
    }
    
    /// <summary>
    /// Successful validation result.
    /// </summary>
    public static ValidationResult Success => new(true, null);
    
    /// <summary>
    /// Create a failed validation result.
    /// </summary>
    public static ValidationResult Fail(string error) => new(false, error);
    
    /// <summary>
    /// Implicit conversion to bool.
    /// </summary>
    public static implicit operator bool(ValidationResult result) => result.IsValid;
}

