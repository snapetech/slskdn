// <copyright file="HashDbMediaVariantStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.MediaCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;
    using slskd.VirtualSoulfind.Core;

    /// <summary>
    ///     IMediaVariantStore: Music via IHashDbService, Image/Video/Generic in-memory. T-911.
    /// </summary>
    public sealed class HashDbMediaVariantStore : IMediaVariantStore
    {
        private readonly IHashDbService _hashDb;
        private readonly ILogger<HashDbMediaVariantStore> _log;

        private readonly Dictionary<string, MediaVariant> _nonMusic = new(StringComparer.Ordinal);
        private readonly object _nonMusicLock = new();

        public HashDbMediaVariantStore(IHashDbService hashDb, ILogger<HashDbMediaVariantStore> log)
        {
            _hashDb = hashDb ?? throw new ArgumentNullException(nameof(hashDb));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <inheritdoc />
        public async Task<MediaVariant?> GetByVariantIdAsync(string variantId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(variantId))
            {
                return null;
            }

            // Music: treat variantId as FlacKey
            var audio = await _hashDb.GetAudioVariantByFlacKeyAsync(variantId, cancellationToken).ConfigureAwait(false);
            if (audio != null)
            {
                return MediaVariant.FromAudioVariant(audio);
            }

            lock (_nonMusicLock)
            {
                return _nonMusic.TryGetValue(variantId, out var v) ? v : null;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaVariant>> GetByRecordingIdAsync(string recordingId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(recordingId))
            {
                return Array.Empty<MediaVariant>();
            }

            var list = await _hashDb.GetVariantsByRecordingAsync(recordingId, cancellationToken).ConfigureAwait(false);
            return list == null
                ? Array.Empty<MediaVariant>()
                : list.ConvertAll(MediaVariant.FromAudioVariant);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaVariant>> GetByDomainAsync(ContentDomain domain, int limit = 100, CancellationToken cancellationToken = default)
        {
            if (domain == ContentDomain.Music)
            {
                var recordingIds = await _hashDb.GetRecordingIdsWithVariantsAsync(cancellationToken).ConfigureAwait(false);
                var results = new List<MediaVariant>();
                foreach (var rid in recordingIds ?? new List<string>())
                {
                    if (results.Count >= limit)
                    {
                        break;
                    }

                    var list = await _hashDb.GetVariantsByRecordingAsync(rid, cancellationToken).ConfigureAwait(false);
                    if (list != null)
                    {
                        foreach (var a in list)
                        {
                            results.Add(MediaVariant.FromAudioVariant(a));
                            if (results.Count >= limit)
                            {
                                break;
                            }
                        }
                    }
                }

                return results;
            }

            lock (_nonMusicLock)
            {
                return _nonMusic.Values
                    .Where(v => v.Domain == domain)
                    .Take(limit)
                    .ToList();
            }
        }

        /// <inheritdoc />
        public async Task UpsertAsync(MediaVariant variant, CancellationToken cancellationToken = default)
        {
            if (variant == null)
            {
                throw new ArgumentNullException(nameof(variant));
            }

            if (variant.Domain == ContentDomain.Music && variant.Audio != null)
            {
                var flacKey = variant.Audio.FlacKey ?? variant.VariantId;
                if (!string.IsNullOrWhiteSpace(flacKey))
                {
                    var existing = await _hashDb.GetAudioVariantByFlacKeyAsync(flacKey, cancellationToken).ConfigureAwait(false);
                    if (existing != null)
                    {
                        await _hashDb.UpdateVariantMetadataAsync(flacKey, variant.Audio, cancellationToken).ConfigureAwait(false);
                        _log.LogDebug("MediaVariant Upsert: updated Music variant {FlacKey}", flacKey);
                        return;
                    }
                }

                _log.LogDebug("MediaVariant Upsert: Music variant {VariantId} has no existing HashDb entry; create via file ingest", variant.VariantId);
                return;
            }

            if (variant.Domain == ContentDomain.Image || variant.Domain == ContentDomain.Video || variant.Domain == ContentDomain.GenericFile)
            {
                lock (_nonMusicLock)
                {
                    _nonMusic[variant.VariantId] = variant;
                }

                _log.LogDebug("MediaVariant Upsert: stored {Domain} variant {VariantId} in-memory", variant.Domain, variant.VariantId);
            }
        }
    }
}
