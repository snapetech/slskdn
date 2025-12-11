namespace slskd.Jobs
{
    using System;
    using System.Collections.Generic;
    using slskd.Integrations.MusicBrainz;

    public enum JobStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
    }

    public class DiscographyJob
    {
        public string JobId { get; set; }

        public string ArtistId { get; set; }

        public string ArtistName { get; set; }

        public DiscographyProfile Profile { get; set; }

        public string TargetDirectory { get; set; }

        public List<string> ReleaseJobIds { get; set; } = new();

        public List<string> ReleaseIds { get; set; } = new();

        public int TotalReleases { get; set; }

        public int CompletedReleases { get; set; }

        public int FailedReleases { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Pending;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    public class DiscographyReleaseJobStatus
    {
        public string ReleaseId { get; set; }

        public JobStatus Status { get; set; } = JobStatus.Pending;
    }
}
