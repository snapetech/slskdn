// <copyright file="MetadataFacade.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Integrations.MetadataFacade
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using slskd;
    using slskd.Integrations.AcoustId;
    using slskd.Integrations.Chromaprint;
    using slskd.Integrations.MusicBrainz;
    using slskd.Integrations.MusicBrainz.Models;
    using TagLib;

    /// <summary>
    ///     Single facade over MusicBrainz, AcoustID, file tags, and Soulseek metadata. T-912.
    /// </summary>
    public sealed class MetadataFacade : IMetadataFacade
    {
        private readonly IMusicBrainzClient _mb;
        private readonly IAcoustIdClient _acoustId;
        private readonly IFingerprintExtractionService _fingerprint;
        private readonly IOptionsMonitor<slskd.Options> _options;
        private readonly ILogger<MetadataFacade> _log;
        private readonly IMemoryCache? _cache;

        private const int CacheRecordingSeconds = 300;
        private const int CacheFingerprintSeconds = 600;

        public MetadataFacade(
            IMusicBrainzClient musicBrainzClient,
            IAcoustIdClient acoustIdClient,
            IFingerprintExtractionService fingerprintExtractionService,
            IOptionsMonitor<slskd.Options> options,
            ILogger<MetadataFacade> log,
            IMemoryCache? cache = null)
        {
            _mb = musicBrainzClient ?? throw new ArgumentNullException(nameof(musicBrainzClient));
            _acoustId = acoustIdClient ?? throw new ArgumentNullException(nameof(acoustIdClient));
            _fingerprint = fingerprintExtractionService ?? throw new ArgumentNullException(nameof(fingerprintExtractionService));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _cache = cache;
        }

        /// <inheritdoc />
        public async Task<MetadataResult?> GetByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                return null;
            }

            var key = "mf:rec:" + recordingId;
            if (_cache?.TryGetValue(key, out MetadataResult? cached) == true && cached != null)
            {
                return cached;
            }

            var track = await _mb.GetRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);
            if (track is null)
            {
                return null;
            }

            var result = new MetadataResult(
                track.Artist,
                track.Title,
                Album: null,
                track.MusicBrainzRecordingId,
                MusicBrainzReleaseId: null,
                MusicBrainzArtistId: null,
                track.Isrc,
                Year: null,
                Genre: null,
                MetadataResult.SourceMusicBrainz);

            _cache?.Set(key, result, TimeSpan.FromSeconds(CacheRecordingSeconds));
            return result;
        }

        /// <inheritdoc />
        public async Task<MetadataResult?> GetByFingerprintAsync(string fingerprint, int sampleRate, int durationSeconds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return null;
            }

            var cacheKey = $"mf:fp:{fingerprint}:{sampleRate}:{durationSeconds}";
            if (_cache?.TryGetValue(cacheKey, out MetadataResult? cached) == true && cached != null)
            {
                return cached;
            }

            if (!_options.CurrentValue.Integration.AcoustId.Enabled)
            {
                return null;
            }

            var acoust = await _acoustId.LookupAsync(fingerprint, sampleRate, durationSeconds, cancellationToken).ConfigureAwait(false);
            var rec = acoust?.Recordings?.FirstOrDefault();
            var recordingId = rec?.Id;
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                _log.LogDebug("AcoustID did not resolve a recording for fingerprint (len={Len})", fingerprint?.Length ?? 0);
                return null;
            }

            var fromMb = await GetByRecordingIdAsync(recordingId, cancellationToken).ConfigureAwait(false);
            if (fromMb is null)
            {
                return new MetadataResult(
                    rec?.Artists?.FirstOrDefault()?.Name,
                    rec?.Title,
                    Album: null,
                    recordingId,
                    MusicBrainzReleaseId: null,
                    MusicBrainzArtistId: null,
                    Isrc: null,
                    Year: null,
                    Genre: null,
                    MetadataResult.SourceAcoustId);
            }

            var combined = fromMb with { Source = MetadataResult.SourceAcoustId };
            _cache?.Set(cacheKey, combined, TimeSpan.FromSeconds(CacheFingerprintSeconds));
            return combined;
        }

        /// <inheritdoc />
        public async Task<MetadataResult?> GetByFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
            {
                return null;
            }

            // 1) File tags (TagLib)
            try
            {
                using var file = TagLib.File.Create(filePath);
                var tag = file.Tag;
                var artist = tag.FirstPerformer ?? tag.FirstAlbumArtist;
                var title = tag.Title;
                var album = tag.Album;
                var year = tag.Year > 0 ? (int?)tag.Year : null;
                var genre = tag.JoinedGenres;
                string? mbidRec = null, mbidRel = null, mbidArt = null;
                if (tag is TagLib.Ogg.XiphComment xiph)
                {
                    mbidRel = xiph.GetField("MUSICBRAINZ_ALBUMID")?.FirstOrDefault();
                    mbidRec = xiph.GetField("MUSICBRAINZ_TRACKID")?.FirstOrDefault();
                    mbidArt = xiph.GetField("MUSICBRAINZ_ARTISTID")?.FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(artist) || !string.IsNullOrWhiteSpace(title))
                {
                    var fromTags = new MetadataResult(artist, title, album, mbidRec, mbidRel, mbidArt, null, year, genre, MetadataResult.SourceFileTags);
                    if (!string.IsNullOrWhiteSpace(mbidRec))
                    {
                        return fromTags;
                    }

                    // 2) If no MBIDs: try fingerprint → AcoustID → MB
                    if (_options.CurrentValue.Integration.Chromaprint.Enabled && _options.CurrentValue.Integration.AcoustId.Enabled)
                    {
                        var fp = await _fingerprint.ExtractFingerprintAsync(filePath, cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(fp))
                        {
                            var props = file.Properties;
                            var sr = props.AudioSampleRate;
                            var dur = (int)props.Duration.TotalSeconds;
                            if (sr > 0 && dur > 0)
                            {
                                var fromFp = await GetByFingerprintAsync(fp, sr, dur, cancellationToken).ConfigureAwait(false);
                                if (fromFp != null)
                                {
                                    return new MetadataResult(
                                        fromFp.Artist ?? artist,
                                        fromFp.Title ?? title,
                                        fromFp.Album ?? album,
                                        fromFp.MusicBrainzRecordingId ?? mbidRec,
                                        fromFp.MusicBrainzReleaseId ?? mbidRel,
                                        fromFp.MusicBrainzArtistId ?? mbidArt,
                                        fromFp.Isrc,
                                        fromFp.Year ?? year,
                                        fromFp.Genre ?? genre,
                                        MetadataResult.SourceFileTags);
                                }
                            }
                        }
                    }

                    return fromTags;
                }
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "TagLib failed for {File}", filePath);
            }

            return null;
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<MetadataResult> SearchAsync(string query, int limit = 10, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                yield break;
            }

            var hits = await _mb.SearchRecordingsAsync(query, limit, cancellationToken).ConfigureAwait(false);
            if (hits is null)
            {
                yield break;
            }

            foreach (var h in hits)
            {
                yield return new MetadataResult(
                    h.Artist,
                    h.Title,
                    Album: null,
                    h.RecordingId,
                    MusicBrainzReleaseId: null,
                    h.MusicBrainzArtistId,
                    Isrc: null,
                    Year: null,
                    Genre: null,
                    MetadataResult.SourceMusicBrainz);
            }
        }

        /// <inheritdoc />
        public Task<MetadataResult?> GetBySoulseekFilenameAsync(string username, string filename, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filename))
                return Task.FromResult<MetadataResult?>(null);
            var (artist, title, album) = ParseSoulseekFilename(filename);
            if (string.IsNullOrWhiteSpace(artist) && string.IsNullOrWhiteSpace(title))
                return Task.FromResult<MetadataResult?>(null);
            return Task.FromResult<MetadataResult?>(new MetadataResult(
                artist,
                title,
                album,
                MusicBrainzRecordingId: null,
                MusicBrainzReleaseId: null,
                MusicBrainzArtistId: null,
                Isrc: null,
                Year: null,
                Genre: null,
                MetadataResult.SourceSoulseek));
        }

        /// <summary>Parses common Soulseek filename patterns into (Artist, Title, Album).</summary>
        private static (string? artist, string? title, string? album) ParseSoulseekFilename(string filename)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(filename);
            if (string.IsNullOrWhiteSpace(name)) return (null, null, null);
            // "Artist - Title", "Artist – Title" (en-dash), "Artist - Album - 01 - Title", "01 - Title"
            var parts = name.Split(new[] { " - ", " – ", " — " }, StringSplitOptions.None);
            if (parts.Length >= 4 && parts[2].Length <= 3 && System.Text.RegularExpressions.Regex.IsMatch(parts[2].Trim(), @"^\d{1,3}$"))
            {
                return (parts[0].Trim(), parts[3].Trim(), parts[1].Trim());
            }
            if (parts.Length >= 2)
            {
                return (parts[0].Trim(), parts[1].Trim(), null);
            }
            if (parts.Length == 1)
            {
                var s = parts[0].Trim();
                if (System.Text.RegularExpressions.Regex.IsMatch(s, @"^\d{1,3}[\.)\-\s]\s*(.+)$"))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(s, @"^\d{1,3}[\.)\-\s]\s*(.+)$");
                    return (null, m.Groups[1].Value.Trim(), null);
                }
                return (null, s, null);
            }
            return (null, null, null);
        }
    }
}
