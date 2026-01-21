// <copyright file="HashDbController.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.HashDb.API
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using slskd.HashDb.Models;
    using slskd.Search;
    using slskd.Core.Security;

    /// <summary>
    ///     Hash Database API controller.
    /// </summary>
    [Route("api/v{version:apiVersion}/hashdb")]
    [ApiVersion("0")]
    [ApiController]
    [ValidateCsrfForCookiesOnly] // CSRF protection for cookie-based auth (exempts JWT/API key)
    public class HashDbController : ControllerBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="HashDbController"/> class.
        /// </summary>
        public HashDbController(IHashDbService hashDb, IDbContextFactory<SearchDbContext> searchContextFactory)
        {
            HashDb = hashDb;
            SearchContextFactory = searchContextFactory;
        }

        private IHashDbService HashDb { get; }
        private IDbContextFactory<SearchDbContext> SearchContextFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<HashDbController>();

        /// <summary>
        ///     Gets database statistics.
        /// </summary>
        [HttpGet("stats")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetStats()
        {
            var stats = HashDb.GetStats();
            return Ok(stats);
        }

        /// <summary>
        ///     Gets database schema version and migration status.
        /// </summary>
        [HttpGet("schema")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GetSchemaInfo()
        {
            var currentVersion = HashDb.GetSchemaVersion();
            var targetVersion = Migrations.HashDbMigrations.CurrentVersion;

            return Ok(new
            {
                currentVersion,
                targetVersion,
                isUpToDate = currentVersion >= targetVersion,
                message = currentVersion >= targetVersion
                    ? "Schema is up to date"
                    : $"Schema needs migration from v{currentVersion} to v{targetVersion}",
            });
        }

        /// <summary>
        ///     Looks up a hash by FLAC key.
        /// </summary>
        [HttpGet("hash/{flacKey}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> LookupHash(string flacKey)
        {
            var entry = await HashDb.LookupHashAsync(flacKey);
            if (entry == null)
            {
                return NotFound(new { error = "No hash found for key " + flacKey });
            }

            return Ok(entry);
        }

        /// <summary>
        ///     Looks up hashes by file size.
        /// </summary>
        [HttpGet("hash/by-size/{size}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> LookupHashesBySize(long size)
        {
            var entries = await HashDb.LookupHashesBySizeAsync(size);
            return Ok(new
            {
                size,
                count = entries.Count(),
                entries,
            });
        }

        /// <summary>
        ///     Generates a FLAC key for a filename and size.
        /// </summary>
        [HttpGet("key")]
        [Authorize(Policy = AuthPolicy.Any)]
        public IActionResult GenerateKey([FromQuery] string filename, [FromQuery] long size)
        {
            if (string.IsNullOrEmpty(filename) || size <= 0)
            {
                return BadRequest(new { error = "filename and size are required" });
            }

            var key = HashDbEntry.GenerateFlacKey(filename, size);
            return Ok(new { filename, size, flacKey = key });
        }

        /// <summary>
        ///     Gets FLAC inventory entries by size.
        /// </summary>
        [HttpGet("inventory/by-size/{size}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetInventoryBySize(long size, [FromQuery] int limit = 100)
        {
            var entries = await HashDb.GetFlacEntriesBySizeAsync(size, limit);
            return Ok(new
            {
                size,
                count = entries.Count(),
                entries,
            });
        }

        /// <summary>
        ///     Gets unhashed FLAC files pending verification.
        /// </summary>
        [HttpGet("inventory/unhashed")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetUnhashedFiles([FromQuery] int limit = 50)
        {
            var entries = await HashDb.GetUnhashedFlacFilesAsync(limit);
            return Ok(new
            {
                count = entries.Count(),
                entries,
            });
        }

        /// <summary>
        ///     Gets backfill candidates (files that need hash probing).
        /// </summary>
        [HttpGet("backfill/candidates")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetBackfillCandidates([FromQuery] int limit = 10)
        {
            var entries = await HashDb.GetBackfillCandidatesAsync(limit);
            return Ok(new
            {
                count = entries.Count(),
                entries,
            });
        }

        /// <summary>
        ///     Gets all tracked peers.
        /// </summary>
        [HttpGet("peers")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetPeers()
        {
            var peers = await HashDb.GetSlskdnPeersAsync();
            return Ok(new
            {
                count = peers.Count(),
                peers = peers.Select(p => new
                {
                    peerId = p.PeerId,
                    caps = p.Caps,
                    capsFlags = p.CapabilityFlags.ToString(),
                    clientVersion = p.ClientVersion,
                    lastSeen = p.LastSeenUtc,
                    backfillsToday = p.BackfillsToday,
                }),
            });
        }

        /// <summary>
        ///     Gets entries since a sequence ID (for mesh sync).
        /// </summary>
        [HttpGet("sync/since/{sinceSeq}")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> GetEntriesSinceSeq(long sinceSeq, [FromQuery] int limit = 1000)
        {
            var entries = await HashDb.GetEntriesSinceSeqAsync(sinceSeq, limit);
            var latestSeq = await HashDb.GetLatestSeqIdAsync();

            return Ok(new
            {
                sinceSeq,
                latestSeq,
                count = entries.Count(),
                entries,
            });
        }

        /// <summary>
        ///     Receives entries from mesh sync.
        /// </summary>
        [HttpPost("sync/merge")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> MergeEntries([FromBody] MergeRequest request)
        {
            if (request == null || request.Entries == null || !request.Entries.Any())
            {
                return BadRequest(new { error = "entries required" });
            }

            var merged = await HashDb.MergeEntriesFromMeshAsync(request.Entries);
            var latestSeq = await HashDb.GetLatestSeqIdAsync();

            return Ok(new
            {
                received = request.Entries.Count(),
                merged,
                latestSeq,
            });
        }

        /// <summary>
        ///     Stores a hash from verification result.
        /// </summary>
        [HttpPost("hash")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> StoreHash([FromBody] StoreHashRequest request)
        {
            if (string.IsNullOrEmpty(request?.Filename) || request.Size <= 0 || string.IsNullOrEmpty(request.ByteHash))
            {
                return BadRequest(new { error = "filename, size, and byteHash are required" });
            }

            await HashDb.StoreHashFromVerificationAsync(
                request.Filename,
                request.Size,
                request.ByteHash,
                request.SampleRate,
                request.Channels,
                request.BitDepth);

            var key = HashDbEntry.GenerateFlacKey(request.Filename, request.Size);
            return Ok(new { flacKey = key, stored = true });
        }

        /// <summary>
        ///     Backfills FLAC inventory from existing search history.
        ///     Processes searches in batches, tracking progress so subsequent calls continue where left off.
        /// </summary>
        /// <param name="batchSize">Number of searches to process per call (default 50, max 500).</param>
        /// <param name="reset">If true, resets progress and starts from the beginning.</param>
        [HttpPost("backfill/from-history")]
        [Authorize(Policy = AuthPolicy.Any)]
        public async Task<IActionResult> BackfillFromHistory([FromQuery] int batchSize = 50, [FromQuery] bool reset = false)
        {
            // Clamp batch size to reasonable values
            batchSize = Math.Clamp(batchSize, 10, 500);

            // Get or reset the last processed search timestamp
            var lastProcessedAt = reset ? (DateTimeOffset?)null : await HashDb.GetBackfillProgressAsync();

            using var context = SearchContextFactory.CreateDbContext();

            // Count total searches and remaining
            var totalSearches = await context.Searches.CountAsync();
            var remainingSearches = lastProcessedAt.HasValue
                ? await context.Searches.CountAsync(s => s.StartedAt < lastProcessedAt.Value.UtcDateTime)
                : totalSearches;

            if (remainingSearches == 0 && !reset)
            {
                return Ok(new
                {
                    searchesProcessed = 0,
                    flacsDiscovered = 0,
                    totalSearches,
                    remainingSearches = 0,
                    complete = true,
                    message = "Backfill complete - all searches have been processed. Use reset=true to start over.",
                });
            }

            Log.Information("[HashDb] Starting backfill batch (size: {BatchSize}, from: {From})", batchSize, lastProcessedAt?.ToString() ?? "beginning");

            // Get next batch of searches, oldest first (so we process in chronological order)
            var query = context.Searches.AsNoTracking();

            if (lastProcessedAt.HasValue)
            {
                query = query.Where(s => s.StartedAt < lastProcessedAt.Value.UtcDateTime);
            }

            var searches = await query
                .OrderByDescending(s => s.StartedAt) // Most recent unprocessed first
                .Take(batchSize)
                .ToListAsync();

            var totalFlacs = 0;
            var searchesProcessed = 0;
            DateTimeOffset? oldestProcessed = null;

            foreach (var search in searches)
            {
                if (search.Responses != null && search.Responses.Any())
                {
                    var flacs = await HashDb.BackfillFromSearchResponsesAsync(search.Responses);
                    totalFlacs += flacs;
                }

                searchesProcessed++;
                oldestProcessed = search.StartedAt;
            }

            // Save progress (the oldest timestamp we processed, so next batch gets older ones)
            if (oldestProcessed.HasValue)
            {
                await HashDb.SetBackfillProgressAsync(oldestProcessed.Value);
            }

            var newRemaining = remainingSearches - searchesProcessed;
            var isComplete = newRemaining <= 0;

            Log.Information("[HashDb] Backfill batch complete: {Searches} searches, {Flacs} FLACs, {Remaining} remaining", searchesProcessed, totalFlacs, newRemaining);

            return Ok(new
            {
                searchesProcessed,
                flacsDiscovered = totalFlacs,
                totalSearches,
                remainingSearches = Math.Max(0, newRemaining),
                complete = isComplete,
                message = isComplete
                    ? $"Backfill complete! Processed {searchesProcessed} searches, discovered {totalFlacs} FLACs."
                    : $"Processed {searchesProcessed} searches, discovered {totalFlacs} FLACs. {newRemaining} searches remaining - click again to continue.",
            });
        }
    }

    /// <summary>
    ///     Request to merge entries from mesh sync.
    /// </summary>
    public class MergeRequest
    {
        /// <summary>Gets or sets entries to merge.</summary>
        public HashDbEntry[] Entries { get; set; }
    }

    /// <summary>
    ///     Request to store a hash.
    /// </summary>
    public class StoreHashRequest
    {
        /// <summary>Gets or sets the filename.</summary>
        public string Filename { get; set; }

        /// <summary>Gets or sets the file size.</summary>
        public long Size { get; set; }

        /// <summary>Gets or sets the SHA256 hash of first 32KB.</summary>
        public string ByteHash { get; set; }

        /// <summary>Gets or sets the sample rate.</summary>
        public int? SampleRate { get; set; }

        /// <summary>Gets or sets the number of channels.</summary>
        public int? Channels { get; set; }

        /// <summary>Gets or sets the bit depth.</summary>
        public int? BitDepth { get; set; }
    }
}


