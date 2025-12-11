namespace slskd.VirtualSoulfind.Capture;

using Soulseek;

public interface ITrafficObserver
{
    Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct = default);
    Task OnTransferCompleteAsync(Transfers.Transfer transfer, CancellationToken ct = default);
}

/// <summary>
/// Stub traffic observer; full capture disabled for build.
/// </summary>
public class TrafficObserver : ITrafficObserver
{
    public Task OnSearchResultsAsync(string query, SearchResponse response, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task OnTransferCompleteAsync(Transfers.Transfer transfer, CancellationToken ct = default) =>
        Task.CompletedTask;
}
