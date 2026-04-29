// <copyright file="ActivityPubOutboxStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.SocialFederation;

using Microsoft.Data.Sqlite;

public interface IActivityPubOutboxStore
{
    Task StoreAsync(string actorName, ActivityPubActivity activity, string rawJson, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ActivityPubOutboxEntry>> GetActivitiesAsync(string actorName, int limit, CancellationToken cancellationToken = default);
}

public sealed class ActivityPubOutboxEntry
{
    public string ActorName { get; init; } = string.Empty;
    public string ActivityId { get; init; } = string.Empty;
    public string ActivityType { get; init; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public string RawJson { get; init; } = string.Empty;
}

public sealed class ActivityPubOutboxStore : IActivityPubOutboxStore
{
    private readonly string _dbPath;
    private readonly ILogger<ActivityPubOutboxStore> _logger;

    public ActivityPubOutboxStore(ILogger<ActivityPubOutboxStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbPath = Path.Combine(Program.AppDirectory, "social-federation-outbox.db");
        InitializeDatabase();
    }

    public async Task StoreAsync(string actorName, ActivityPubActivity activity, string rawJson, CancellationToken cancellationToken = default)
    {
        var activityId = string.IsNullOrWhiteSpace(activity.Id) ? Ulid.NewUlid().ToString() : activity.Id;
        var publishedAt = (activity.Published ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO OutboundActivities (
                ActorName,
                ActivityId,
                ActivityType,
                PublishedAt,
                RawJson
            ) VALUES (
                @actorName,
                @activityId,
                @activityType,
                @publishedAt,
                @rawJson
            )
            ON CONFLICT(ActorName, ActivityId) DO UPDATE SET
                ActivityType = excluded.ActivityType,
                PublishedAt = excluded.PublishedAt,
                RawJson = excluded.RawJson";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@activityId", activityId);
        command.Parameters.AddWithValue("@activityType", activity.Type ?? string.Empty);
        command.Parameters.AddWithValue("@publishedAt", publishedAt);
        command.Parameters.AddWithValue("@rawJson", rawJson);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityPubOutboxEntry>> GetActivitiesAsync(string actorName, int limit, CancellationToken cancellationToken = default)
    {
        var entries = new List<ActivityPubOutboxEntry>();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ActorName, ActivityId, ActivityType, PublishedAt, RawJson
            FROM OutboundActivities
            WHERE ActorName = @actorName
            ORDER BY PublishedAt DESC, rowid DESC
            LIMIT @limit";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            entries.Add(new ActivityPubOutboxEntry
            {
                ActorName = reader.GetString(0),
                ActivityId = reader.GetString(1),
                ActivityType = reader.GetString(2),
                PublishedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                RawJson = reader.GetString(4),
            });
        }

        return entries;
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS OutboundActivities (
                ActorName TEXT NOT NULL,
                ActivityId TEXT NOT NULL,
                ActivityType TEXT NOT NULL,
                PublishedAt INTEGER NOT NULL,
                RawJson TEXT NOT NULL,
                PRIMARY KEY (ActorName, ActivityId)
            );

            CREATE INDEX IF NOT EXISTS IX_OutboundActivities_Actor_PublishedAt
            ON OutboundActivities (ActorName, PublishedAt DESC);";
        command.ExecuteNonQuery();

        _logger.LogInformation("[ActivityPubOutbox] Initialized outbox store at {Path}", _dbPath);
    }
}
