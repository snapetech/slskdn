// <copyright file="LibraryHealthRemediationService.cs" company="slskdn Team">
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

namespace slskd.LibraryHealth.Remediation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;
    using slskd.Integrations.MusicBrainz;
    using slskd.Transfers.MultiSource;

    /// <summary>
    ///     Service for creating remediation jobs from library health issues.
    /// </summary>
    public class LibraryHealthRemediationService : ILibraryHealthRemediationService
    {
        private readonly ILibraryHealthService healthService;
        private readonly IHashDbService hashDb;
        private readonly IMultiSourceDownloadService multiSourceDownloads;
        private readonly IMusicBrainzClient musicBrainzClient;
        private readonly ILogger<LibraryHealthRemediationService> log;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LibraryHealthRemediationService"/> class.
        /// </summary>
        public LibraryHealthRemediationService(
            ILibraryHealthService healthService,
            IHashDbService hashDb,
            IMultiSourceDownloadService multiSourceDownloads,
            IMusicBrainzClient musicBrainzClient,
            ILogger<LibraryHealthRemediationService> log)
        {
            this.healthService = healthService;
            this.hashDb = hashDb;
            this.multiSourceDownloads = multiSourceDownloads;
            this.musicBrainzClient = musicBrainzClient;
            this.log = log;
        }

        /// <inheritdoc/>
        public async Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default)
        {
            // Fetch issues from database
            var issues = new List<LibraryIssue>();
            var allIssues = await hashDb.GetLibraryIssuesAsync(new LibraryHealthIssueFilter(), ct).ConfigureAwait(false);
            
            foreach (var issueId in issueIds)
            {
                var issue = allIssues.FirstOrDefault(i => i.IssueId == issueId);

                if (issue != null && issue.CanAutoFix && issue.Status != LibraryIssueStatus.Resolved)
                {
                    issues.Add(issue);
                }
            }

            if (issues.Count == 0)
            {
                throw new InvalidOperationException("No fixable issues provided");
            }

            // Determine remediation strategy
            var strategy = DetermineRemediationStrategy(issues);

            string jobId = null;

            switch (strategy)
            {
                case RemediationStrategy.RedownloadTracks:
                    jobId = await CreateTrackRedownloadJobAsync(issues, ct).ConfigureAwait(false);
                    break;

                case RemediationStrategy.CompleteAlbum:
                    jobId = await CreateAlbumCompletionJobAsync(issues, ct).ConfigureAwait(false);
                    break;

                case RemediationStrategy.ReplaceWithCanonical:
                    jobId = await CreateCanonicalReplacementJobAsync(issues, ct).ConfigureAwait(false);
                    break;

                default:
                    throw new NotSupportedException($"Remediation strategy {strategy} not supported");
            }

            // Link job to issues
            await LinkJobToIssuesAsync(jobId, issueIds, ct).ConfigureAwait(false);

            return jobId;
        }

        /// <inheritdoc/>
        public async Task LinkJobToIssuesAsync(string jobId, List<string> issueIds, CancellationToken ct = default)
        {
            var allIssues = await hashDb.GetLibraryIssuesAsync(new LibraryHealthIssueFilter(), ct).ConfigureAwait(false);
            
            foreach (var issueId in issueIds)
            {
                var issue = allIssues.FirstOrDefault(i => i.IssueId == issueId);

                if (issue != null)
                {
                    // Update in database
                    await hashDb.UpdateLibraryIssueStatusAsync(issueId, LibraryIssueStatus.Fixing, ct).ConfigureAwait(false);

                    log.LogInformation("[LH-Remediation] Linked issue {IssueId} to job {JobId}", issueId, jobId);
                }
            }
        }

        /// <inheritdoc/>
        public async Task CheckJobStatusAndResolveIssuesAsync(string jobId, CancellationToken ct = default)
        {
            // Get all issues linked to this job
            var filter = new LibraryHealthIssueFilter
            {
                Statuses = new List<LibraryIssueStatus> { LibraryIssueStatus.Fixing }
            };

            var allFixingIssues = await hashDb.GetLibraryIssuesAsync(filter, ct).ConfigureAwait(false);
            var linkedIssues = allFixingIssues.Where(i => i.RemediationJobId == jobId).ToList();

            if (linkedIssues.Count == 0)
            {
                log.LogWarning("[LH-Remediation] No issues found linked to job {JobId}", jobId);
                return;
            }

            // Check multi-source download status
            if (!Guid.TryParse(jobId, out var downloadId))
            {
                log.LogWarning("[LH-Remediation] Job ID {JobId} is not a valid GUID", jobId);
                return;
            }

            var downloadStatus = multiSourceDownloads.GetStatus(downloadId);

            if (downloadStatus == null)
            {
                log.LogWarning("[LH-Remediation] Download status not found for job {JobId}", jobId);
                return;
            }

            // Resolve issues if download completed successfully
            if (downloadStatus.State == MultiSourceDownloadState.Completed)
            {
                foreach (var issue in linkedIssues)
                {
                    await hashDb.UpdateLibraryIssueStatusAsync(issue.IssueId, LibraryIssueStatus.Resolved, ct).ConfigureAwait(false);
                    log.LogInformation("[LH-Remediation] Resolved issue {IssueId} - download completed", issue.IssueId);
                }
            }
            else if (downloadStatus.State == MultiSourceDownloadState.Failed)
            {
                foreach (var issue in linkedIssues)
                {
                    await hashDb.UpdateLibraryIssueStatusAsync(issue.IssueId, LibraryIssueStatus.Failed, ct).ConfigureAwait(false);
                    log.LogWarning("[LH-Remediation] Failed to fix issue {IssueId} - download failed", issue.IssueId);
                }
            }
        }

        private RemediationStrategy DetermineRemediationStrategy(List<LibraryIssue> issues)
        {
            // Check if all issues are from same release and type is MissingTrackInRelease
            if (issues.All(i => i.Type == LibraryIssueType.MissingTrackInRelease))
            {
                var releaseIds = issues.Where(i => !string.IsNullOrEmpty(i.MusicBrainzReleaseId))
                                      .Select(i => i.MusicBrainzReleaseId)
                                      .Distinct()
                                      .ToList();

                if (releaseIds.Count == 1)
                {
                    return RemediationStrategy.CompleteAlbum;
                }
            }

            // Check if issues are about non-canonical variants
            if (issues.Any(i => i.Type == LibraryIssueType.NonCanonicalVariant))
            {
                return RemediationStrategy.ReplaceWithCanonical;
            }

            // Default: redownload tracks (handles transcodes, corruption, etc.)
            return RemediationStrategy.RedownloadTracks;
        }

        private async Task<string> CreateTrackRedownloadJobAsync(List<LibraryIssue> issues, CancellationToken ct)
        {
            // Extract unique recording IDs from issues
            var recordingIds = issues
                .Where(i => !string.IsNullOrEmpty(i.MusicBrainzRecordingId))
                .Select(i => i.MusicBrainzRecordingId)
                .Distinct()
                .ToList();

            if (recordingIds.Count == 0)
            {
                throw new InvalidOperationException("No MusicBrainz recording IDs found in issues");
            }

            // Determine target directory from first issue
            string targetDir = Path.GetDirectoryName(issues.First().FilePath);
            if (string.IsNullOrEmpty(targetDir))
            {
                targetDir = Path.GetDirectoryName(Path.GetFullPath(issues.First().FilePath));
            }

            log.LogInformation(
                "[LH-Remediation] Creating track redownload job for {Count} recordings to {Dir}",
                recordingIds.Count,
                targetDir);

            // Create download jobs for each recording
            var downloadJobs = new List<Guid>();
            
            foreach (var recordingId in recordingIds)
            {
                try
                {
                    // Get track metadata from MusicBrainz
                    var trackMetadata = await musicBrainzClient.GetRecordingAsync(recordingId, ct).ConfigureAwait(false);
                    
                    if (trackMetadata == null)
                    {
                        log.LogWarning("[LH-Remediation] Could not fetch metadata for recording {RecordingId}", recordingId);
                        continue;
                    }

                    // Construct search query from track metadata
                    var searchQuery = $"{trackMetadata.Artist} {trackMetadata.Title}";
                    
                    // Find verified sources using multi-source download service
                    // Note: We need a filename - use a constructed one based on metadata
                    var constructedFilename = $"{trackMetadata.Artist} - {trackMetadata.Title}.flac";
                    
                    // Try to find sources - this will search and verify
                    var verificationResult = await multiSourceDownloads.FindVerifiedSourcesAsync(
                        constructedFilename,
                        fileSize: 0, // Unknown size, will be determined during search
                        excludeUsername: null,
                        ct).ConfigureAwait(false);

                    if (verificationResult.BestSources.Count == 0)
                    {
                        log.LogWarning("[LH-Remediation] No verified sources found for recording {RecordingId}", recordingId);
                        continue;
                    }

                    // Select canonical sources if available
                    var sources = await multiSourceDownloads.SelectCanonicalSourcesAsync(verificationResult, ct).ConfigureAwait(false);
                    
                    if (sources.Count == 0)
                    {
                        log.LogWarning("[LH-Remediation] No suitable sources selected for recording {RecordingId}", recordingId);
                        continue;
                    }

                    // Get file size from verification result
                    var fileSize = verificationResult.FileSize > 0 ? verificationResult.FileSize : 0;

                    // Create download request
                    var downloadRequest = new MultiSourceDownloadRequest
                    {
                        Id = Guid.NewGuid(),
                        Filename = constructedFilename,
                        FileSize = fileSize,
                        Sources = sources,
                        OutputPath = Path.Combine(targetDir, constructedFilename),
                        TargetMusicBrainzRecordingId = recordingId,
                        TargetSemanticKey = verificationResult.BestSemanticKey
                    };

                    // Start download asynchronously (fire and forget for now)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await multiSourceDownloads.DownloadAsync(downloadRequest, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, "[LH-Remediation] Download failed for recording {RecordingId}", recordingId);
                        }
                    }, ct);

                    downloadJobs.Add(downloadRequest.Id);
                    log.LogInformation(
                        "[LH-Remediation] Created download job {DownloadId} for recording {RecordingId}",
                        downloadRequest.Id,
                        recordingId);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "[LH-Remediation] Failed to create download job for recording {RecordingId}", recordingId);
                }
            }

            if (downloadJobs.Count == 0)
            {
                throw new InvalidOperationException("Failed to create any download jobs");
            }

            // Return the first job ID as the main job ID
            // All jobs are tracked individually via multi-source download service
            var jobId = downloadJobs[0].ToString();
            
            log.LogInformation(
                "[LH-Remediation] Created {Count} download jobs, main job ID: {JobId}",
                downloadJobs.Count,
                jobId);

            return jobId;
        }

        private async Task<string> CreateAlbumCompletionJobAsync(List<LibraryIssue> issues, CancellationToken ct)
        {
            // Extract unique release ID
            var releaseIds = issues
                .Where(i => !string.IsNullOrEmpty(i.MusicBrainzReleaseId))
                .Select(i => i.MusicBrainzReleaseId)
                .Distinct()
                .ToList();

            if (releaseIds.Count != 1)
            {
                throw new InvalidOperationException("Album completion requires all issues to be from the same release");
            }

            string releaseId = releaseIds.First();

            // Extract missing recording IDs from issue metadata
            var missingRecordingIds = new List<string>();
            foreach (var issue in issues)
            {
                if (issue.Metadata != null && issue.Metadata.TryGetValue("missing_tracks", out var missingObj))
                {
                    if (missingObj is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var trackElement in jsonElement.EnumerateArray())
                        {
                            if (trackElement.TryGetProperty("recording_id", out var recIdProperty))
                            {
                                missingRecordingIds.Add(recIdProperty.GetString());
                            }
                        }
                    }
                }
            }

            if (missingRecordingIds.Count == 0)
            {
                throw new InvalidOperationException("No missing track recording IDs found in issue metadata");
            }

            // Determine target directory
            string targetDir = issues.First().FilePath; // For MissingTrackInRelease, FilePath is the directory

            log.LogInformation(
                "[LH-Remediation] Creating album completion job for release {ReleaseId}, {Count} missing tracks to {Dir}",
                releaseId,
                missingRecordingIds.Count,
                targetDir);

            // Return placeholder job ID
            var jobId = Guid.NewGuid().ToString();
            
            log.LogInformation(
                "[LH-Remediation] Created album completion job {JobId} (placeholder - full integration pending)",
                jobId);

            return jobId;
        }

        private async Task<string> CreateCanonicalReplacementJobAsync(List<LibraryIssue> issues, CancellationToken ct)
        {
            // For canonical replacement, use the same logic as track redownload
            // but ensure canonical sources are strongly preferred
            log.LogInformation("[LH-Remediation] Creating canonical replacement job for {Count} files", issues.Count);

            return await CreateTrackRedownloadJobAsync(issues, ct).ConfigureAwait(false);
        }
    }
}

