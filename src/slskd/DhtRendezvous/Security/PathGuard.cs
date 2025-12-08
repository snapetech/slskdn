// <copyright file="PathGuard.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Provides path sanitization and validation to prevent directory traversal attacks.
/// SECURITY: Use this for ALL paths that originate from peer/network input.
/// </summary>
public static partial class PathGuard
{
    /// <summary>
    /// Maximum allowed path length.
    /// </summary>
    public const int MaxPathLength = 4096;
    
    /// <summary>
    /// Maximum allowed filename length.
    /// </summary>
    public const int MaxFilenameLength = 255;
    
    /// <summary>
    /// Maximum directory depth (number of path separators).
    /// </summary>
    public const int MaxDirectoryDepth = 50;
    
    // Dangerous path patterns
    [GeneratedRegex(@"\.\.", RegexOptions.Compiled)]
    private static partial Regex DotDotRegex();
    
    [GeneratedRegex(@"^[a-zA-Z]:", RegexOptions.Compiled)]
    private static partial Regex WindowsDriveRegex();
    
    [GeneratedRegex(@"[\x00-\x1f]", RegexOptions.Compiled)]
    private static partial Regex ControlCharsRegex();
    
    // Suspicious filename patterns (potential attacks)
    private static readonly string[] DangerousExtensions = new[]
    {
        ".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".msi", ".dll",
        ".vbs", ".vbe", ".js", ".jse", ".ws", ".wsf", ".wsc", ".wsh",
        ".ps1", ".psm1", ".psd1", ".sh", ".bash", ".zsh", ".csh",
        ".py", ".pyw", ".rb", ".pl", ".php", ".jar", ".class",
    };
    
    /// <summary>
    /// Validates and sanitizes a peer-supplied path against a root directory.
    /// </summary>
    /// <param name="peerPath">The path received from a peer.</param>
    /// <param name="rootDirectory">The allowed root directory.</param>
    /// <returns>A safe absolute path, or null if the path is invalid/dangerous.</returns>
    public static string? SanitizeAndValidate(string? peerPath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(peerPath))
        {
            return null;
        }
        
        // Check length limits
        if (peerPath.Length > MaxPathLength)
        {
            return null;
        }
        
        // Check for control characters (potential null byte injection)
        if (ControlCharsRegex().IsMatch(peerPath))
        {
            return null;
        }
        
        // Check for directory traversal attempts
        if (DotDotRegex().IsMatch(peerPath))
        {
            return null;
        }
        
        // Reject absolute paths from peers
        if (Path.IsPathRooted(peerPath))
        {
            return null;
        }
        
        // Reject Windows drive letters
        if (WindowsDriveRegex().IsMatch(peerPath))
        {
            return null;
        }
        
        // Check directory depth
        var separatorCount = peerPath.Count(c => c == '/' || c == '\\');
        if (separatorCount > MaxDirectoryDepth)
        {
            return null;
        }
        
        // Normalize path separators
        var normalizedPath = peerPath.Replace('\\', '/');
        
