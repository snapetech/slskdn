// <copyright file="ContentLinkService.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

        // Parse the content ID
        var parsed = ContentIdParser.Parse(contentId);
        if (parsed == null)
        {
            return new ContentValidationResult(false, contentId, "Invalid content ID format. Expected: content:<domain>:<type>:<id>");
        }

        try
        {
            // Validate based on domain and type
            var metadata = await GetContentMetadataAsync(contentId, ct);
            if (metadata == null)
            {
                return new ContentValidationResult(false, contentId, "Content not found or inaccessible");
            }

            return new ContentValidationResult(true, contentId, Metadata: metadata);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error validating content ID {ContentId}", contentId);
            return new ContentValidationResult(false, contentId, $"Validation error: {ex.Message}");
        }
    }

    public async Task<ContentMetadata?> GetContentMetadataAsync(string contentId, CancellationToken ct = default)
    {
        var parsed = ContentIdParser.Parse(contentId);
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
                    // For other domains, return basic metadata without external validation
                    return new ContentMetadata(
                        ContentId: contentId,
                        Title: $"{parsed.Type}: {parsed.Id}",
                        Artist: "Unknown",
                        Type: parsed.Type,
                        Domain: parsed.Domain,
                        AdditionalInfo: new Dictionary<string, string>
                        {
                            ["id"] = parsed.Id,
                            ["domain"] = parsed.Domain,
                            ["type"] = parsed.Type
                        });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching metadata for content ID {ContentId}", contentId);
            return null;
        }
    }

    public Task<IReadOnlyList<ContentSearchResult>> SearchContentAsync(string query, string? domain = null, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<ContentSearchResult>>(Array.Empty<ContentSearchResult>());
        }

        try
        {
            _logger.LogWarning("Content search requested for query '{Query}' (domain: {Domain}), but search integration is not implemented", query, domain ?? "all");
            return Task.FromResult<IReadOnlyList<ContentSearchResult>>(Array.Empty<ContentSearchResult>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching content with query '{Query}'", query);
            return Task.FromResult<IReadOnlyList<ContentSearchResult>>(Array.Empty<ContentSearchResult>());
        }
    }

    private async Task<ContentMetadata?> GetAudioMetadataAsync(ContentId parsed, CancellationToken ct)
    {
        switch (parsed.Type.ToLowerInvariant())
        {
            case ContentDomains.AudioArtist:
                // MusicBrainz artist lookup would go here
                return new ContentMetadata(
                    ContentId: parsed.FullId,
                    Title: parsed.Id, // Would be actual artist name
                    Artist: parsed.Id,
                    Type: ContentDomains.AudioArtist,
                    Domain: ContentDomains.Audio,
                    AdditionalInfo: new Dictionary<string, string>
                    {
                        ["musicbrainz_id"] = parsed.Id,
                        ["type"] = "artist"
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
                            ["album"] = "Unknown" // TrackTarget doesn't have album info
                        });
                }

                break;
        }

        return null;
    }

    private Task<ContentMetadata?> GetVideoMetadataAsync(ContentId parsed, CancellationToken ct)
    {
        _logger.LogWarning("Video metadata requested for {ContentId}, but video metadata integration is not implemented", parsed.FullId);
        return Task.FromResult<ContentMetadata?>(null);
    }
}
