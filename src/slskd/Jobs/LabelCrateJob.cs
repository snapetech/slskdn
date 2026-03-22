// <copyright file="LabelCrateJob.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Jobs
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    public class LabelCrateJob
    {
        public string JobId { get; set; }

        public string LabelId { get; set; }

        public string LabelName { get; set; }

        public int Limit { get; set; }

        public List<string> ReleaseIds { get; set; } = new();

        public int TotalReleases { get; set; }

        public int CompletedReleases { get; set; }

        public int FailedReleases { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Pending;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class LabelCrateJobRequest
    {
        [JsonPropertyName("label_id")]
        public string? LabelId { get; set; }

        [JsonPropertyName("label_name")]
        public string? LabelName { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 10;
    }
}
