// <copyright file="ActivityPubRelationshipStore.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.SocialFederation;

using Microsoft.Data.Sqlite;

public interface IActivityPubRelationshipStore
{
    Task UpsertFollowerAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default);
    Task RemoveFollowerAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetFollowersAsync(string actorName, int limit, CancellationToken cancellationToken = default);
    Task UpsertFollowingAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default);
    Task RemoveFollowingAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetFollowingAsync(string actorName, int limit, CancellationToken cancellationToken = default);
}

public sealed class ActivityPubRelationshipStore : IActivityPubRelationshipStore
{
    private readonly string _dbPath;
    private readonly ILogger<ActivityPubRelationshipStore> _logger;

    public ActivityPubRelationshipStore(ILogger<ActivityPubRelationshipStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbPath = Path.Combine(Program.AppDirectory, "social-federation-relationships.db");
        InitializeDatabase();
    }

    public async Task UpsertFollowerAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Followers (ActorName, RemoteActorId, FollowedAt)
            VALUES (@actorName, @remoteActorId, @followedAt)
            ON CONFLICT(ActorName, RemoteActorId) DO UPDATE SET
                FollowedAt = excluded.FollowedAt";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@remoteActorId", remoteActorId);
        command.Parameters.AddWithValue("@followedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFollowerAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM Followers
            WHERE ActorName = @actorName
              AND RemoteActorId = @remoteActorId";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@remoteActorId", remoteActorId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetFollowersAsync(string actorName, int limit, CancellationToken cancellationToken = default)
    {
        var followers = new List<string>();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT RemoteActorId
            FROM Followers
            WHERE ActorName = @actorName
            ORDER BY FollowedAt DESC
            LIMIT @limit";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            followers.Add(reader.GetString(0));
        }

        return followers;
    }

    public async Task UpsertFollowingAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Following (ActorName, RemoteActorId, FollowedAt)
            VALUES (@actorName, @remoteActorId, @followedAt)
            ON CONFLICT(ActorName, RemoteActorId) DO UPDATE SET
                FollowedAt = excluded.FollowedAt";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@remoteActorId", remoteActorId);
        command.Parameters.AddWithValue("@followedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFollowingAsync(string actorName, string remoteActorId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM Following
            WHERE ActorName = @actorName
              AND RemoteActorId = @remoteActorId";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@remoteActorId", remoteActorId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetFollowingAsync(string actorName, int limit, CancellationToken cancellationToken = default)
    {
        var following = new List<string>();

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT RemoteActorId
            FROM Following
            WHERE ActorName = @actorName
            ORDER BY FollowedAt DESC
            LIMIT @limit";
        command.Parameters.AddWithValue("@actorName", actorName);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            following.Add(reader.GetString(0));
        }

        return following;
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Followers (
                ActorName TEXT NOT NULL,
                RemoteActorId TEXT NOT NULL,
                FollowedAt INTEGER NOT NULL,
                PRIMARY KEY (ActorName, RemoteActorId)
            );

            CREATE TABLE IF NOT EXISTS Following (
                ActorName TEXT NOT NULL,
                RemoteActorId TEXT NOT NULL,
                FollowedAt INTEGER NOT NULL,
                PRIMARY KEY (ActorName, RemoteActorId)
            );

            CREATE INDEX IF NOT EXISTS IX_Followers_Actor_FollowedAt
            ON Followers (ActorName, FollowedAt DESC);

            CREATE INDEX IF NOT EXISTS IX_Following_Actor_FollowedAt
            ON Following (ActorName, FollowedAt DESC);";
        command.ExecuteNonQuery();

        _logger.LogInformation("[ActivityPubRelationship] Initialized relationship store at {Path}", _dbPath);
    }
}
