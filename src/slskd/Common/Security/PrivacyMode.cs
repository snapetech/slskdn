// <copyright file="PrivacyMode.cs" company="slskdN">
//     Copyright (c) slskdN. All rights reserved.
//     Licensed under the AGPL-3.0 license. See LICENSE file in the project root for full license information.
// </copyright>

namespace slskd.Common.Security;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// Provides privacy-enhancing features to minimize metadata exposure.
/// SECURITY: Reduces information leakage to adversarial peers and servers.
/// </summary>
public static partial class PrivacyMode
{
    /// <summary>
    /// Gets or sets a value indicating whether privacy mode is enabled globally.
    /// </summary>
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Default generic client description when privacy mode is enabled.
    /// </summary>
    public const string GenericDescription = "Music enthusiast";

    /// <summary>
    /// Default generic client version string.
    /// </summary>
    public const string GenericClientVersion = "slskd";

    // Patterns that might leak information
    [GeneratedRegex(@"[A-Z]:\\", RegexOptions.Compiled)]
    private static partial Regex WindowsPathRegex();

    [GeneratedRegex(@"/home/[^/]+", RegexOptions.Compiled)]
    private static partial Regex LinuxHomeRegex();

    [GeneratedRegex(@"/Users/[^/]+", RegexOptions.Compiled)]
    private static partial Regex MacHomeRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", RegexOptions.Compiled)]
    private static partial Regex IpAddressRegex();

    /// <summary>
    /// Information leakage types that can be detected.
    /// </summary>
    [Flags]
    public enum LeakageType
    {
        None = 0,
        WindowsPath = 1,
        UnixHomePath = 2,
        EmailAddress = 4,
        IpAddress = 8,
        Hostname = 16,
        Username = 32,
    }

    /// <summary>
    /// Sanitize a user description to remove potentially identifying information.
    /// </summary>
    /// <param name="description">The original description.</param>
    /// <returns>Sanitized description, or generic if privacy mode enabled.</returns>
    public static string SanitizeDescription(string? description)
    {
        if (IsEnabled || string.IsNullOrWhiteSpace(description))
        {
            return GenericDescription;
        }

        var sanitized = description;

        // Remove Windows paths
        sanitized = WindowsPathRegex().Replace(sanitized, "[path]");

        // Remove Unix home paths
        sanitized = LinuxHomeRegex().Replace(sanitized, "/home/[user]");
        sanitized = MacHomeRegex().Replace(sanitized, "/Users/[user]");

        // Remove email addresses
        sanitized = EmailRegex().Replace(sanitized, "[email]");

        // Remove IP addresses
        sanitized = IpAddressRegex().Replace(sanitized, "[ip]");

        return sanitized;
    }

    /// <summary>
    /// Get a privacy-safe client version string.
    /// </summary>
    /// <param name="actualVersion">The actual version string.</param>
    /// <returns>Version string appropriate for privacy settings.</returns>
    public static string GetClientVersion(string actualVersion)
    {
        return IsEnabled ? GenericClientVersion : actualVersion;
    }

    /// <summary>
    /// Sanitize file paths in share announcements to prevent information leakage.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="virtualRoot">Virtual root to show instead of real path.</param>
    /// <returns>Sanitized path.</returns>
    public static string SanitizeSharePath(string path, string virtualRoot = "Music")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        // Remove drive letters
        var sanitized = WindowsPathRegex().Replace(path, string.Empty);

        // Remove home directory components
        sanitized = LinuxHomeRegex().Replace(sanitized, virtualRoot);
        sanitized = MacHomeRegex().Replace(sanitized, virtualRoot);

        // Normalize path separators
        sanitized = sanitized.Replace('\\', '/');

        // Remove leading slashes
        sanitized = sanitized.TrimStart('/');

        // If path is now empty, use virtual root
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return virtualRoot;
        }

        return sanitized;
    }

    /// <summary>
    /// Check if a string contains potentially sensitive information.
    /// </summary>
    /// <param name="text">The text to check.</param>
    /// <returns>Types of leakage detected.</returns>
    public static LeakageType DetectLeakage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return LeakageType.None;
        }

        var leakage = LeakageType.None;

        if (WindowsPathRegex().IsMatch(text))
        {
            leakage |= LeakageType.WindowsPath;
        }

        if (LinuxHomeRegex().IsMatch(text) || MacHomeRegex().IsMatch(text))
        {
            leakage |= LeakageType.UnixHomePath;
        }

        if (EmailRegex().IsMatch(text))
        {
            leakage |= LeakageType.EmailAddress;
        }

        if (IpAddressRegex().IsMatch(text))
        {
            leakage |= LeakageType.IpAddress;
        }

        return leakage;
    }

    /// <summary>
    /// Generate a per-session random identifier for overlay protocols.
    /// Different each time to prevent tracking across sessions.
    /// </summary>
    /// <returns>Random session identifier.</returns>
    public static string GenerateSessionId()
    {
        return Guid.NewGuid().ToString("N")[..16];
    }

    /// <summary>
    /// Get features to advertise based on privacy settings.
    /// </summary>
    /// <param name="actualFeatures">Actual supported features.</param>
    /// <returns>Features appropriate for privacy settings.</returns>
    public static IReadOnlyList<string> GetAdvertisedFeatures(IEnumerable<string> actualFeatures)
    {
        if (!IsEnabled)
        {
            return actualFeatures.ToList();
        }

        // In privacy mode, only advertise essential features
        var essential = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "mesh_sync",
            "multi_source",
        };

        return actualFeatures.Where(f => essential.Contains(f)).ToList();
    }

    /// <summary>
    /// Determine if auto-joining public rooms should be allowed.
    /// </summary>
    /// <returns>True if auto-join is allowed.</returns>
    public static bool AllowAutoJoinRooms()
    {
        // In privacy mode, don't auto-join to avoid presence tracking
        return !IsEnabled;
    }

    /// <summary>
    /// Sanitize a username for logging (partial redaction).
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>Partially redacted username.</returns>
    public static string RedactUsername(string? username)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length <= 3)
        {
            return "***";
        }

        // Show first and last character, redact middle
        return $"{username[0]}***{username[^1]}";
    }

    /// <summary>
    /// Configuration for privacy mode.
    /// </summary>
    public sealed class PrivacyConfig
    {
        /// <summary>Gets or sets whether privacy mode is enabled.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets whether to minimize metadata in protocol messages.</summary>
        public bool MinimizeMetadata { get; set; } = true;

        /// <summary>Gets or sets whether to use generic client identifier.</summary>
        public bool UseGenericClient { get; set; } = true;

        /// <summary>Gets or sets whether to prevent auto-joining rooms.</summary>
        public bool PreventAutoJoin { get; set; } = true;

        /// <summary>Gets or sets whether to randomize session identifiers.</summary>
        public bool RandomizeSessionId { get; set; } = true;

        /// <summary>Gets or sets whether to sanitize share paths.</summary>
        public bool SanitizeSharePaths { get; set; } = true;

        /// <summary>Gets or sets custom description (used if not generic).</summary>
        public string? CustomDescription { get; set; }
    }
}

