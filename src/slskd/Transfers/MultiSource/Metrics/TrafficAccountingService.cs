namespace slskd.Transfers.MultiSource.Metrics
{
    using System.Threading;
    using System.Threading.Tasks;
    using slskd.HashDb;

    public interface ITrafficAccountingService
    {
        Task AddOverlayDownloadAsync(long bytes, CancellationToken ct = default);
        Task AddOverlayUploadAsync(long bytes, CancellationToken ct = default);
        Task AddSoulseekDownloadAsync(long bytes, CancellationToken ct = default);
        Task AddSoulseekUploadAsync(long bytes, CancellationToken ct = default);
        Task<HashDb.Models.TrafficTotals> GetTotalsAsync(CancellationToken ct = default);
    }

    /// <summary>
    ///     Centralized traffic accounting for overlay/Soulseek.
    /// </summary>
    public class TrafficAccountingService : ITrafficAccountingService
    {
        private readonly IHashDbService hashDb;

        public TrafficAccountingService(IHashDbService hashDb)
        {
            this.hashDb = hashDb;
        }

        public Task AddOverlayDownloadAsync(long bytes, CancellationToken ct = default) =>
            hashDb.AddTrafficAsync(overlayUpload: 0, overlayDownload: bytes, soulseekUpload: 0, soulseekDownload: 0, ct);

        public Task AddOverlayUploadAsync(long bytes, CancellationToken ct = default) =>
            hashDb.AddTrafficAsync(overlayUpload: bytes, overlayDownload: 0, soulseekUpload: 0, soulseekDownload: 0, ct);

        public Task AddSoulseekDownloadAsync(long bytes, CancellationToken ct = default) =>
            hashDb.AddTrafficAsync(overlayUpload: 0, overlayDownload: 0, soulseekUpload: 0, soulseekDownload: bytes, ct);

        public Task AddSoulseekUploadAsync(long bytes, CancellationToken ct = default) =>
            hashDb.AddTrafficAsync(overlayUpload: 0, overlayDownload: 0, soulseekUpload: bytes, soulseekDownload: 0, ct);

        public Task<HashDb.Models.TrafficTotals> GetTotalsAsync(CancellationToken ct = default) =>
            hashDb.GetTrafficTotalsAsync(ct);
    }
}
