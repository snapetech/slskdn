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

    /// <summary>
    ///     MusicBrainz lookup.
    /// </summary>
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [Produces("application/json")]
    [Consumes("application/json")]
    [Authorize(Policy = AuthPolicy.Any)]
    public class MusicBrainzController : ControllerBase
    {
        private readonly IMusicBrainzClient client;
        private readonly IHashDbService hashDbService;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MusicBrainzController"/> class.
        /// </summary>
        public MusicBrainzController(IMusicBrainzClient client, IHashDbService hashDbService)
        {
            this.client = client;
            this.hashDbService = hashDbService;
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
    }
}

