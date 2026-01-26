// <copyright file="MediaVariant.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore
{
    using System;
    using slskd.Audio;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Generalizes AudioVariant to multi-domain media (Music, Image, Video, Generic). T-911.
    /// </summary>
    /// <remarks>
    ///     For Music, embeds <see cref="Audio"/> (AudioVariant). Image/Video have placeholders.
    ///     Migration: use <see cref="FromAudioVariant"/> to build from existing AudioVariant.
    /// </remarks>
    public class MediaVariant
    {
        /// <summary>Content domain (Music, Image, Video, GenericFile).</summary>
        public ContentDomain Domain { get; set; }

        /// <summary>Unique variant id (e.g. FlacKey for Music).</summary>
        public string VariantId { get; set; } = string.Empty;

        /// <summary>First seen.</summary>
        public DateTimeOffset FirstSeenAt { get; set; }

        /// <summary>Last seen.</summary>
        public DateTimeOffset LastSeenAt { get; set; }

        /// <summary>Seen count.</summary>
        public int SeenCount { get; set; }

        /// <summary>File SHA256 (common).</summary>
        public string? FileSha256 { get; set; }

        /// <summary>File size in bytes (common).</summary>
        public long? FileSizeBytes { get; set; }

        /// <summary>Audio-specific data when Domain == Music.</summary>
        public Audio.AudioVariant? Audio { get; set; }

        /// <summary>Image placeholder: dimensions (e.g. "1920x1080"). Domain == Image.</summary>
        public string? ImageDimensions { get; set; }

        /// <summary>Image placeholder: codec. Domain == Image.</summary>
        public string? ImageCodec { get; set; }

        /// <summary>Video placeholder: dimensions. Domain == Video.</summary>
        public string? VideoDimensions { get; set; }

        /// <summary>Video placeholder: codec. Domain == Video.</summary>
        public string? VideoCodec { get; set; }

        /// <summary>Video placeholder: duration seconds. Domain == Video.</summary>
        public int? VideoDurationSeconds { get; set; }

        /// <summary>
        ///     Builds a MediaVariant from an AudioVariant (Domain=Music, same fields). T-911 migration.
        /// </summary>
        public static MediaVariant FromAudioVariant(AudioVariant a)
        {
            if (a == null)
            {
                throw new ArgumentNullException(nameof(a));
            }

            return new MediaVariant
            {
                Domain = ContentDomain.Music,
                VariantId = a.VariantId ?? a.FlacKey ?? string.Empty,
                FirstSeenAt = a.FirstSeenAt,
                LastSeenAt = a.LastSeenAt,
                SeenCount = a.SeenCount,
                FileSha256 = a.FileSha256,
                FileSizeBytes = a.FileSizeBytes,
                Audio = a,
            };
        }

        /// <summary>
        ///     Returns the embedded AudioVariant when Domain==Music, otherwise null.
        /// </summary>
        public AudioVariant? ToAudioVariant() => Domain == ContentDomain.Music ? Audio : null;
    }
}
