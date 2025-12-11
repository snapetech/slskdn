namespace slskd.VirtualSoulfind.Capture;

/// <summary>
/// Optional database schema for persisting raw observations (debugging/replay).
/// </summary>
public class ObservationDatabase
{
    /// <summary>
    /// Observation database entry.
    /// </summary>
    public class ObservationEntry
    {
        public string ObservationId { get; set; } = string.Empty;
        public string ObservationType { get; set; } = string.Empty;  // "Search" or "Transfer"
        public DateTimeOffset Timestamp { get; set; }
        public string JsonData { get; set; } = string.Empty;  // Serialized observation
        public bool Processed { get; set; }
        public string? ProcessingError { get; set; }
    }
}

/// <summary>
/// Interface for observation persistence (optional, for debugging).
/// </summary>
public interface IObservationStore
{
    Task StoreSearchObservationAsync(SearchObservation obs, CancellationToken ct = default);
    Task StoreTransferObservationAsync(TransferObservation obs, CancellationToken ct = default);
    Task<List<ObservationDatabase.ObservationEntry>> GetUnprocessedAsync(int limit, CancellationToken ct = default);
    Task MarkProcessedAsync(string observationId, bool success, string? error, CancellationToken ct = default);
    Task PurgeOldObservationsAsync(TimeSpan maxAge, CancellationToken ct = default);
}

/// <summary>
/// In-memory observation store (no persistence, for production use).
/// </summary>
public class InMemoryObservationStore : IObservationStore
{
    private readonly ILogger<InMemoryObservationStore> logger;

    public InMemoryObservationStore(ILogger<InMemoryObservationStore> logger)
    {
        this.logger = logger;
    }

    public Task StoreSearchObservationAsync(SearchObservation obs, CancellationToken ct)
    {
        // No-op: observations not persisted
        logger.LogTrace("[VSF-STORE] Skipping persistence for search observation {ObsId}",
            obs.ObservationId);
        return Task.CompletedTask;
    }

    public Task StoreTransferObservationAsync(TransferObservation obs, CancellationToken ct)
    {
        // No-op: observations not persisted
        logger.LogTrace("[VSF-STORE] Skipping persistence for transfer observation {TransferId}",
            obs.TransferId);
        return Task.CompletedTask;
    }

    public Task<List<ObservationDatabase.ObservationEntry>> GetUnprocessedAsync(int limit, CancellationToken ct)
    {
        return Task.FromResult(new List<ObservationDatabase.ObservationEntry>());
    }

    public Task MarkProcessedAsync(string observationId, bool success, string? error, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public Task PurgeOldObservationsAsync(TimeSpan maxAge, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

