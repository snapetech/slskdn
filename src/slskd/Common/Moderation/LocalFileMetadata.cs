// <copyright file="LocalFileMetadata.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Common.Moderation
{
    /// <summary>
    ///     Lightweight metadata about a local file for moderation checks.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This DTO is passed to <see cref="IModerationProvider.CheckLocalFileAsync"/>
    ///         to determine if a file should be blocked, quarantined, or allowed.
    ///     </para>
    ///     <para>
    ///         🔒 SECURITY (docs/MCP-HARDENING.md Section 1):
    ///         - Id: Internal identifier (NOT full path)
    ///         - PrimaryHash: Content hash (NEVER logged in full)
    ///         - MediaInfo: Generic summary only (e.g., "Audio: FLAC", "Book: EPUB")
    ///     </para>
    ///     <para>
    ///         This type is designed to contain ONLY the minimal information needed
    ///         for moderation decisions, avoiding sensitive data exposure.
    ///     </para>
    /// </remarks>
    public sealed class LocalFileMetadata
    {
        /// <summary>
        ///     Gets or initializes the internal file identifier.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is an opaque identifier used for internal tracking.
        ///         It should NOT be the full filesystem path.
        ///     </para>
        ///     <para>
        ///         Examples:
        ///         - Guid.ToString()
        ///         - Filename only (Path.GetFileName())
        ///         - Database primary key
        ///     </para>
        ///     <para>
        ///         🔒 NEVER use full paths like "/home/user/Music/artist/album/track.mp3"
        ///     </para>
        /// </remarks>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the file size in bytes.
        /// </summary>
        public long SizeBytes { get; init; }

        /// <summary>
        ///     Gets or initializes the primary content hash (e.g., SHA256).
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         🔒 CRITICAL (docs/MCP-HARDENING.md Section 1.1):
        ///         This hash MUST NEVER be logged in full.
        ///         - For debugging: Log only first 8 chars
        ///         - For metrics: Do NOT include in labels
        ///         - For errors: Do NOT include in error messages
        ///     </para>
        /// </remarks>
        public string PrimaryHash { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the secondary content hash (optional).
        /// </summary>
        /// <remarks>
        ///     Some domains may use multiple hashes for verification (e.g., MD5 + SHA256).
        ///     Same logging restrictions as <see cref="PrimaryHash"/> apply.
        /// </remarks>
        public string? SecondaryHash { get; init; }

        /// <summary>
        ///     Gets or initializes a generic media info summary.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This should be a HIGH-LEVEL category, not detailed metadata:
        ///         - "Audio: FLAC"
        ///         - "Book: EPUB"
        ///         - "Video: MP4"
        ///         - "GenericFile"
        ///     </para>
        ///     <para>
        ///         Do NOT include:
        ///         - Full paths or filenames
        ///         - Detailed codec/bitrate info
        ///         - Artist/title information
        ///     </para>
        /// </remarks>
        public string? MediaInfo { get; init; }
    }
}
