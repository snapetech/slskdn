// <copyright file="PathGuard.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.DhtRendezvous.Security;

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommonPathGuard = slskd.Common.Security.PathGuard;

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
        return CommonPathGuard.NormalizeAndValidate(peerPath?.Trim(), rootDirectory?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Validates a filename (without path) for safety.
    /// </summary>
    /// <param name="filename">The filename to validate.</param>
    /// <returns>True if the filename is safe to use.</returns>
    public static bool IsFilenameValid(string? filename)
    {
        filename = filename?.Trim();

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
        filename = filename?.Trim();

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
        filename = filename?.Trim();

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
        var result = CommonPathGuard.Validate(peerPath?.Trim(), rootDirectory?.Trim() ?? string.Empty);

        return result.IsValid
            ? PathValidationResult.Success(result.SafePath!)
            : PathValidationResult.Fail(result.Error ?? "Invalid path", MapViolationType(result.ViolationType));
    }

    private static PathViolationType MapViolationType(slskd.Common.Security.PathViolationType violationType) => violationType switch
    {
        slskd.Common.Security.PathViolationType.None => PathViolationType.None,
        slskd.Common.Security.PathViolationType.Empty => PathViolationType.Empty,
        slskd.Common.Security.PathViolationType.TooLong => PathViolationType.TooLong,
        slskd.Common.Security.PathViolationType.ControlCharacters => PathViolationType.ControlCharacters,
        slskd.Common.Security.PathViolationType.DirectoryTraversal => PathViolationType.DirectoryTraversal,
        slskd.Common.Security.PathViolationType.AbsolutePath => PathViolationType.AbsolutePath,
        slskd.Common.Security.PathViolationType.TooDeep => PathViolationType.TooDeep,
        slskd.Common.Security.PathViolationType.InvalidComponent => PathViolationType.InvalidComponent,
        slskd.Common.Security.PathViolationType.EscapedRoot => PathViolationType.EscapedRoot,
        _ => PathViolationType.InvalidComponent,
    };
}
