// <copyright file="CandidateFileMetadata.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Matching
{
    /// <summary>
    ///     Metadata about a candidate file for matching purposes.
    /// </summary>
    /// <remarks>
    ///     This is a sanitized DTO containing only the information needed for matching.
    ///     No full paths, no peer IDs, no sensitive data.
    /// </remarks>
    public sealed class CandidateFileMetadata
    {
        /// <summary>
        ///     Gets or initializes the filename (basename only, no path).
        /// </summary>
        public string Filename { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the file extension (e.g., ".flac", ".mp3").
        /// </summary>
        public string Extension { get; init; } = string.Empty;

        /// <summary>
        ///     Gets or initializes the file size in bytes.
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        ///     Gets or initializes the duration in seconds (if audio file).
        /// </summary>
        public int? DurationSeconds { get; init; }

        /// <summary>
        ///     Gets or initializes the SHA256 hash (if available).
        /// </summary>
        public string? Hash { get; init; }

        /// <summary>
        ///     Gets or initializes the Chromaprint fingerprint (if available).
        /// </summary>
        public string? Chromaprint { get; init; }

        /// <summary>
        ///     Gets or initializes embedded metadata (artist, title, album from tags).
        /// </summary>
        public EmbeddedMetadata? Embedded { get; init; }
    }

    /// <summary>
    ///     Embedded metadata from file tags.
    /// </summary>
    public sealed class EmbeddedMetadata
    {
        /// <summary>
        ///     Gets or initializes the artist name.
        /// </summary>
        public string? Artist { get; init; }

        /// <summary>
        ///     Gets or initializes the title.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        ///     Gets or initializes the album name.
        /// </summary>
        public string? Album { get; init; }

        /// <summary>
        ///     Gets or initializes the MusicBrainz recording ID (if present in tags).
        /// </summary>
        public string? MusicBrainzRecordingId { get; init; }
    }
}
