namespace slskd.Audio
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICanonicalStatsService
    {
        Task<CanonicalStats> AggregateStatsAsync(string recordingId, string codecProfileKey, CancellationToken ct = default);

        Task<List<AudioVariant>> GetCanonicalVariantCandidatesAsync(string recordingId, CancellationToken ct = default);

        Task RecomputeAllStatsAsync(CancellationToken ct = default);
    }
}
















