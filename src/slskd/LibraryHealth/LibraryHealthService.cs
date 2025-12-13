namespace slskd.LibraryHealth
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using slskd.HashDb;
    using slskd.LibraryHealth.Remediation;

    /// <summary>
    /// Library Health scanner (simplified placeholder implementation).
    /// </summary>
    public class LibraryHealthService : ILibraryHealthService
    {
        private readonly IHashDbService hashDb;
        private readonly ILibraryHealthRemediationService remediationService;
        private readonly ILogger<LibraryHealthService> log;
        private readonly ConcurrentDictionary<string, LibraryHealthScan> activeScans = new();

        public LibraryHealthService(
            IHashDbService hashDb,
            ILibraryHealthRemediationService remediationService,
            ILogger<LibraryHealthService> log)
        {
            this.hashDb = hashDb;
            this.remediationService = remediationService;
            this.log = log;
        }

        public async Task<string> StartScanAsync(LibraryHealthScanRequest request, CancellationToken ct = default)
        {
            var scanId = Guid.NewGuid().ToString();
            var scan = new LibraryHealthScan
            {
                ScanId = scanId,
                LibraryPath = request.LibraryPath,
                StartedAt = DateTimeOffset.UtcNow,
                Status = ScanStatus.Running,
                FilesScanned = 0,
                IssuesDetected = 0,
            };

            activeScans[scanId] = scan;
            await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);

            _ = Task.Run(() => PerformScanAsync(scanId, request, ct), ct);
            return scanId;
        }

        public async Task<LibraryHealthScan> GetScanStatusAsync(string scanId, CancellationToken ct = default)
        {
            if (activeScans.TryGetValue(scanId, out var active))
            {
                return active;
            }

            return await hashDb.GetLibraryHealthScanAsync(scanId, ct).ConfigureAwait(false);
        }

        public Task<List<LibraryIssue>> GetIssuesAsync(LibraryHealthIssueFilter filter, CancellationToken ct = default)
        {
            return hashDb.GetLibraryIssuesAsync(filter, ct);
        }

        public Task UpdateIssueStatusAsync(string issueId, LibraryIssueStatus newStatus, CancellationToken ct = default)
        {
            return hashDb.UpdateLibraryIssueStatusAsync(issueId, newStatus, ct);
        }

        public async Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default)
        {
            log.LogInformation("[LH] Creating remediation job for {Count} issues", issueIds?.Count ?? 0);
            return await remediationService.CreateRemediationJobAsync(issueIds, ct).ConfigureAwait(false);
        }

        public async Task<LibraryHealthSummary> GetSummaryAsync(string libraryPath, CancellationToken ct = default)
        {
            var issues = await hashDb.GetLibraryIssuesAsync(new LibraryHealthIssueFilter { LibraryPath = libraryPath }, ct).ConfigureAwait(false);
            return new LibraryHealthSummary
            {
                LibraryPath = libraryPath,
                TotalIssues = issues.Count,
                IssuesOpen = issues.Count(i => i.Status != LibraryIssueStatus.Resolved && i.Status != LibraryIssueStatus.Ignored),
                IssuesResolved = issues.Count(i => i.Status == LibraryIssueStatus.Resolved),
            };
        }

        private async Task PerformScanAsync(string scanId, LibraryHealthScanRequest request, CancellationToken ct)
        {
            if (!activeScans.TryGetValue(scanId, out var scan))
            {
                return;
            }

            try
            {
                if (!Directory.Exists(request.LibraryPath))
                {
                    throw new DirectoryNotFoundException($"Library path not found: {request.LibraryPath}");
                }

                var files = Directory.EnumerateFiles(
                        request.LibraryPath,
                        "*.*",
                        request.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                    .Where(f => request.FileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                scan.FilesScanned = files.Count;

                // Simplified placeholder: just record scan completion without deep analysis
                scan.Status = ScanStatus.Completed;
                scan.CompletedAt = DateTimeOffset.UtcNow;
                await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);
                activeScans.TryRemove(scanId, out _);

                log.LogInformation("[LH] Completed library health scan {ScanId} for {Path} (files={Files})", scanId, request.LibraryPath, files.Count);
            }
            catch (Exception ex)
            {
                scan.Status = ScanStatus.Failed;
                scan.ErrorMessage = ex.Message;
                scan.CompletedAt = DateTimeOffset.UtcNow;
                await hashDb.UpsertLibraryHealthScanAsync(scan, ct).ConfigureAwait(false);
                activeScans.TryRemove(scanId, out _);
                log.LogWarning(ex, "[LH] Scan failed for {Path}", request.LibraryPath);
            }
        }
    }
}

















