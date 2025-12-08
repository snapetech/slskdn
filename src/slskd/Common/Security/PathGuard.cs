// <copyright file="PathGuard.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Validates and sanitizes file paths to prevent directory traversal attacks.
/// ALL paths derived from peer/server input MUST go through this.
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

    /// <summary>
    /// Characters forbidden in filenames.
    /// </summary>
    public static readonly char[] ForbiddenChars = { '<', '>', ':', '"', '|', '?', '*', '\0' };

    /// <summary>
    /// Dangerous file extensions that may contain executable code.
    /// </summary>
    public static readonly string[] DangerousExtensions =
    {
        ".exe", ".bat", ".cmd", ".com", ".scr", ".pif", ".msi", ".dll",
        ".vbs", ".vbe", ".js", ".jse", ".ws", ".wsf", ".wsc", ".wsh",
        ".ps1", ".psm1", ".psd1", ".sh", ".bash", ".zsh", ".csh",
        ".py", ".pyw", ".rb", ".pl", ".php", ".jar", ".class",
    };

    /// <summary>
    /// Safe audio file extensions.
    /// </summary>
    public static readonly string[] SafeAudioExtensions =
    {
        ".flac", ".mp3", ".ogg", ".opus", ".m4a", ".aac", ".wav", ".wma",
        ".ape", ".wv", ".tta", ".alac", ".aiff", ".aif",
    };

    // Patterns for dangerous path components
    [GeneratedRegex(@"\.\.[\\/]?|[\\/]\.\.")]
    private static partial Regex TraversalPatternRegex();

    [GeneratedRegex(@"^[a-zA-Z]:")]
    private static partial Regex WindowsDriveRegex();

    [GeneratedRegex(@"[\x00-\x1f]")]
    private static partial Regex ControlCharsRegex();

    [GeneratedRegex(@"%[0-9a-fA-F]{2}")]
    private static partial Regex UrlEncodedRegex();

    /// <summary>
    /// Normalize and validate a peer-supplied path against a root directory.
    /// </summary>
    /// <param name="peerPath">The untrusted path from peer/server.</param>
    /// <param name="root">The trusted root directory all paths must be under.</param>
    /// <returns>Safe absolute path, or null if validation fails.</returns>
    public static string? NormalizeAndValidate(string? peerPath, string root)
    {
        // 1. Null/empty check
        if (string.IsNullOrWhiteSpace(peerPath) || string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        // 2. Length check BEFORE any processing
        if (peerPath.Length > MaxPathLength)
        {
            return null;
        }

        // 3. Normalize unicode (NFC form)
        peerPath = NormalizeUnicode(peerPath);

        // 4. Check for null bytes (can truncate paths in some systems)
        if (peerPath.Contains('\0'))
        {
            return null;
        }

        // 5. Check for control characters
        if (ControlCharsRegex().IsMatch(peerPath))
        {
            return null;
        }

        // 6. Check for URL-encoded traversal attempts
        var decoded = DecodeUrlEncoding(peerPath);
        if (ContainsTraversal(decoded))
        {
            return null;
        }

        // 7. Check for explicit traversal attempts
        if (ContainsTraversal(peerPath))
        {
            return null;
        }

        // 8. Reject absolute paths from peers
        if (Path.IsPathRooted(peerPath))
        {
            return null;
        }

        // 9. Reject Windows drive letters
        if (WindowsDriveRegex().IsMatch(peerPath))
        {
            return null;
        }

        // 10. Check directory depth
        var separatorCount = peerPath.Count(c => c == '/' || c == '\\');
        if (separatorCount > MaxDirectoryDepth)
        {
            return null;
        }

        // 11. Normalize path separators
        var normalizedPath = peerPath.Replace('\\', Path.DirectorySeparatorChar)
                                     .Replace('/', Path.DirectorySeparatorChar);

        // 12. Combine with root and get full path
        try
        {
            var combined = Path.Combine(root, normalizedPath);
            var fullPath = Path.GetFullPath(combined);
            var fullRoot = Path.GetFullPath(root);

            // 13. Ensure root ends with separator for proper prefix matching
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            // 14. Ensure result is still under root (handles symlinks, etc.)
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.Equals(fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // 15. Final length check after normalization
            if (fullPath.Length > MaxPathLength)
            {
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
    /// Check if a path contains traversal sequences.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if traversal sequences detected.</returns>
    public static bool ContainsTraversal(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        // Check for various traversal patterns
        if (TraversalPatternRegex().IsMatch(path))
        {
            return true;
        }

        // Check for standalone ".." components
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(p => p == "..");
    }

    /// <summary>
    /// Sanitize a filename for safe filesystem use.
    /// Removes dangerous characters, normalizes unicode, truncates length.
    /// </summary>
    /// <param name="filename">The filename to sanitize.</param>
    /// <returns>A safe filename.</returns>
    public static string SanitizeFilename(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
        {
            return "unnamed";
        }

        // Normalize unicode
        filename = NormalizeUnicode(filename);

        // Remove path separators (prevent hidden paths in filenames)
        filename = filename.Replace('/', '_').Replace('\\', '_');

        // Remove forbidden characters
        foreach (var c in ForbiddenChars)
        {
            filename = filename.Replace(c, '_');
        }

        // Remove control characters
        filename = ControlCharsRegex().Replace(filename, "_");

        // Remove leading/trailing whitespace and dots
        filename = filename.Trim().Trim('.');

        // Handle empty result
        if (string.IsNullOrWhiteSpace(filename))
        {
            return "unnamed";
        }

        // Truncate to max length while preserving extension
        if (filename.Length > MaxFilenameLength)
        {
            var ext = Path.GetExtension(filename);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(filename);
            var maxNameLength = MaxFilenameLength - ext.Length;
            filename = nameWithoutExt[..Math.Min(nameWithoutExt.Length, maxNameLength)] + ext;
        }

        return filename;
    }

    /// <summary>
    /// Check if a path is safely contained within root (no escape possible).
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="root">The root directory.</param>
    /// <returns>True if path is safely contained.</returns>
    public static bool IsContainedIn(string path, string root)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(root);

            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
            {
                fullRoot += Path.DirectorySeparatorChar;
            }

            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) ||
                   fullPath.Equals(fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Normalize unicode to prevent homoglyph attacks (NFC normalization).
    /// </summary>
    /// <param name="input">The string to normalize.</param>
    /// <returns>NFC-normalized string.</returns>
    public static string NormalizeUnicode(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return input.Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Check if a filename has a dangerous extension.
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
    /// Check if a filename has a safe audio extension.
    /// </summary>
    /// <param name="filename">The filename to check.</param>
    /// <returns>True if the extension is a known safe audio format.</returns>
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
    /// Validate a peer path with detailed error reporting.
    /// </summary>
    /// <param name="peerPath">The path to validate.</param>
    /// <param name="rootDirectory">The root directory.</param>
    /// <returns>Validation result with details.</returns>
    public static PathValidationResult Validate(string? peerPath, string rootDirectory)
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

        if (ContainsTraversal(peerPath) || ContainsTraversal(DecodeUrlEncoding(peerPath)))
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

        var safePath = NormalizeAndValidate(peerPath, rootDirectory);
        if (safePath == null)
        {
            return PathValidationResult.Fail("Path escapes root directory", PathViolationType.EscapedRoot);
        }

        return PathValidationResult.Success(safePath);
    }

    /// <summary>
    /// Decode URL-encoded characters for traversal detection.
    /// Recursively decodes to catch double-encoding attacks like %252e%252e -> %2e%2e -> ..
    /// </summary>
    private static string DecodeUrlEncoding(string input)
    {
        if (!UrlEncodedRegex().IsMatch(input))
        {
            return input;
        }

        try
        {
            var decoded = input;
            var previousDecoded = string.Empty;
            var iterations = 0;
            const int maxIterations = 5; // Prevent infinite loops

            // Keep decoding until no more changes (catches double/triple encoding)
            while (decoded != previousDecoded && iterations < maxIterations)
            {
                previousDecoded = decoded;
                decoded = Uri.UnescapeDataString(decoded);
                iterations++;
            }

            return decoded;
        }
        catch
        {
            return input;
        }
    }
}

/// <summary>
/// Result of path validation.
/// </summary>
public readonly struct PathValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the path is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the safe, normalized path (only set if valid).
    /// </summary>
    public string? SafePath { get; init; }

    /// <summary>
    /// Gets the error message (only set if invalid).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the type of violation detected.
    /// </summary>
    public PathViolationType ViolationType { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static PathValidationResult Success(string safePath) => new()
    {
        IsValid = true,
        SafePath = safePath,
        ViolationType = PathViolationType.None,
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
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
    /// <summary>No violation.</summary>
    None,

    /// <summary>Path is empty or null.</summary>
    Empty,

    /// <summary>Path exceeds maximum length.</summary>
    TooLong,

    /// <summary>Path contains control characters.</summary>
    ControlCharacters,

    /// <summary>Directory traversal attempt detected.</summary>
    DirectoryTraversal,

    /// <summary>Absolute path not allowed.</summary>
    AbsolutePath,

    /// <summary>Path exceeds maximum directory depth.</summary>
    TooDeep,

    /// <summary>Invalid path component.</summary>
    InvalidComponent,

    /// <summary>Path escaped the root directory.</summary>
    EscapedRoot,
}

