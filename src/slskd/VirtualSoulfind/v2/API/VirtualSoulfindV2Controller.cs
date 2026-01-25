// <copyright file="VirtualSoulfindV2Controller.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

namespace slskd.VirtualSoulfind.v2.API
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Catalogue;
    using slskd.VirtualSoulfind.v2.Intents;
    using slskd.VirtualSoulfind.v2.Planning;
    using slskd.VirtualSoulfind.v2.Processing;
    using slskd.VirtualSoulfind.v2.Resolution;
    using slskd.VirtualSoulfind.v2.Execution;
    using slskd.Core.Security;

    /// <summary>
    ///     API controller for VirtualSoulfind v2.
    /// </summary>
    [ApiController]
    [Route("api/v1/virtualsoulfind/v2")]
    [AllowAnonymous] // PR-02: intended-public
    [Produces("application/json")]
    [Consumes("application/json")]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class VirtualSoulfindV2Controller : ControllerBase
    {
        private readonly IIntentQueue _intentQueue;
        private readonly ICatalogueStore _catalogueStore;
        private readonly IPlanner _planner;
        private readonly IResolver _resolver;
        private readonly IIntentQueueProcessor _processor;

        /// <summary>
        ///     Initializes a new instance of the <see cref="VirtualSoulfindV2Controller"/> class.
        /// </summary>
        public VirtualSoulfindV2Controller(
            IIntentQueue intentQueue,
            ICatalogueStore catalogueStore,
            IPlanner planner,
            IResolver resolver,
            IIntentQueueProcessor processor)
        {
            _intentQueue = intentQueue ?? throw new ArgumentNullException(nameof(intentQueue));
            _catalogueStore = catalogueStore ?? throw new ArgumentNullException(nameof(catalogueStore));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        }

        /// <summary>
        ///     Enqueues a track for acquisition.
        /// </summary>
        /// <param name="request">The enqueue request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created intent.</returns>
        [HttpPost("intents/tracks")]
        [ProducesResponseType(typeof(DesiredTrack), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EnqueueTrack(
            [FromBody] EnqueueTrackRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // H-VF01: Validate ContentDomain and required fields
            if (!VirtualSoulfindValidation.IsValidContentDomain(request.Domain, out var domainError))
            {
                return BadRequest(domainError);
            }

            if (!VirtualSoulfindValidation.ValidateRequiredFields(
                request.Domain, request.TrackId, null, null, out var fieldError))
            {
                return BadRequest(fieldError);
            }

            if (!VirtualSoulfindValidation.ValidateTrackIdFormat(
                request.Domain, request.TrackId, out var formatError))
            {
                return BadRequest(formatError);
            }

            var intent = await _intentQueue.EnqueueTrackAsync(
                request.Domain,
                request.TrackId,
                request.Priority,
                request.ParentDesiredReleaseId,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetTrackIntent),
                new { intentId = intent.DesiredTrackId },
                intent);
        }

        /// <summary>
        ///     Enqueues a release for acquisition.
        /// </summary>
        /// <param name="request">The enqueue request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The created intent.</returns>
        [HttpPost("intents/releases")]
        [ProducesResponseType(typeof(DesiredRelease), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> EnqueueRelease(
            [FromBody] EnqueueReleaseRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var intent = await _intentQueue.EnqueueReleaseAsync(
                request.ReleaseId,
                request.Priority,
                request.Mode,
                request.Notes,
                cancellationToken);

            return CreatedAtAction(
                nameof(GetReleaseIntent),
                new { intentId = intent.DesiredReleaseId },
                intent);
        }

        /// <summary>
        ///     Gets pending track intents.
        /// </summary>
        /// <param name="limit">Maximum number of intents to return.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of pending intents.</returns>
        [HttpGet("intents/tracks/pending")]
        [ProducesResponseType(typeof(IReadOnlyList<DesiredTrack>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetPendingTracks(
            [FromQuery] int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var intents = await _intentQueue.GetPendingTracksAsync(limit, cancellationToken);
            return Ok(intents);
        }

        /// <summary>
        ///     Gets a track intent by ID.
        /// </summary>
        /// <param name="intentId">The intent ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The intent.</returns>
        [HttpGet("intents/tracks/{intentId}")]
        [ProducesResponseType(typeof(DesiredTrack), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTrackIntent(
            string intentId,
            CancellationToken cancellationToken)
        {
            var intent = await _intentQueue.GetTrackIntentAsync(intentId, cancellationToken);
            
            if (intent == null)
            {
                return NotFound();
            }

            return Ok(intent);
        }

        /// <summary>
        ///     Gets a release intent by ID.
        /// </summary>
        /// <param name="intentId">The intent ID.</param>
        /// <returns>The intent.</returns>
        [HttpGet("intents/releases/{intentId}")]
        [ProducesResponseType(typeof(DesiredRelease), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetReleaseIntent(string intentId)
        {
            // TODO: Implement GetReleaseIntentAsync in IIntentQueue
            return NotFound(new { Message = "Release intent retrieval not yet implemented" });
        }

        /// <summary>
        ///     Updates a track intent status.
        /// </summary>
        /// <param name="intentId">The intent ID.</param>
        /// <param name="request">The update request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>No content on success.</returns>
        [HttpPatch("intents/tracks/{intentId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTrackIntent(
            string intentId,
            [FromBody] UpdateIntentRequest request,
            CancellationToken cancellationToken)
        {
            var intent = await _intentQueue.GetTrackIntentAsync(intentId, cancellationToken);
            
            if (intent == null)
            {
                return NotFound();
            }

            await _intentQueue.UpdateTrackStatusAsync(intentId, request.Status, cancellationToken);
            return NoContent();
        }

        /// <summary>
        ///     Gets processor statistics.
        /// </summary>
        /// <returns>Processor stats.</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(IntentProcessorStats), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _processor.GetStatsAsync();
            return Ok(stats);
        }

        /// <summary>
        ///     Searches the virtual catalogue for artists.
        /// </summary>
        /// <param name="query">Search query.</param>
        /// <param name="limit">Maximum results.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of artists.</returns>
        [HttpGet("catalogue/artists/search")]
        [ProducesResponseType(typeof(IReadOnlyList<Artist>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchArtists(
            [FromQuery] [Required] string query,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { Message = "Query is required" });
            }

            var artists = await _catalogueStore.SearchArtistsAsync(query, limit, cancellationToken);
            return Ok(artists);
        }

        /// <summary>
        ///     Gets an artist by ID.
        /// </summary>
        /// <param name="artistId">The artist ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The artist.</returns>
        [HttpGet("catalogue/artists/{artistId}")]
        [ProducesResponseType(typeof(Artist), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetArtist(
            string artistId,
            CancellationToken cancellationToken)
        {
            var artist = await _catalogueStore.FindArtistByIdAsync(artistId, cancellationToken);
            
            if (artist == null)
            {
                return NotFound();
            }

            return Ok(artist);
        }

        /// <summary>
        ///     Lists releases for an artist.
        /// </summary>
        /// <param name="artistId">The artist ID.</param>
        /// <param name="limit">Maximum results.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of release groups.</returns>
        [HttpGet("catalogue/artists/{artistId}/releases")]
        [ProducesResponseType(typeof(IReadOnlyList<ReleaseGroup>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetArtistReleases(
            string artistId,
            [FromQuery] int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var releases = await _catalogueStore.ListReleaseGroupsForArtistAsync(artistId, cancellationToken);
            
            // Apply limit client-side
            var limited = limit > 0 ? releases.Take(limit).ToList() : releases;
            return Ok(limited);
        }

        /// <summary>
        ///     Gets tracks for a release.
        /// </summary>
        /// <param name="releaseId">The release ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of tracks.</returns>
        [HttpGet("catalogue/releases/{releaseId}/tracks")]
        [ProducesResponseType(typeof(IReadOnlyList<Track>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetReleaseTracks(
            string releaseId,
            CancellationToken cancellationToken)
        {
            var tracks = await _catalogueStore.ListTracksForReleaseAsync(releaseId, cancellationToken);
            return Ok(tracks);
        }

        /// <summary>
        ///     Creates an acquisition plan for a track.
        /// </summary>
        /// <param name="request">The plan request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The acquisition plan.</returns>
        [HttpPost("plans")]
        [ProducesResponseType(typeof(TrackAcquisitionPlan), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreatePlan(
            [FromBody] CreatePlanRequest request,
            CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Create a temporary DesiredTrack for planning
            var desiredTrack = new DesiredTrack
            {
                DesiredTrackId = Guid.NewGuid().ToString(),
                TrackId = request.TrackId,
                Priority = request.Priority ?? IntentPriority.Normal,
                Status = IntentStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };

            var plan = await _planner.CreatePlanAsync(desiredTrack, request.Mode, cancellationToken);
            return Ok(plan);
        }

        /// <summary>
        ///     Gets execution status for a plan.
        /// </summary>
        /// <param name="executionId">The execution ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The execution state.</returns>
        [HttpGet("executions/{executionId}")]
        [ProducesResponseType(typeof(PlanExecutionState), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetExecutionStatus(
            string executionId,
            CancellationToken cancellationToken = default)
        {
            var state = await _resolver.GetExecutionStatusAsync(executionId, cancellationToken);
            
            if (state == null)
            {
                return NotFound();
            }

            return Ok(state);
        }

        /// <summary>
        ///     Manually triggers processing of a specific intent.
        /// </summary>
        /// <param name="intentId">The intent ID to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Processing result.</returns>
        [HttpPost("intents/tracks/{intentId}/process")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ProcessIntent(
            string intentId,
            CancellationToken cancellationToken)
        {
            var intent = await _intentQueue.GetTrackIntentAsync(intentId, cancellationToken);
            
            if (intent == null)
            {
                return NotFound();
            }

            // Process asynchronously
            _ = Task.Run(async () => await _processor.ProcessIntentAsync(intentId, cancellationToken), cancellationToken);

            return Accepted(new { Message = "Processing started", IntentId = intentId });
        }
    }

    #region Request/Response DTOs

    /// <summary>
    ///     Request to enqueue a track.
    /// </summary>
    public sealed class EnqueueTrackRequest
    {
        /// <summary>
        ///     Gets or sets the content domain.
        /// </summary>
        [Required]
        public ContentDomain Domain { get; set; } = ContentDomain.Music;

        /// <summary>
        ///     Gets or sets the track ID.
        /// </summary>
        [Required]
        public string TrackId { get; set; }

        /// <summary>
        ///     Gets or sets the priority.
        /// </summary>
        public IntentPriority Priority { get; set; } = IntentPriority.Normal;

        /// <summary>
        ///     Gets or sets the parent release ID (optional).
        /// </summary>
        public string? ParentDesiredReleaseId { get; set; }
    }

    /// <summary>
    ///     Request to enqueue a release.
    /// </summary>
    public sealed class EnqueueReleaseRequest
    {
        /// <summary>
        ///     Gets or sets the release ID.
        /// </summary>
        [Required]
        public string ReleaseId { get; set; }

        /// <summary>
        ///     Gets or sets the priority.
        /// </summary>
        public IntentPriority Priority { get; set; } = IntentPriority.Normal;

        /// <summary>
        ///     Gets or sets the mode.
        /// </summary>
        public IntentMode Mode { get; set; } = IntentMode.Wanted;

        /// <summary>
        ///     Gets or sets optional notes.
        /// </summary>
        public string? Notes { get; set; }
    }

    /// <summary>
    ///     Request to update an intent.
    /// </summary>
    public sealed class UpdateIntentRequest
    {
        /// <summary>
        ///     Gets or sets the new status.
        /// </summary>
        [Required]
        public IntentStatus Status { get; set; }
    }

    /// <summary>
    ///     Request to create an acquisition plan.
    /// </summary>
    public sealed class CreatePlanRequest
    {
        /// <summary>
        ///     Gets or sets the track ID.
        /// </summary>
        [Required]
        public string TrackId { get; set; }

        /// <summary>
        ///     Gets or sets the planning mode (optional).
        /// </summary>
        public PlanningMode? Mode { get; set; }

        /// <summary>
        ///     Gets or sets the priority (optional).
        /// </summary>
        public IntentPriority? Priority { get; set; }
    }

    #endregion
}
