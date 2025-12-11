namespace slskd.VirtualSoulfind.Capture;

using System.Threading.Tasks;

public interface INormalizationPipeline
{
    Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct = default);
    Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct = default);
}

/// <summary>
/// Stub normalization pipeline to satisfy build; real implementation pending.
/// </summary>
public class NormalizationPipeline : INormalizationPipeline
{
    public Task ProcessSearchObservationAsync(SearchObservation obs, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task ProcessTransferObservationAsync(TransferObservation obs, CancellationToken ct = default) =>
        Task.CompletedTask;
}
