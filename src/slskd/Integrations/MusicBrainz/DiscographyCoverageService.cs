// <copyright file="DiscographyCoverageService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.HashDb;
using slskd.Integrations.MusicBrainz.API.DTO;
using slskd.Integrations.MusicBrainz.Models;
using slskd.Wishlist;

public interface IDiscographyCoverageService
{
    Task<DiscographyCoverageResult?> GetCoverageAsync(
        DiscographyCoverageRequest request,
        CancellationToken cancellationToken = default);

    Task<DiscographyWishlistPromotionResult> PromoteMissingToWishlistAsync(
        DiscographyWishlistPromotionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DiscographyCoverageService : IDiscographyCoverageService
{
    private readonly IArtistReleaseGraphService releaseGraphService;
    private readonly IDiscographyProfileService profileService;
    private readonly IMusicBrainzClient musicBrainzClient;
    private readonly IHashDbService hashDb;
    private readonly IWishlistService wishlistService;
    private readonly ILogger<DiscographyCoverageService> logger;

    public DiscographyCoverageService(
        IArtistReleaseGraphService releaseGraphService,
        IDiscographyProfileService profileService,
        IMusicBrainzClient musicBrainzClient,
        IHashDbService hashDb,
        IWishlistService wishlistService,
        ILogger<DiscographyCoverageService> logger)
    {
        this.releaseGraphService = releaseGraphService;
        this.profileService = profileService;
        this.musicBrainzClient = musicBrainzClient;
        this.hashDb = hashDb;
        this.wishlistService = wishlistService;
        this.logger = logger;
    }

    public async Task<DiscographyCoverageResult?> GetCoverageAsync(
        DiscographyCoverageRequest request,
        CancellationToken cancellationToken = default)
    {
        var artistId = request.ArtistId.Trim();
        var graph = await releaseGraphService.GetArtistReleaseGraphAsync(
            artistId,
            request.ForceRefresh,
            cancellationToken).ConfigureAwait(false);

        if (graph == null)
        {
            return null;
        }

        var releaseIds = profileService
            .ApplyProfile(graph, DiscographyProfileFilter.FromProfile(request.Profile))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var wishlistKeys = (await wishlistService.ListAsync().ConfigureAwait(false))
            .Select(item => NormalizeKey(item.SearchText, item.Filter))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new DiscographyCoverageResult
        {
            ArtistId = graph.ArtistId,
            ArtistName = graph.Name,
            Profile = request.Profile,
        };

        foreach (var group in graph.ReleaseGroups)
        {
            foreach (var release in group.Releases.Where(release => releaseIds.Contains(release.ReleaseId)))
            {
                var album = await EnsureAlbumTargetAsync(release.ReleaseId, cancellationToken).ConfigureAwait(false);
                if (album == null)
                {
                    result.Releases.Add(new DiscographyCoverageRelease
                    {
                        ReleaseGroupId = group.ReleaseGroupId,
                        ReleaseId = release.ReleaseId,
                        Title = release.Title,
                        ReleaseDate = release.ReleaseDate,
                        Type = group.Type,
                    });
                    continue;
                }

                var tracks = (await hashDb.GetAlbumTracksAsync(release.ReleaseId, cancellationToken).ConfigureAwait(false))
                    .OrderBy(track => track.Position)
                    .ToList();

                var coverageRelease = new DiscographyCoverageRelease
                {
                    ReleaseGroupId = group.ReleaseGroupId,
                    ReleaseId = release.ReleaseId,
                    Title = string.IsNullOrWhiteSpace(album.Title) ? release.Title : album.Title,
                    ReleaseDate = album.Metadata.ReleaseDate?.ToString("yyyy-MM-dd") ?? release.ReleaseDate,
                    Type = group.Type,
                    TotalTracks = tracks.Count,
                };

                foreach (var track in tracks)
                {
                    var coverageTrack = await BuildTrackCoverageAsync(track, wishlistKeys, cancellationToken).ConfigureAwait(false);
                    coverageRelease.Tracks.Add(coverageTrack);

                    if (coverageTrack.Status == DiscographyCoverageStatus.MeshAvailable)
                    {
                        coverageRelease.CoveredTracks++;
                    }
                }

                result.Releases.Add(coverageRelease);
            }
        }

        result.TotalReleases = result.Releases.Count;
        result.CompleteReleases = result.Releases.Count(release => release.Complete);
        result.TotalTracks = result.Releases.Sum(release => release.TotalTracks);
        result.CoveredTracks = result.Releases.Sum(release => release.CoveredTracks);

        return result;
    }

    public async Task<DiscographyWishlistPromotionResult> PromoteMissingToWishlistAsync(
        DiscographyWishlistPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        var coverage = await GetCoverageAsync(
            new DiscographyCoverageRequest
            {
                ArtistId = request.ArtistId,
                Profile = request.Profile,
            },
            cancellationToken).ConfigureAwait(false);

        if (coverage == null)
        {
            throw new NotFoundException($"Artist {request.ArtistId} not found");
        }

        var result = new DiscographyWishlistPromotionResult
        {
            ArtistId = coverage.ArtistId,
        };
        var createdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var track in coverage.Releases.SelectMany(release => release.Tracks))
        {
            if (track.Status != DiscographyCoverageStatus.Absent)
            {
                if (track.Status == DiscographyCoverageStatus.WishlistSeeded)
                {
                    result.AlreadySeededCount++;
                }

                continue;
            }

            var searchText = BuildSearchText(track);
            var key = NormalizeKey(searchText, request.Filter);
            if (!createdKeys.Add(key))
            {
                result.AlreadySeededCount++;
                continue;
            }

            var item = await wishlistService.CreateAsync(new WishlistItem
            {
                SearchText = searchText,
                Filter = request.Filter.Trim(),
                Enabled = true,
                AutoDownload = false,
                MaxResults = request.MaxResults,
            }).ConfigureAwait(false);

            result.CreatedCount++;
            result.CreatedItemIds.Add(item.Id);
        }

        return result;
    }

    private async Task<AlbumTarget?> EnsureAlbumTargetAsync(string releaseId, CancellationToken cancellationToken)
    {
        var existing = await hashDb.GetAlbumTargetAsync(releaseId, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            return new AlbumTarget
            {
                MusicBrainzReleaseId = existing.ReleaseId,
                DiscogsReleaseId = existing.DiscogsReleaseId,
                Title = existing.Title,
                Artist = existing.Artist,
                Metadata = new ReleaseMetadata
                {
                    Country = existing.Country,
                    Label = existing.Label,
                    Status = existing.Status,
                },
            };
        }

        var album = await musicBrainzClient.GetReleaseAsync(releaseId, cancellationToken).ConfigureAwait(false);
        if (album == null)
        {
            logger.LogDebug("[DiscographyCoverage] Release {ReleaseId} could not be resolved", releaseId);
            return null;
        }

        await hashDb.UpsertAlbumTargetAsync(album, cancellationToken).ConfigureAwait(false);
        return album;
    }

    private async Task<DiscographyCoverageTrack> BuildTrackCoverageAsync(
        HashDb.Models.AlbumTargetTrackEntry track,
        HashSet<string> wishlistKeys,
        CancellationToken cancellationToken)
    {
        var result = new DiscographyCoverageTrack
        {
            Position = track.Position,
            Title = track.Title,
            Artist = track.Artist,
            RecordingId = track.RecordingId,
            DurationMs = track.DurationMs,
        };

        if (string.IsNullOrWhiteSpace(track.RecordingId))
        {
            result.Status = DiscographyCoverageStatus.Ambiguous;
            result.Evidence.Add("Missing MusicBrainz recording id");
            return result;
        }

        var hashes = await hashDb.LookupHashesByRecordingIdAsync(track.RecordingId, cancellationToken).ConfigureAwait(false);
        foreach (var hash in hashes)
        {
            result.Matches.Add(new HashMatch
            {
                FlacKey = hash.FlacKey,
                Size = hash.Size,
                UseCount = hash.UseCount,
                FirstSeenAt = hash.FirstSeenAt,
                LastUpdatedAt = hash.LastUpdatedAt,
            });
        }

        if (result.Matches.Count > 0)
        {
            result.Status = DiscographyCoverageStatus.MeshAvailable;
            result.Evidence.Add("HashDb has verified content evidence for this recording");
            return result;
        }

        if (wishlistKeys.Contains(NormalizeKey(BuildSearchText(result), "flac")) ||
            wishlistKeys.Any(key => key.StartsWith(NormalizeSearchText(BuildSearchText(result)) + "\u001f", StringComparison.OrdinalIgnoreCase)))
        {
            result.Status = DiscographyCoverageStatus.WishlistSeeded;
            result.Evidence.Add("Wishlist already has a matching search seed");
            return result;
        }

        result.Status = DiscographyCoverageStatus.Absent;
        return result;
    }

    private static string BuildSearchText(DiscographyCoverageTrack track)
    {
        var artist = string.IsNullOrWhiteSpace(track.Artist) ? string.Empty : track.Artist.Trim();
        var title = string.IsNullOrWhiteSpace(track.Title) ? string.Empty : track.Title.Trim();
        return string.IsNullOrWhiteSpace(artist) ? title : $"{artist} {title}";
    }

    private static string NormalizeKey(string searchText, string filter) =>
        NormalizeSearchText(searchText) + "\u001f" + (filter ?? string.Empty).Trim();

    private static string NormalizeSearchText(string searchText) =>
        (searchText ?? string.Empty).Trim();
}
