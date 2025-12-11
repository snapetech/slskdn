namespace slskd.Jobs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;

    public interface ILabelCrateJobService
    {
        Task<string> CreateJobAsync(LabelCrateJobRequest request, CancellationToken ct = default);

        Task<LabelCrateJob?> GetJobAsync(string jobId, CancellationToken ct = default);

        Task SetReleaseStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken ct = default);
    }

    public class LabelCrateJobService : ILabelCrateJobService
    {
        private readonly IHashDbService hashDb;
        private readonly ILogger<LabelCrateJobService> log;

        public LabelCrateJobService(IHashDbService hashDb, ILogger<LabelCrateJobService> log)
        {
            this.hashDb = hashDb;
            this.log = log;
        }

        public async Task<string> CreateJobAsync(LabelCrateJobRequest request, CancellationToken ct = default)
        {
            if (request == null || (string.IsNullOrWhiteSpace(request.LabelId) && string.IsNullOrWhiteSpace(request.LabelName)))
            {
                throw new ArgumentException("LabelId or LabelName is required", nameof(request));
            }

            var limit = request.Limit <= 0 ? 10 : request.Limit;
            var releases = await hashDb.GetReleaseIdsByLabelAsync(request.LabelName ?? request.LabelId, limit, ct).ConfigureAwait(false);

            var job = new LabelCrateJob
            {
                JobId = Guid.NewGuid().ToString("N"),
                LabelId = request.LabelId ?? string.Empty,
                LabelName = request.LabelName ?? request.LabelId ?? string.Empty,
                Limit = limit,
                ReleaseIds = releases.ToList(),
                TotalReleases = releases.Count,
                Status = JobStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var subJobs = releases.Select(id => new DiscographyReleaseJobStatus { ReleaseId = id, Status = JobStatus.Pending }).ToList();
            await hashDb.UpsertLabelCrateReleaseJobsAsync(job.JobId, subJobs, ct).ConfigureAwait(false);
            await hashDb.UpsertLabelCrateJobAsync(job, ct).ConfigureAwait(false);

            log.LogInformation("[LabelCrateJob] Planned job {JobId} for label {Label} with {Count} releases (limit {Limit})",
                job.JobId, job.LabelName, job.TotalReleases, limit);

            return job.JobId;
        }

        public async Task<LabelCrateJob?> GetJobAsync(string jobId, CancellationToken ct = default)
        {
            var job = await hashDb.GetLabelCrateJobAsync(jobId, ct).ConfigureAwait(false);
            if (job == null)
            {
                return null;
            }

            var releaseStatuses = await hashDb.GetLabelCrateReleaseJobsAsync(jobId, ct).ConfigureAwait(false);
            if (releaseStatuses.Count == 0 && job.ReleaseIds?.Count > 0)
            {
                var seeds = job.ReleaseIds.Select(id => new DiscographyReleaseJobStatus { ReleaseId = id, Status = JobStatus.Pending });
                await hashDb.UpsertLabelCrateReleaseJobsAsync(job.JobId, seeds, ct).ConfigureAwait(false);
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

            await hashDb.UpsertLabelCrateJobAsync(job, ct).ConfigureAwait(false);

            return job;
        }

        public async Task SetReleaseStatusAsync(string jobId, string releaseId, JobStatus status, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(jobId) || string.IsNullOrWhiteSpace(releaseId))
            {
                return;
            }

            await hashDb.SetLabelCrateReleaseJobStatusAsync(jobId, releaseId, status, ct).ConfigureAwait(false);
            await GetJobAsync(jobId, ct).ConfigureAwait(false);
        }
    }
}


