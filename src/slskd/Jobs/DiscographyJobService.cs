namespace slskd.Jobs
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;
    using slskd.Integrations.MusicBrainz;

    public interface IDiscographyJobService
    {
        Task<string> CreateJobAsync(DiscographyJobRequest request, CancellationToken ct = default);

        Task<DiscographyJob?> GetJobAsync(string jobId, CancellationToken ct = default);

        Task SetReleaseStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken ct = default);
    }

    public class DiscographyJobService : IDiscographyJobService
    {
        private readonly IDiscographyProfileService profileService;
        private readonly IArtistReleaseGraphService graphService;
        private readonly IHashDbService hashDb;
        private readonly ILogger<DiscographyJobService> log;

        public DiscographyJobService(
            IDiscographyProfileService profileService,
            IArtistReleaseGraphService graphService,
            IHashDbService hashDb,
            ILogger<DiscographyJobService> log)
        {
            this.profileService = profileService;
            this.graphService = graphService;
            this.hashDb = hashDb;
            this.log = log;
        }

        public async Task<string> CreateJobAsync(DiscographyJobRequest request, CancellationToken ct = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ArtistId))
            {
                throw new ArgumentException("ArtistId is required", nameof(request));
            }

            var profile = request.Profile;
            var releaseIds = await profileService.GetReleaseIdsForProfileAsync(request.ArtistId, profile, ct).ConfigureAwait(false);

            var graph = await graphService.GetArtistReleaseGraphAsync(request.ArtistId, forceRefresh: false, ct).ConfigureAwait(false);
            var job = new DiscographyJob
            {
                JobId = Guid.NewGuid().ToString("N"),
                ArtistId = request.ArtistId,
                ArtistName = graph?.Name ?? request.ArtistId,
                Profile = profile,
                TargetDirectory = request.TargetDirectory ?? string.Empty,
                ReleaseIds = releaseIds,
                TotalReleases = releaseIds.Count,
                Status = JobStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            // Seed release sub-jobs as pending
            var releaseJobs = releaseIds
                .Select(id => new DiscographyReleaseJobStatus { ReleaseId = id, Status = JobStatus.Pending })
                .ToList();

            await hashDb.UpsertDiscographyReleaseJobsAsync(job.JobId, releaseJobs, ct).ConfigureAwait(false);
            await hashDb.UpsertDiscographyJobAsync(job, ct).ConfigureAwait(false);

            log.LogInformation("[DiscographyJob] Planned job {JobId} for artist {Artist} with {Count} releases (profile {Profile})",
                job.JobId, job.ArtistName, job.TotalReleases, profile);

            return job.JobId;
        }

        public async Task<DiscographyJob?> GetJobAsync(string jobId, CancellationToken ct = default)
        {
            var job = await hashDb.GetDiscographyJobAsync(jobId, ct).ConfigureAwait(false);
            if (job == null)
            {
                return null;
            }

            var releaseStatuses = await hashDb.GetDiscographyReleaseJobsAsync(jobId, ct).ConfigureAwait(false);
            if (releaseStatuses.Count == 0 && job.ReleaseIds?.Count > 0)
            {
                // No sub-jobs persisted yet, seed them
                var seeds = job.ReleaseIds.Select(id => new DiscographyReleaseJobStatus { ReleaseId = id, Status = JobStatus.Pending });
                await hashDb.UpsertDiscographyReleaseJobsAsync(job.JobId, seeds, ct).ConfigureAwait(false);
                releaseStatuses = seeds.ToList();
            }

            job.TotalReleases = releaseStatuses.Count;
            job.CompletedReleases = releaseStatuses.Count(r => r.Status == JobStatus.Completed);
            job.FailedReleases = releaseStatuses.Count(r => r.Status == JobStatus.Failed);

            var anyRunning = releaseStatuses.Any(r => r.Status == JobStatus.Running);
            var anyPending = releaseStatuses.Any(r => r.Status == JobStatus.Pending);

            job.Status = job.TotalReleases == 0
                ? JobStatus.Pending
                : job.CompletedReleases == job.TotalReleases
                    ? JobStatus.Completed
                    : job.FailedReleases > 0 && !anyRunning && !anyPending
                        ? JobStatus.Failed
                        : JobStatus.Running;

            await hashDb.UpsertDiscographyJobAsync(job, ct).ConfigureAwait(false);

            return job;
        }

        public async Task SetReleaseStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(releaseId))
            {
                return;
            }

            await hashDb.SetDiscographyReleaseJobStatusAsync(jobId, releaseId, status, ct).ConfigureAwait(false);

            // Recalculate aggregate status
            await GetJobAsync(jobId, ct).ConfigureAwait(false);
        }
    }

    public class DiscographyJobRequest
    {
        public string ArtistId { get; set; }

        public DiscographyProfile Profile { get; set; } = DiscographyProfile.CoreDiscography;

        public string TargetDirectory { get; set; }
    }
}
