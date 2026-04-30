// <copyright file="MusicBrainzController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Integrations.MusicBrainz.API
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.HashDb;
    using slskd.Integrations.MusicBrainz.API.DTO;
    using slskd.Integrations.MusicBrainz.Models;
    using slskd.Core.Security;

    /// <summary>
    ///     MusicBrainz lookup.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class MusicBrainzController : ControllerBase
    {
        private readonly IMusicBrainzClient client;
        private readonly IHashDbService hashDbService;
        private readonly IArtistReleaseGraphService releaseGraphService;
        private readonly IDiscographyCoverageService discographyCoverageService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicBrainzController"/> class.
        /// </summary>
        public MusicBrainzController(
            IMusicBrainzClient client,
            IHashDbService hashDbService,
            IArtistReleaseGraphService releaseGraphService,
            IDiscographyCoverageService discographyCoverageService)
        {
            this.client = client;
            this.hashDbService = hashDbService;
            this.releaseGraphService = releaseGraphService;
            this.discographyCoverageService = discographyCoverageService;
        }

        /// <summary>
        ///     Resolves album or track metadata.
        /// </summary>
        [HttpPost("targets")]
        public async Task<IActionResult> ResolveTarget(
            [FromBody] MusicBrainzTargetRequest request,
            CancellationToken cancellationToken)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            if (request is null || !request.HasIdentifier)
            {
                return BadRequest("Provide at least one identifier (release, recording, or discogs).");
            }

            request.ReleaseId = string.IsNullOrWhiteSpace(request.ReleaseId) ? null : request.ReleaseId.Trim();
            request.RecordingId = string.IsNullOrWhiteSpace(request.RecordingId) ? null : request.RecordingId.Trim();
            request.DiscogsReleaseId = string.IsNullOrWhiteSpace(request.DiscogsReleaseId) ? null : request.DiscogsReleaseId.Trim();

            AlbumTarget? album = null;
            if (!string.IsNullOrWhiteSpace(request.ReleaseId))
            {
                album = await client.GetReleaseAsync(request.ReleaseId!, cancellationToken).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(request.DiscogsReleaseId))
            {
                album = await client.GetReleaseByDiscogsReleaseIdAsync(request.DiscogsReleaseId!, cancellationToken).ConfigureAwait(false);
            }

            TrackTarget? track = null;
            if (string.IsNullOrWhiteSpace(request.ReleaseId) || AlbumIsMissing(album))
            {
                if (!string.IsNullOrWhiteSpace(request.RecordingId))
                {
                    track = await client.GetRecordingAsync(request.RecordingId!, cancellationToken).ConfigureAwait(false);
                }
            }

            if (album is null && track is null)
            {
                return NotFound();
            }

            if (album is not null)
            {
                await hashDbService.UpsertAlbumTargetAsync(album, cancellationToken).ConfigureAwait(false);
            }

            return Ok(new MusicBrainzTargetResponse
            {
                Album = album,
                Track = track,
            });
        }

        private static bool AlbumIsMissing(AlbumTarget? album) =>
            album is null || string.IsNullOrWhiteSpace(album.MusicBrainzReleaseId);

        /// <summary>
        ///     Returns album completion summaries stored locally.
        /// </summary>
        [HttpGet("albums/completion")]
        public async Task<IActionResult> GetAlbumCompletion(CancellationToken cancellationToken)
        {
            if (Program.IsRelayAgent)
            {
                return Forbid();
            }

            var targets = await hashDbService.GetAlbumTargetsAsync(cancellationToken).ConfigureAwait(false);
            var summaries = new List<AlbumCompletionSummary>();

            foreach (var target in targets)
            {
                var tracks = (await hashDbService.GetAlbumTracksAsync(target.ReleaseId, cancellationToken).ConfigureAwait(false)).ToList();
                var trackSummaries = new List<AlbumCompletionTrack>();
                var completedTracks = 0;

                foreach (var track in tracks)
                {
                    var matches = new List<HashMatch>();
                    var complete = false;

                    if (!string.IsNullOrWhiteSpace(track.RecordingId))
                    {
                        var hashes = await hashDbService.LookupHashesByRecordingIdAsync(track.RecordingId, cancellationToken).ConfigureAwait(false);

                        foreach (var hash in hashes)
                        {
                            matches.Add(new HashMatch
                            {
                                FlacKey = hash.FlacKey,
                                Size = hash.Size,
                                UseCount = hash.UseCount,
                                FirstSeenAt = hash.FirstSeenAt,
                                LastUpdatedAt = hash.LastUpdatedAt,
                            });
                        }

                        if (matches.Count > 0)
                        {
                            complete = true;
                            completedTracks++;
                        }
                    }

                    trackSummaries.Add(new AlbumCompletionTrack
                    {
                        Position = track.Position,
                        Title = track.Title,
                        RecordingId = track.RecordingId,
                        DurationMs = track.DurationMs,
                        Complete = complete,
                        Matches = matches.ToArray(),
                    });
                }

                summaries.Add(new AlbumCompletionSummary
                {
                    ReleaseId = target.ReleaseId,
                    Title = target.Title,
                    Artist = target.Artist,
                    ReleaseDate = target.ReleaseDate,
                    DiscogsReleaseId = target.DiscogsReleaseId,
                    TotalTracks = tracks.Count,
                    CompletedTracks = completedTracks,
                    Tracks = trackSummaries.ToArray(),
                });
            }

            return Ok(new AlbumCompletionResponse
            {
                Albums = summaries.ToArray(),
            });
        }

        /// <summary>
        ///     Fetches (and caches) an artist release graph from MusicBrainz.
        /// </summary>
        [HttpGet("artist/{artistId}/release-graph")]
        public async Task<ActionResult<ArtistReleaseGraph>> GetReleaseGraph(
            string artistId,
            [FromQuery] bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            artistId = artistId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artistId))
            {
                return BadRequest("artistId is required");
            }

            var graph = await releaseGraphService.GetArtistReleaseGraphAsync(artistId, forceRefresh, cancellationToken).ConfigureAwait(false);
            if (graph == null)
            {
                return NotFound();
            }

            return Ok(graph);
        }

        /// <summary>
        ///     Returns a per-release and per-track coverage map for an artist.
        /// </summary>
        [HttpGet("artist/{artistId}/discography-coverage")]
        public async Task<ActionResult<DiscographyCoverageResult>> GetDiscographyCoverage(
            string artistId,
            [FromQuery] DiscographyProfile profile = DiscographyProfile.CoreDiscography,
            [FromQuery] bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            artistId = artistId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artistId))
            {
                return BadRequest("artistId is required");
            }

            var coverage = await discographyCoverageService.GetCoverageAsync(
                new DiscographyCoverageRequest
                {
                    ArtistId = artistId,
                    Profile = profile,
                    ForceRefresh = forceRefresh,
                },
                cancellationToken).ConfigureAwait(false);

            if (coverage == null)
            {
                return NotFound();
            }

            return Ok(coverage);
        }

        /// <summary>
        ///     Seeds missing artist coverage tracks into the Wishlist without running searches.
        /// </summary>
        [HttpPost("artist/{artistId}/discography-coverage/wishlist")]
        public async Task<ActionResult<DiscographyWishlistPromotionResult>> PromoteDiscographyCoverageToWishlist(
            string artistId,
            [FromBody] DiscographyWishlistPromotionRequest request,
            CancellationToken cancellationToken = default)
        {
            artistId = artistId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artistId))
            {
                return BadRequest("artistId is required");
            }

            request ??= new DiscographyWishlistPromotionRequest();
            request.ArtistId = artistId;
            request.Filter = string.IsNullOrWhiteSpace(request.Filter) ? "flac" : request.Filter.Trim();
            if (request.MaxResults <= 0)
            {
                return BadRequest("MaxResults must be greater than 0");
            }

            var result = await discographyCoverageService.PromoteMissingToWishlistAsync(
                request,
                cancellationToken).ConfigureAwait(false);

            return Ok(result);
        }
    }
}
