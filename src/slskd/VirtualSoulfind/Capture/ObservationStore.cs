// <copyright file="ObservationStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.Capture;

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OptionsModel = slskd.Options;

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

/// <summary>
/// SQLite-backed observation store for optional raw capture persistence.
/// </summary>
public class SqliteObservationStore : IObservationStore
{
    private readonly string dbPath;
    private readonly ILogger<SqliteObservationStore> logger;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;

    public SqliteObservationStore(
        ILogger<SqliteObservationStore> logger,
        IOptionsMonitor<OptionsModel> optionsMonitor)
    {
        this.logger = logger;
        this.optionsMonitor = optionsMonitor;
        dbPath = Path.Combine(Program.AppDirectory, "virtual-soulfind-observations.db");
        InitializeDatabase();
    }

    public async Task StoreSearchObservationAsync(SearchObservation obs, CancellationToken ct)
    {
        await StoreObservationAsync(
            observationId: obs.ObservationId,
            observationType: "Search",
            timestamp: obs.Timestamp,
            jsonData: JsonSerializer.Serialize(obs),
            ct);

        await PurgeUsingConfiguredRetentionAsync(ct);
    }

    public async Task StoreTransferObservationAsync(TransferObservation obs, CancellationToken ct)
    {
        await StoreObservationAsync(
            observationId: obs.TransferId,
            observationType: "Transfer",
            timestamp: obs.CompletedAt,
            jsonData: JsonSerializer.Serialize(obs),
            ct);

        await PurgeUsingConfiguredRetentionAsync(ct);
    }

    public async Task<List<ObservationDatabase.ObservationEntry>> GetUnprocessedAsync(int limit, CancellationToken ct)
    {
        var entries = new List<ObservationDatabase.ObservationEntry>();

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ObservationId, ObservationType, Timestamp, JsonData, Processed, ProcessingError
            FROM Observations
            WHERE Processed = 0
            ORDER BY Timestamp ASC
            LIMIT @limit";
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            entries.Add(new ObservationDatabase.ObservationEntry
            {
                ObservationId = reader.GetString(0),
                ObservationType = reader.GetString(1),
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)),
                JsonData = reader.GetString(3),
                Processed = reader.GetBoolean(4),
                ProcessingError = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return entries;
    }

    public async Task MarkProcessedAsync(string observationId, bool success, string? error, CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Observations
            SET Processed = 1,
                ProcessingError = @error
            WHERE ObservationId = @observationId";
        command.Parameters.AddWithValue("@error", (object?)error ?? DBNull.Value);
        command.Parameters.AddWithValue("@observationId", observationId);

        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task PurgeOldObservationsAsync(TimeSpan maxAge, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge).ToUnixTimeSeconds();

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM Observations
            WHERE Timestamp < @cutoff";
        command.Parameters.AddWithValue("@cutoff", cutoff);

        var deleted = await command.ExecuteNonQueryAsync(ct);
        if (deleted > 0)
        {
            logger.LogDebug("[VSF-STORE] Purged {Count} raw observations older than {MaxAge}", deleted, maxAge);
        }
    }

    private async Task StoreObservationAsync(
        string observationId,
        string observationType,
        DateTimeOffset timestamp,
        string jsonData,
        CancellationToken ct)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(ct);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Observations (ObservationId, ObservationType, Timestamp, JsonData, Processed, ProcessingError)
            VALUES (@observationId, @observationType, @timestamp, @jsonData, 0, NULL)
            ON CONFLICT(ObservationId) DO UPDATE SET
                ObservationType = excluded.ObservationType,
                Timestamp = excluded.Timestamp,
                JsonData = excluded.JsonData";
        command.Parameters.AddWithValue("@observationId", observationId);
        command.Parameters.AddWithValue("@observationType", observationType);
        command.Parameters.AddWithValue("@timestamp", timestamp.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@jsonData", jsonData);

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task PurgeUsingConfiguredRetentionAsync(CancellationToken ct)
    {
        var retention = PrivacyControls.GetRetentionPolicy(optionsMonitor.CurrentValue);
        await PurgeOldObservationsAsync(retention.RawObservationRetention, ct);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Observations (
                ObservationId TEXT PRIMARY KEY,
                ObservationType TEXT NOT NULL,
                Timestamp INTEGER NOT NULL,
                JsonData TEXT NOT NULL,
                Processed INTEGER NOT NULL DEFAULT 0,
                ProcessingError TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Observations_Processed_Timestamp
            ON Observations (Processed, Timestamp);

            CREATE INDEX IF NOT EXISTS IX_Observations_Timestamp
            ON Observations (Timestamp);";
        command.ExecuteNonQuery();

        logger.LogInformation("[VSF-STORE] Raw observation persistence initialized at {Path}", dbPath);
    }
}
