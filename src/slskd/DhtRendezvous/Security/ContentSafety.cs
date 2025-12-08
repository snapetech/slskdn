// <copyright file="ContentSafety.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides content safety verification for downloaded files.
/// SECURITY: Detects file type mismatches (e.g., .exe disguised as .mp3).
/// </summary>
public static class ContentSafety
{
    /// <summary>
    /// File signature (magic bytes) definitions.
    /// </summary>
    private static readonly Dictionary<string, FileSignature[]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        // Audio formats
        [".flac"] = new[] { new FileSignature(new byte[] { 0x66, 0x4C, 0x61, 0x43 }, 0) }, // fLaC
        [".mp3"] = new[]
        {
            new FileSignature(new byte[] { 0xFF, 0xFB }, 0), // MPEG audio frame sync
            new FileSignature(new byte[] { 0xFF, 0xFA }, 0),
            new FileSignature(new byte[] { 0xFF, 0xF3 }, 0),
            new FileSignature(new byte[] { 0xFF, 0xF2 }, 0),
            new FileSignature(new byte[] { 0x49, 0x44, 0x33 }, 0), // ID3v2 tag
        },
        [".ogg"] = new[] { new FileSignature(new byte[] { 0x4F, 0x67, 0x67, 0x53 }, 0) }, // OggS
        [".opus"] = new[] { new FileSignature(new byte[] { 0x4F, 0x67, 0x67, 0x53 }, 0) }, // Also OggS container
        [".wav"] = new[] { new FileSignature(new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0) }, // RIFF
        [".m4a"] = new[]
        {
            new FileSignature(new byte[] { 0x00, 0x00, 0x00 }, 0), // ftyp at offset 4
            new FileSignature(new byte[] { 0x66, 0x74, 0x79, 0x70 }, 4), // ftyp
        },
        [".aac"] = new[] { new FileSignature(new byte[] { 0xFF, 0xF1 }, 0) }, // ADTS
        [".wma"] = new[] { new FileSignature(new byte[] { 0x30, 0x26, 0xB2, 0x75, 0x8E, 0x66, 0xCF, 0x11 }, 0) }, // ASF
        [".ape"] = new[] { new FileSignature(new byte[] { 0x4D, 0x41, 0x43, 0x20 }, 0) }, // MAC 
        [".wv"] = new[] { new FileSignature(new byte[] { 0x77, 0x76, 0x70, 0x6B }, 0) }, // wvpk
        
        // Dangerous executable formats (for detection)
        [".exe"] = new[]
        {
            new FileSignature(new byte[] { 0x4D, 0x5A }, 0), // MZ (DOS/PE)
        },
        [".dll"] = new[]
        {
            new FileSignature(new byte[] { 0x4D, 0x5A }, 0), // MZ (DOS/PE)
        },
        [".com"] = new[]
        {
            new FileSignature(new byte[] { 0x4D, 0x5A }, 0), // Some COM files are actually PE
        },
        
        // Scripts (text-based, harder to detect but check for shebang)
        [".sh"] = new[]
        {
            new FileSignature(new byte[] { 0x23, 0x21 }, 0), // #!
        },
        
