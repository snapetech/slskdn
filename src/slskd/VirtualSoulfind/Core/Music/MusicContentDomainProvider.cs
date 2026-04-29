// <copyright file="MusicContentDomainProvider.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Core.Music
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.Common.Moderation;
    using slskd.HashDb;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     Music domain provider implementation for VirtualSoulfind v2.
    /// </summary>
    /// <remarks>
    ///     T-VC02: Music Domain Provider implementation.
    ///     Wraps existing music identity logic and provides domain-neutral mappings.
    ///     Integrates MusicBrainz IDs, Chromaprint fingerprinting, and tag-based matching.
    /// </remarks>
    public sealed class MusicContentDomainProvider : IMusicContentDomainProvider
    {
        private readonly ILogger<MusicContentDomainProvider> _logger;
        private readonly IHashDbService _hashDb;
        private const int VariantBackfillScanLimit = 256;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicContentDomainProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="hashDb">The hash database service for music metadata.</param>
        public MusicContentDomainProvider(
            ILogger<MusicContentDomainProvider> logger,
            IHashDbService hashDb)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hashDb = hashDb ?? throw new ArgumentNullException(nameof(hashDb));
        }

        /// <inheritdoc/>
        public async Task<MusicWork?> TryGetWorkByReleaseIdAsync(string releaseId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(releaseId))
            {
                return null;
            }

            try
            {
                // Query HashDb for album by MusicBrainz Release ID
                var album = await _hashDb.GetAlbumTargetAsync(releaseId, cancellationToken);
                if (album == null)
                {
                    _logger.LogDebug("No album found for MusicBrainz Release ID: {ReleaseId}", releaseId);
                    return null;
                }

                // Convert to MusicWork
                var work = MusicWork.FromAlbumEntry(album);
                _logger.LogDebug("Resolved work {WorkId} for MusicBrainz Release ID: {ReleaseId}", work.Id, releaseId);
                return work;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve work for MusicBrainz Release ID: {ReleaseId}", releaseId);
                return null;
            }
        }

        /// <inheritdoc/>
        public Task<MusicWork?> TryGetWorkByTitleArtistAsync(string title, string artist, int? year = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            {
                return Task.FromResult<MusicWork?>(null);
            }

            return TryGetWorkByTitleArtistInternalAsync(title, artist, year, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<MusicItem?> TryGetItemByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                return Task.FromResult<MusicItem?>(null);
            }

            return TryGetItemByRecordingIdInternalAsync(recordingId, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<MusicItem?> TryGetItemByLocalMetadataAsync(LocalFileMetadata fileMetadata, AudioTags tags, CancellationToken cancellationToken = default)
        {
            if (tags == null || string.IsNullOrWhiteSpace(tags.Title) || string.IsNullOrWhiteSpace(tags.Artist))
            {
                _logger.LogDebug("Insufficient metadata for local file matching: {SanitizedPath}", LoggingSanitizer.SanitizeFilePath(fileMetadata.Id));
                return null;
            }

            try
            {
                // Try MusicBrainz ID first if available
                if (!string.IsNullOrWhiteSpace(tags.MusicBrainzRecordingId))
                {
                    var item = await TryGetItemByRecordingIdAsync(tags.MusicBrainzRecordingId, cancellationToken);
                    if (item != null)
                    {
                        return item;
                    }
                }

                var albums = await _hashDb.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false);
                var normalizedTitle = NormalizeText(tags.Title);
                var normalizedArtist = NormalizeText(tags.Artist);
                var normalizedAlbum = NormalizeText(tags.Album);

                foreach (var album in albums)
                {
                    if (!string.IsNullOrWhiteSpace(normalizedAlbum) &&
                        !string.Equals(NormalizeText(album.Title), normalizedAlbum, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var tracks = await _hashDb.GetAlbumTracksAsync(album.ReleaseId, cancellationToken).ConfigureAwait(false);
                    var track = tracks.FirstOrDefault(candidate =>
                        string.Equals(NormalizeText(candidate.Title), normalizedTitle, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(NormalizeText(candidate.Artist), normalizedArtist, StringComparison.OrdinalIgnoreCase));

                    if (track == null)
                    {
                        continue;
                    }

                    var isAdvertisable = (await _hashDb.LookupHashesByRecordingIdAsync(track.RecordingId, cancellationToken).ConfigureAwait(false)).Any();
                    return MusicItem.FromTrackEntry(track, isAdvertisable);
                }

                var fallbackItem = await TryGetFallbackItemByVariantAsync(
                    normalizedTitle,
                    normalizedArtist,
                    normalizedAlbum,
                    cancellationToken).ConfigureAwait(false);
                if (fallbackItem != null)
                {
                    return fallbackItem;
                }

                _logger.LogDebug("No exact metadata match found for local file: {Title} / {Artist}", tags.Title, tags.Artist);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve item from local metadata: {SanitizedPath}", LoggingSanitizer.SanitizeFilePath(fileMetadata.Id));
                return null;
            }
        }

        /// <inheritdoc/>
        public Task<MusicItem?> TryMatchTrackByFingerprintAsync(string fingerprint, int durationSeconds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return Task.FromResult<MusicItem?>(null);
            }

            try
            {
                return TryMatchTrackByFingerprintInternalAsync(fingerprint, durationSeconds, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to match track by fingerprint: {Fingerprint}", fingerprint);
                return Task.FromResult<MusicItem?>(null);
            }
        }

        /// <summary>
        ///     Gets recently added music items.
        /// </summary>
        public async Task<IReadOnlyList<MusicItem>> GetRecentItemsAsync(int count = 50, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return Array.Empty<MusicItem>();
            }

            try
            {
                var items = new List<MusicItem>(count);
                var albums = await _hashDb.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false);

                foreach (var album in albums.OrderByDescending(entry => entry.CreatedAt))
                {
                    if (items.Count >= count)
                    {
                        break;
                    }

                    var tracks = await _hashDb.GetAlbumTracksAsync(album.ReleaseId, cancellationToken).ConfigureAwait(false);
                    foreach (var track in tracks.OrderBy(track => track.Position))
                    {
                        var isAdvertisable = (await _hashDb.LookupHashesByRecordingIdAsync(track.RecordingId, cancellationToken).ConfigureAwait(false)).Any();
                        items.Add(MusicItem.FromTrackEntry(track, isAdvertisable));

                        if (items.Count >= count)
                        {
                            break;
                        }
                    }
                }

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve recent music items");
                return Array.Empty<MusicItem>();
            }
        }

        /// <summary>
        ///     Parses the year from a release date string (YYYY-MM-DD format).
        /// </summary>
        private static int? ParseYearFromReleaseDate(string? releaseDate)
        {
            if (string.IsNullOrWhiteSpace(releaseDate))
            {
                return null;
            }

            // Release date is stored as YYYY-MM-DD, so just take first 4 chars
            if (releaseDate.Length >= 4 && int.TryParse(releaseDate.Substring(0, 4), out var year))
            {
                return year;
            }

            return null;
        }

        private async Task<MusicWork?> TryGetWorkByTitleArtistInternalAsync(string title, string artist, int? year, CancellationToken cancellationToken)
        {
            try
            {
                var normalizedTitle = NormalizeText(title);
                var normalizedArtist = NormalizeText(artist);
                var albums = await _hashDb.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false);

                var match = albums.FirstOrDefault(album =>
                    string.Equals(NormalizeText(album.Title), normalizedTitle, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(NormalizeText(album.Artist), normalizedArtist, StringComparison.OrdinalIgnoreCase) &&
                    (!year.HasValue || ParseYearFromReleaseDate(album.ReleaseDate) == year));

                if (match == null)
                {
                    _logger.LogDebug("No exact work match found for {Title} / {Artist} ({Year})", title, artist, year);
                    return null;
                }

                return MusicWork.FromAlbumEntry(match);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve work by title/artist: {Title} / {Artist}", title, artist);
                return null;
            }
        }

        private async Task<MusicItem?> TryGetItemByRecordingIdInternalAsync(string recordingId, CancellationToken cancellationToken)
        {
            try
            {
                var hashes = await _hashDb.LookupHashesByRecordingIdAsync(recordingId, cancellationToken).ConfigureAwait(false);
                var isAdvertisable = hashes.Any();
                var variants = await _hashDb.GetVariantsByRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);

                var albums = await _hashDb.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var album in albums)
                {
                    var tracks = await _hashDb.GetAlbumTracksAsync(album.ReleaseId, cancellationToken).ConfigureAwait(false);
                    var track = tracks.FirstOrDefault(candidate =>
                        string.Equals(candidate.RecordingId, recordingId, StringComparison.OrdinalIgnoreCase));

                    if (track != null)
                    {
                        return MusicItem.FromTrackEntry(track, isAdvertisable);
                    }
                }

                var bestVariant = variants
                    .OrderByDescending(variant => variant.QualityScore)
                    .ThenByDescending(variant => variant.SeenCount)
                    .FirstOrDefault();
                if (bestVariant != null)
                {
                    return MusicItem.FromRecordingFallback(
                        recordingId,
                        DeriveFallbackTitle(bestVariant),
                        null,
                        bestVariant.DurationMs > 0 ? bestVariant.DurationMs : null,
                        isAdvertisable);
                }

                _logger.LogDebug("No track entry found for MusicBrainz Recording ID: {RecordingId}", recordingId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve item for MusicBrainz Recording ID: {RecordingId}", recordingId);
                return null;
            }
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private async Task<MusicItem?> TryGetFallbackItemByVariantAsync(
            string title,
            string artist,
            string? album,
            CancellationToken cancellationToken)
        {
            var recordingIds = await _hashDb.GetRecordingIdsWithVariantsAsync(cancellationToken).ConfigureAwait(false);
            foreach (var recordingId in recordingIds.Take(VariantBackfillScanLimit))
            {
                var variants = await _hashDb.GetVariantsByRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);
                var bestVariant = variants
                    .OrderByDescending(variant => variant.QualityScore)
                    .ThenByDescending(variant => variant.SeenCount)
                    .FirstOrDefault();
                if (bestVariant == null)
                {
                    continue;
                }

                var candidateTitle = NormalizeText(DeriveFallbackTitle(bestVariant));
                if (!string.Equals(candidateTitle, title, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var isAdvertisable = true;
                return MusicItem.FromRecordingFallback(
                    recordingId,
                    DeriveFallbackTitle(bestVariant),
                    artist,
                    bestVariant.DurationMs > 0 ? bestVariant.DurationMs : null,
                    isAdvertisable);
            }

            return null;
        }

        private static string DeriveFallbackTitle(slskd.Audio.AudioVariant variant)
        {
            if (!string.IsNullOrWhiteSpace(variant.VariantId))
            {
                return variant.VariantId;
            }

            if (!string.IsNullOrWhiteSpace(variant.FlacKey))
            {
                return variant.FlacKey;
            }

            return variant.MusicBrainzRecordingId;
        }

        private async Task<MusicItem?> TryMatchTrackByFingerprintInternalAsync(
            string fingerprint,
            int durationSeconds,
            CancellationToken cancellationToken)
        {
            var matches = await _hashDb.LookupHashesByAudioFingerprintAsync(fingerprint, cancellationToken).ConfigureAwait(false);
            var bestMatch = matches
                .Where(match => !string.IsNullOrWhiteSpace(match.MusicBrainzId))
                .OrderBy(match =>
                {
                    var durationMs = match.DurationMs ?? 0;
                    var deltaSeconds = Math.Abs((durationMs / 1000) - durationSeconds);
                    return deltaSeconds;
                })
                .ThenByDescending(match => match.QualityScore ?? 0.0)
                .ThenByDescending(match => match.UseCount)
                .FirstOrDefault();

            if (bestMatch == null)
            {
                _logger.LogDebug("No fingerprint match found for duration {DurationSeconds}s", durationSeconds);
                return null;
            }

            return await TryGetItemByRecordingIdInternalAsync(bestMatch.MusicBrainzId, cancellationToken).ConfigureAwait(false);
        }
    }
}
