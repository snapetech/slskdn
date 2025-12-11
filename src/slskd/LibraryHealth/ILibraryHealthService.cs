namespace slskd.LibraryHealth
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILibraryHealthService
    {
        Task<string> StartScanAsync(LibraryHealthScanRequest request, CancellationToken ct = default);

        Task<LibraryHealthScan> GetScanStatusAsync(string scanId, CancellationToken ct = default);

        Task<List<LibraryIssue>> GetIssuesAsync(LibraryHealthIssueFilter filter, CancellationToken ct = default);

        Task UpdateIssueStatusAsync(string issueId, LibraryIssueStatus newStatus, CancellationToken ct = default);

        Task<string> CreateRemediationJobAsync(List<string> issueIds, CancellationToken ct = default);

        Task<LibraryHealthSummary> GetSummaryAsync(string libraryPath, CancellationToken ct = default);
    }
}


