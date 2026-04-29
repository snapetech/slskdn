// <copyright file="ContentLinkService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Integrations.MusicBrainz;
using slskd.MediaCore;

/// <summary>
///     Service for validating and resolving content links for pod creation.
/// </summary>
public class ContentLinkService : IContentLinkService
{
    private readonly IMusicBrainzClient _musicBrainzClient;
    private readonly ILogger<ContentLinkService> _logger;

    public ContentLinkService(
        IMusicBrainzClient musicBrainzClient,
        ILogger<ContentLinkService> logger)
    {
        _musicBrainzClient = musicBrainzClient;
        _logger = logger;
    }

    public async Task<ContentValidationResult> ValidateContentIdAsync(string contentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return new ContentValidationResult(false, contentId, "Content ID cannot be empty");
        }

        var normalizedContentId = contentId.Trim();

        // Parse the content ID
        var parsed = ContentIdParser.Parse(normalizedContentId);
        if (parsed == null)
        {
            return new ContentValidationResult(false, normalizedContentId, "Invalid content ID format. Expected: content:<domain>:<type>:<id>");
        }

        try
        {
            // Validate based on domain and type
            var metadata = await GetContentMetadataAsync(normalizedContentId, ct);
            if (metadata == null)
            {
                return new ContentValidationResult(false, normalizedContentId, "Content not found or inaccessible");
            }

            return new ContentValidationResult(true, normalizedContentId, Metadata: metadata);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating content ID {ContentId}", normalizedContentId);
            return new ContentValidationResult(false, normalizedContentId, "Validation failed");
        }
    }

    public async Task<ContentMetadata?> GetContentMetadataAsync(string contentId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(contentId))
        {
            return null;
        }

        var normalizedContentId = contentId.Trim();
        var parsed = ContentIdParser.Parse(normalizedContentId);
        if (parsed == null)
        {
            return null;
        }

        try
        {
            switch (parsed.Domain.ToLowerInvariant())
            {
                case ContentDomains.Audio:
                    return await GetAudioMetadataAsync(parsed, ct);

                case ContentDomains.Video:
                    return await GetVideoMetadataAsync(parsed, ct);

                default:
                    return CreateBasicMetadata(parsed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching metadata for content ID {ContentId}", normalizedContentId);
            return null;
        }
    }

    public async Task<IReadOnlyList<ContentSearchResult>> SearchContentAsync(string query, string? domain = null, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ContentSearchResult>();
        }

        if (limit <= 0)
        {
            return Array.Empty<ContentSearchResult>();
        }

        try
        {
            var normalizedQuery = query.Trim();
            var normalizedDomain = domain?.Trim();
            var effectiveLimit = Math.Min(limit, 100);

            if (!string.IsNullOrWhiteSpace(normalizedDomain) &&
                !string.Equals(normalizedDomain, ContentDomains.Audio, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Content search requested for unsupported domain '{Domain}'", normalizedDomain);
                return Array.Empty<ContentSearchResult>();
            }

            var hits = await _musicBrainzClient.SearchRecordingsAsync(normalizedQuery, effectiveLimit, ct);
            return hits
                .Where(hit => !string.IsNullOrWhiteSpace(hit.RecordingId))
                .Select(hit => new ContentSearchResult(
                    ContentId: $"content:{ContentDomains.Audio}:{ContentDomains.AudioTrack}:{hit.RecordingId.Trim()}",
                    Title: hit.Title,
                    Subtitle: hit.Artist,
                    Type: ContentDomains.AudioTrack,
                    Domain: ContentDomains.Audio,
                    Metadata: new Dictionary<string, string>
                    {
                        ["musicbrainz_recording_id"] = hit.RecordingId.Trim(),
                        ["artist"] = hit.Artist,
                        ["title"] = hit.Title,
                        ["musicbrainz_artist_id"] = hit.MusicBrainzArtistId ?? string.Empty,
                    }))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching content with query '{Query}'", query);
            return Array.Empty<ContentSearchResult>();
        }
    }

    private async Task<ContentMetadata?> GetAudioMetadataAsync(ContentId parsed, CancellationToken ct)
    {
        switch (parsed.Type.ToLowerInvariant())
        {
            case ContentDomains.AudioArtist:
                var artistHits = await _musicBrainzClient.SearchRecordingsAsync(parsed.Id, 10, ct);
                var matchingArtist = artistHits.FirstOrDefault(hit =>
                    string.Equals(hit.MusicBrainzArtistId?.Trim(), parsed.Id, StringComparison.OrdinalIgnoreCase))
                    ?? artistHits.FirstOrDefault(hit =>
                        string.Equals(hit.Artist?.Trim(), parsed.Id, StringComparison.OrdinalIgnoreCase));

                return CreateBasicMetadata(
                    parsed,
                    titleOverride: matchingArtist?.Artist ?? parsed.Id,
                    artistOverride: matchingArtist?.Artist ?? parsed.Id,
                    new Dictionary<string, string>
                    {
                        ["musicbrainz_id"] = parsed.Id,
                        ["type"] = "artist",
                        ["musicbrainz_artist_id"] = matchingArtist?.MusicBrainzArtistId?.Trim() ?? parsed.Id,
                    });

            case ContentDomains.AudioAlbum:
                // Try to get release metadata from MusicBrainz
                var release = await _musicBrainzClient.GetReleaseAsync(parsed.Id, ct);
                if (release != null)
                {
                    return new ContentMetadata(
                        ContentId: parsed.FullId,
                        Title: release.Title,
                        Artist: release.Artist,
                        Type: ContentDomains.AudioAlbum,
                        Domain: ContentDomains.Audio,
                        AdditionalInfo: new Dictionary<string, string>
                        {
                            ["musicbrainz_id"] = parsed.Id,
                            ["release_date"] = release.Metadata.ReleaseDate?.ToString("yyyy-MM-dd") ?? "Unknown",
                            ["track_count"] = release.Tracks.Count.ToString(),
                            ["label"] = release.Metadata.Label ?? "Unknown"
                        });
                }

                break;

            case ContentDomains.AudioTrack:
                // Try to get recording metadata from MusicBrainz
                var recording = await _musicBrainzClient.GetRecordingAsync(parsed.Id, ct);
                if (recording != null)
                {
                    return new ContentMetadata(
                        ContentId: parsed.FullId,
                        Title: recording.Title,
                        Artist: recording.Artist,
                        Type: ContentDomains.AudioTrack,
                        Domain: ContentDomains.Audio,
                        AdditionalInfo: new Dictionary<string, string>
                        {
                            ["musicbrainz_id"] = parsed.Id,
                            ["duration_ms"] = ((int)recording.Duration.TotalMilliseconds).ToString(),
                            ["album"] = "Unknown",
                            ["position"] = recording.Position.ToString()
                        });
                }

                break;
        }

        return CreateBasicMetadata(parsed);
    }

    private Task<ContentMetadata?> GetVideoMetadataAsync(ContentId parsed, CancellationToken ct)
    {
        return Task.FromResult<ContentMetadata?>(CreateBasicMetadata(parsed, titleOverride: parsed.Id));
    }

    private static ContentMetadata CreateBasicMetadata(
        ContentId parsed,
        string? titleOverride = null,
        string? artistOverride = null,
        Dictionary<string, string>? additionalInfo = null)
    {
        var metadata = additionalInfo ?? new Dictionary<string, string>();
        metadata["id"] = parsed.Id;
        metadata["domain"] = parsed.Domain;
        metadata["type"] = parsed.Type;

        return new ContentMetadata(
            ContentId: parsed.FullId,
            Title: titleOverride ?? $"{parsed.Type}: {parsed.Id}",
            Artist: artistOverride ?? "Unknown",
            Type: parsed.Type,
            Domain: parsed.Domain,
            AdditionalInfo: metadata);
    }
}
