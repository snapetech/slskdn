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

            await hashDb.UpsertDiscographyJobAsync(job, ct).ConfigureAwait(false);

            log.LogInformation("[DiscographyJob] Planned job {JobId} for artist {Artist} with {Count} releases (profile {Profile})",
                job.JobId, job.ArtistName, job.TotalReleases, profile);

            return job.JobId;
        }

        public Task<DiscographyJob?> GetJobAsync(string jobId, CancellationToken ct = default)
        {
            return hashDb.GetDiscographyJobAsync(jobId, ct);
        }
    }

    public class DiscographyJobRequest
    {
        public string ArtistId { get; set; }

        public DiscographyProfile Profile { get; set; } = DiscographyProfile.CoreDiscography;

        public string TargetDirectory { get; set; }
    }
}