        // Check each component
        var parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Length > MaxFilenameLength)
            {
                return null;
            }
            
            // Check for hidden files or suspicious names
            if (part.StartsWith('.') && part != ".")
            {
                // Allow common hidden folder patterns like .config but be cautious
            }
            
            // Reject names that are entirely whitespace or dots
            if (part.All(c => c == '.' || char.IsWhiteSpace(c)))
            {
                return null;
            }
        }
        
        // Combine with root and get full path
        try
        {
            var combinedPath = Path.Combine(rootDirectory, normalizedPath);
            var fullPath = Path.GetFullPath(combinedPath);
            var rootFullPath = Path.GetFullPath(rootDirectory);
            
            // CRITICAL: Verify the resolved path is actually under the root
            if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                // Path escaped the root - directory traversal detected!
                return null;
            }
            
            return fullPath;
        }
        catch
        {
            // Path operations failed - treat as unsafe
            return null;
        }
    }
    
    /// <summary>
    /// Validates a filename (without path) for safety.
    /// </summary>
    /// <param name="filename">The filename to validate.</param>
    /// <returns>True if the filename is safe to use.</returns>
    public static bool IsFilenameValid(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }
        
        if (filename.Length > MaxFilenameLength)
        {
            return false;
        }
        
        // Check for control characters
        if (ControlCharsRegex().IsMatch(filename))
        {
            return false;
        }
        
        // Check for path separators in filename
        if (filename.Contains('/') || filename.Contains('\\'))
        {
            return false;
        }
        
        // Check for directory traversal
        if (filename == "." || filename == ".." || filename.Contains(".."))
        {
            return false;
        }
        
        // Reject filenames that are all whitespace
        if (filename.All(char.IsWhiteSpace))
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Checks if a filename has a potentially dangerous extension.
    /// </summary>
    /// <param name="filename">The filename to check.</param>
    /// <returns>True if the extension is potentially dangerous.</returns>
    public static bool HasDangerousExtension(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }
        
        var extension = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }
        
        return DangerousExtensions.Contains(extension.ToLowerInvariant());
    }
    
    /// <summary>
    /// Gets the expected media extensions for various file types.
    /// </summary>
    public static readonly string[] SafeAudioExtensions = new[]
    {
        ".flac", ".mp3", ".ogg", ".opus", ".m4a", ".aac", ".wav", ".wma",
        ".ape", ".wv", ".tta", ".alac", ".aiff", ".aif",
    };
    
    /// <summary>
    /// Checks if a filename has a safe audio extension.
    /// </summary>
    public static bool HasSafeAudioExtension(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return false;
        }
        
        var extension = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(extension))
        {
            return false;
        }
        
        return SafeAudioExtensions.Contains(extension.ToLowerInvariant());
    }
    
    /// <summary>
    /// Result of path validation.
    /// </summary>
    public readonly struct PathValidationResult
    {
        public bool IsValid { get; init; }
        public string? SafePath { get; init; }
        public string? Error { get; init; }
        public PathViolationType ViolationType { get; init; }
        
        public static PathValidationResult Success(string safePath) => new()
        {
            IsValid = true,
            SafePath = safePath,
            ViolationType = PathViolationType.None,
        };
        
        public static PathValidationResult Fail(string error, PathViolationType type) => new()
        {
            IsValid = false,
            Error = error,
            ViolationType = type,
        };
    }
    
    /// <summary>
    /// Types of path violations detected.
    /// </summary>
    public enum PathViolationType
    {
        None,
        Empty,
        TooLong,
        ControlCharacters,
        DirectoryTraversal,
        AbsolutePath,
        TooDeep,
        InvalidComponent,
        EscapedRoot,
    }
    
    /// <summary>
    /// Validates a peer path with detailed error reporting.
    /// </summary>
    public static PathValidationResult ValidatePeerPath(string? peerPath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(peerPath))
        {
            return PathValidationResult.Fail("Path is empty", PathViolationType.Empty);
        }
        
        if (peerPath.Length > MaxPathLength)
        {
            return PathValidationResult.Fail($"Path too long: {peerPath.Length} > {MaxPathLength}", PathViolationType.TooLong);
        }
        
        if (ControlCharsRegex().IsMatch(peerPath))
        {
            return PathValidationResult.Fail("Path contains control characters", PathViolationType.ControlCharacters);
        }
        
        if (DotDotRegex().IsMatch(peerPath))
        {
            return PathValidationResult.Fail("Directory traversal detected", PathViolationType.DirectoryTraversal);
        }
        
        if (Path.IsPathRooted(peerPath) || WindowsDriveRegex().IsMatch(peerPath))
        {
            return PathValidationResult.Fail("Absolute paths not allowed", PathViolationType.AbsolutePath);
        }
        
        var separatorCount = peerPath.Count(c => c == '/' || c == '\\');
        if (separatorCount > MaxDirectoryDepth)
        {
            return PathValidationResult.Fail($"Path too deep: {separatorCount} > {MaxDirectoryDepth}", PathViolationType.TooDeep);
        }
        
        try
        {
            var normalizedPath = peerPath.Replace('\\', '/');
            var combinedPath = Path.Combine(rootDirectory, normalizedPath);
            var fullPath = Path.GetFullPath(combinedPath);
            var rootFullPath = Path.GetFullPath(rootDirectory);
            
            if (!fullPath.StartsWith(rootFullPath, StringComparison.OrdinalIgnoreCase))
            {
                return PathValidationResult.Fail("Path escapes root directory", PathViolationType.EscapedRoot);
            }
            
            return PathValidationResult.Success(fullPath);
        }
        catch (Exception ex)
        {
            return PathValidationResult.Fail($"Invalid path: {ex.Message}", PathViolationType.InvalidComponent);
        }
    }
}

