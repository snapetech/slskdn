// <copyright file="JobsController.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Jobs.API
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using slskd.Authentication;
    using slskd.Core.Security;
    using slskd.HashDb;

    [ApiController]
    [Route("api/v{version:apiVersion}/jobs")]
    [ApiVersion("0")]
    [Authorize(Policy = AuthPolicy.Any)]
    [ValidateCsrfForCookiesOnly]
    public class JobsController : ControllerBase
    {
        private readonly IHashDbService hashDb;

        public JobsController(IHashDbService hashDb)
        {
            this.hashDb = hashDb;
        }

        /// <summary>
        ///     Lists jobs (discography + label crate) with optional filtering, sorting, and paging.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<object>> List(
            [FromQuery] string? type,
            [FromQuery] string? status,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            CancellationToken cancellationToken)
        {
            var all = new List<JobSummary>();

            var wantsDiscography = string.IsNullOrWhiteSpace(type)
                || string.Equals(type, "discography", StringComparison.OrdinalIgnoreCase);
            var wantsLabelCrate = string.IsNullOrWhiteSpace(type)
                || string.Equals(type, "label_crate", StringComparison.OrdinalIgnoreCase);

            if (wantsDiscography)
            {
                var rows = await hashDb.ListDiscographyJobsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var j in rows)
                {
                    all.Add(new JobSummary
                    {
                        Id = j.JobId,
                        Type = "discography",
                        Status = j.Status.ToString().ToLowerInvariant(),
                        CreatedAt = j.CreatedAt,
                        Progress = new JobProgress
                        {
                            ReleasesTotal = j.TotalReleases,
                            ReleasesDone = j.CompletedReleases,
                            ReleasesFailed = j.FailedReleases,
                        },
                    });
                }
            }

            if (wantsLabelCrate)
            {
                var rows = await hashDb.ListLabelCrateJobsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var j in rows)
                {
                    all.Add(new JobSummary
                    {
                        Id = j.JobId,
                        Type = "label_crate",
                        Status = j.Status.ToString().ToLowerInvariant(),
                        CreatedAt = j.CreatedAt,
                        Progress = new JobProgress
                        {
                            ReleasesTotal = j.TotalReleases,
                            ReleasesDone = j.CompletedReleases,
                            ReleasesFailed = j.FailedReleases,
                        },
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                all = all
                    .Where(j => string.Equals(j.Status, status, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var descending = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            var sortKey = (sortBy ?? "created_at").ToLowerInvariant();

            IEnumerable<JobSummary> ordered = sortKey switch
            {
                "id" => descending
                    ? all.OrderByDescending(j => j.Id, StringComparer.OrdinalIgnoreCase)
                    : all.OrderBy(j => j.Id, StringComparer.OrdinalIgnoreCase),
                "status" => descending
                    ? all.OrderByDescending(j => j.Status, StringComparer.OrdinalIgnoreCase)
                    : all.OrderBy(j => j.Status, StringComparer.OrdinalIgnoreCase),
                _ => descending
                    ? all.OrderByDescending(j => j.CreatedAt)
                    : all.OrderBy(j => j.CreatedAt),
            };

            var orderedList = ordered.ToList();
            var total = orderedList.Count;

            var off = Math.Max(0, offset ?? 0);
            var lim = limit.HasValue && limit.Value > 0 ? limit.Value : total;
            var page = orderedList.Skip(off).Take(lim).ToList();

            return Ok(new JobListResponse
            {
                Jobs = page,
                Total = total,
                HasMore = off + page.Count < total,
            });
        }

        private sealed class JobListResponse
        {
            [JsonPropertyName("jobs")]
            public List<JobSummary> Jobs { get; set; } = new();

            [JsonPropertyName("total")]
            public int Total { get; set; }

            [JsonPropertyName("has_more")]
            public bool HasMore { get; set; }
        }

        private sealed class JobSummary
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("created_at")]
            public DateTimeOffset CreatedAt { get; set; }

            [JsonPropertyName("progress")]
            public JobProgress Progress { get; set; } = new();
        }

        private sealed class JobProgress
        {
            [JsonPropertyName("releases_total")]
            public int ReleasesTotal { get; set; }

            [JsonPropertyName("releases_done")]
            public int ReleasesDone { get; set; }

            [JsonPropertyName("releases_failed")]
            public int ReleasesFailed { get; set; }
        }
    }
}
