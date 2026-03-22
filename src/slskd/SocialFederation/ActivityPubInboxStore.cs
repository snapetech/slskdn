// <copyright file="ActivityPubInboxStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation;

using System.Text.Json;
using Microsoft.Data.Sqlite;

public interface IActivityPubInboxStore
{
    Task StoreAsync(string actorName, ActivityPubActivity activity, string rawJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityPubInboxEntry>> GetActivitiesAsync(string actorName, int limit, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(string actorName, string activityId, bool processed, string? processingError, CancellationToken cancellationToken = default);
}

public sealed class ActivityPubInboxEntry
{
    public string ActorName { get; init; } = string.Empty;
    public string ActivityId { get; init; } = string.Empty;
    public string ActivityType { get; init; } = string.Empty;
    public string RemoteActor { get; init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; init; }
    public string RawJson { get; init; } = string.Empty;
    public bool Processed { get; init; }
    public string? ProcessingError { get; init; }
}

public sealed class ActivityPubInboxStore : IActivityPubInboxStore
{
    private readonly string _dbPath;
    private readonly ILogger<ActivityPubInboxStore> _logger;

    public ActivityPubInboxStore(ILogger<ActivityPubInboxStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbPath = Path.Combine(Program.AppDirectory, "social-federation-inbox.db");
        InitializeDatabase();
    }

    public async Task StoreAsync(string actorName, ActivityPubActivity activity, string rawJson, CancellationToken cancellationToken = default)
    {
        var activityId = string.IsNullOrWhiteSpace(activity.Id) ? Ulid.NewUlid().ToString() : activity.Id;
        var remoteActor = activity.Actor?.ToString() ?? string.Empty;
        var publishedAt = activity.Published?.ToUnixTimeSeconds();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO InboundActivities (
                ActorName,
                ActivityId,
                ActivityType,
                RemoteActor,
                PublishedAt,
                ReceivedAt,
                RawJson,
                Processed,
                ProcessingError
            ) VALUES (
                @actorName,
                @activityId,
                @activityType,
                @remoteActor,
                @publishedAt,
                @receivedAt,
                @rawJson,
                0,
                NULL
            )
            ON CONFLICT(ActorName, ActivityId) DO UPDATE SET
                ActivityType = excluded.ActivityType,
                RemoteActor = excluded.RemoteActor,
                PublishedAt = excluded.PublishedAt,
                ReceivedAt = excluded.ReceivedAt,
                RawJson = excluded.RawJson";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@activityId", activityId);
        command.Parameters.AddWithValue("@activityType", activity.Type ?? string.Empty);
        command.Parameters.AddWithValue("@remoteActor", remoteActor);
        command.Parameters.AddWithValue("@publishedAt", (object?)publishedAt ?? DBNull.Value);
        command.Parameters.AddWithValue("@receivedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("@rawJson", rawJson);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityPubInboxEntry>> GetActivitiesAsync(string actorName, int limit, CancellationToken cancellationToken = default)
    {
        var entries = new List<ActivityPubInboxEntry>();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ActorName, ActivityId, ActivityType, RemoteActor, ReceivedAt, RawJson, Processed, ProcessingError
            FROM InboundActivities
            WHERE ActorName = @actorName
            ORDER BY ReceivedAt DESC, rowid DESC
            LIMIT @limit";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new ActivityPubInboxEntry
            {
                ActorName = reader.GetString(0),
                ActivityId = reader.GetString(1),
                ActivityType = reader.GetString(2),
                RemoteActor = reader.GetString(3),
                ReceivedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4)),
                RawJson = reader.GetString(5),
                Processed = reader.GetBoolean(6),
                ProcessingError = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }

        return entries;
    }

    public async Task MarkProcessedAsync(string actorName, string activityId, bool processed, string? processingError, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE InboundActivities
            SET Processed = @processed,
                ProcessingError = @processingError
            WHERE ActorName = @actorName
              AND ActivityId = @activityId";
        command.Parameters.AddWithValue("@processed", processed ? 1 : 0);
        command.Parameters.AddWithValue("@processingError", (object?)processingError ?? DBNull.Value);
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@activityId", activityId);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS InboundActivities (
                ActorName TEXT NOT NULL,
                ActivityId TEXT NOT NULL,
                ActivityType TEXT NOT NULL,
                RemoteActor TEXT NOT NULL,
                PublishedAt INTEGER NULL,
                ReceivedAt INTEGER NOT NULL,
                RawJson TEXT NOT NULL,
                Processed INTEGER NOT NULL DEFAULT 0,
                ProcessingError TEXT NULL,
                PRIMARY KEY (ActorName, ActivityId)
            );

            CREATE INDEX IF NOT EXISTS IX_InboundActivities_Actor_ReceivedAt
            ON InboundActivities (ActorName, ReceivedAt DESC);";
        command.ExecuteNonQuery();

        _logger.LogInformation("[ActivityPubInbox] Initialized inbox store at {Path}", _dbPath);
    }
}