        // Archives (could contain executables)
        [".zip"] = new[]
        {
            new FileSignature(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0), // PK
            new FileSignature(new byte[] { 0x50, 0x4B, 0x05, 0x06 }, 0), // Empty archive
        },
        [".rar"] = new[]
        {
            new FileSignature(new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07 }, 0), // Rar!
        },
        [".7z"] = new[]
        {
            new FileSignature(new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C }, 0), // 7z
        },
        
        // ELF binaries (Linux executables)
        [".elf"] = new[]
        {
            new FileSignature(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, 0), // .ELF
        },
    };
    
    /// <summary>
    /// Magic bytes that indicate dangerous content regardless of extension.
    /// </summary>
    private static readonly FileSignature[] DangerousSignatures = new[]
    {
        new FileSignature(new byte[] { 0x4D, 0x5A }, 0, "PE/DOS executable"),
        new FileSignature(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, 0, "ELF executable"),
        new FileSignature(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, 0, "Java class file"),
        new FileSignature(new byte[] { 0xCE, 0xFA, 0xED, 0xFE }, 0, "Mach-O 32-bit"),
        new FileSignature(new byte[] { 0xCF, 0xFA, 0xED, 0xFE }, 0, "Mach-O 64-bit"),
        new FileSignature(new byte[] { 0xFE, 0xED, 0xFA, 0xCE }, 0, "Mach-O 32-bit (BE)"),
        new FileSignature(new byte[] { 0xFE, 0xED, 0xFA, 0xCF }, 0, "Mach-O 64-bit (BE)"),
    };
    
    /// <summary>
    /// Verifies that a file's content matches its extension.
    /// </summary>
    /// <param name="filePath">Path to the file to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    public static async Task<ContentVerificationResult> VerifyFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return ContentVerificationResult.Fail("File not found", ContentThreatLevel.Unknown);
        }
        
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            return ContentVerificationResult.Fail("No file extension", ContentThreatLevel.Suspicious);
        }
        
        // Read header bytes
        byte[] header;
        try
        {
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            header = new byte[Math.Min(stream.Length, 16)];
            await stream.ReadExactlyAsync(header, cancellationToken);
        }
        catch (Exception ex)
        {
            return ContentVerificationResult.Fail($"Could not read file: {ex.Message}", ContentThreatLevel.Unknown);
        }
        
        // Check for dangerous content first
        foreach (var sig in DangerousSignatures)
        {
            if (MatchesSignature(header, sig))
            {
                // Is this expected for the extension?
                if (extension is ".exe" or ".dll" or ".com")
                {
                    return ContentVerificationResult.Warn(
                        $"Executable file: {sig.Description}",
                        ContentThreatLevel.Executable);
                }
                
                // Extension mismatch with executable content!
                return ContentVerificationResult.Fail(
                    $"DANGEROUS: File claims to be {extension} but contains {sig.Description}",
                    ContentThreatLevel.Dangerous);
            }
        }
        
        // Check if file matches expected signature for its extension
        if (Signatures.TryGetValue(extension, out var expectedSigs))
        {
            var matches = false;
            foreach (var sig in expectedSigs)
            {
                if (MatchesSignature(header, sig))
                {
                    matches = true;
                    break;
                }
            }
            
            if (!matches)
            {
                return ContentVerificationResult.Warn(
                    $"Content does not match expected {extension} signature",
                    ContentThreatLevel.Mismatch);
            }
        }
        
        return ContentVerificationResult.Success(extension);
    }
    
    /// <summary>
    /// Verifies content from a byte array (for streaming verification).
    /// </summary>
    /// <param name="header">First bytes of the file (at least 16 bytes recommended).</param>
    /// <param name="expectedExtension">Expected file extension.</param>
    /// <returns>Verification result.</returns>
    public static ContentVerificationResult VerifyHeader(byte[] header, string expectedExtension)
    {
        if (header == null || header.Length < 2)
        {
            return ContentVerificationResult.Fail("Header too short", ContentThreatLevel.Unknown);
        }
        
        var extension = expectedExtension?.ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            return ContentVerificationResult.Fail("No expected extension", ContentThreatLevel.Unknown);
        }
        
        // Check for dangerous content
        foreach (var sig in DangerousSignatures)
        {
            if (MatchesSignature(header, sig))
            {
                if (extension is ".exe" or ".dll" or ".com")
                {
                    return ContentVerificationResult.Warn(
                        $"Executable file: {sig.Description}",
                        ContentThreatLevel.Executable);
                }
                
                return ContentVerificationResult.Fail(
                    $"DANGEROUS: Content claims to be {extension} but is {sig.Description}",
                    ContentThreatLevel.Dangerous);
            }
        }
        
        // Check extension match
        if (Signatures.TryGetValue(extension, out var expectedSigs))
        {
            var matches = false;
            foreach (var sig in expectedSigs)
            {
                if (MatchesSignature(header, sig))
                {
                    matches = true;
                    break;
                }
            }
            
            if (!matches)
            {
                return ContentVerificationResult.Warn(
                    $"Content does not match expected {extension} signature",
                    ContentThreatLevel.Mismatch);
            }
        }
        
        return ContentVerificationResult.Success(extension);
    }
    
    /// <summary>
    /// Quick check if header bytes indicate an executable.
    /// </summary>
    public static bool IsExecutable(byte[] header)
    {
        if (header == null || header.Length < 2)
        {
            return false;
        }
        
        foreach (var sig in DangerousSignatures)
        {
            if (MatchesSignature(header, sig))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private static bool MatchesSignature(byte[] data, FileSignature signature)
    {
        if (data.Length < signature.Offset + signature.Magic.Length)
        {
            return false;
        }
        
        for (int i = 0; i < signature.Magic.Length; i++)
        {
            if (data[signature.Offset + i] != signature.Magic[i])
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// File signature definition.
    /// </summary>
    private readonly struct FileSignature
    {
        public byte[] Magic { get; }
        public int Offset { get; }
        public string Description { get; }
        
        public FileSignature(byte[] magic, int offset, string description = "")
        {
            Magic = magic;
            Offset = offset;
            Description = description;
        }
    }
}

/// <summary>
/// Result of content verification.
/// </summary>
public readonly struct ContentVerificationResult
{
    public bool IsValid { get; init; }
    public bool IsWarning { get; init; }
    public string? Message { get; init; }
    public string? DetectedType { get; init; }
    public ContentThreatLevel ThreatLevel { get; init; }
    
    public static ContentVerificationResult Success(string detectedType) => new()
    {
        IsValid = true,
        IsWarning = false,
        DetectedType = detectedType,
        ThreatLevel = ContentThreatLevel.Safe,
    };
    
    public static ContentVerificationResult Warn(string message, ContentThreatLevel level) => new()
    {
        IsValid = true,
        IsWarning = true,
        Message = message,
        ThreatLevel = level,
    };
    
    public static ContentVerificationResult Fail(string message, ContentThreatLevel level) => new()
    {
        IsValid = false,
        IsWarning = false,
        Message = message,
        ThreatLevel = level,
    };
}

/// <summary>
/// Threat level of detected content.
/// </summary>
public enum ContentThreatLevel
{
    /// <summary>Content is safe.</summary>
    Safe,
    
    /// <summary>Content type is unknown.</summary>
    Unknown,
    
    /// <summary>Content is suspicious but not necessarily dangerous.</summary>
    Suspicious,
    
    /// <summary>Content type doesn't match extension.</summary>
    Mismatch,
    
    /// <summary>Content is an executable (may be intentional).</summary>
    Executable,
    
    /// <summary>Content is dangerous (executable disguised as media).</summary>
    Dangerous,
}

