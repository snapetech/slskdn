namespace slskd.HashDb;

public partial class HashDbService
{
    // Virtual Soulfind pseudonym mapping implementation
    
    public async Task UpsertPseudonymAsync(
        string soulseekUsername,
        string peerId,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Pseudonyms (SoulseekUsername, PeerId, UpdatedAt)
            VALUES (@username, @peerId, @updatedAt)
            ON CONFLICT(SoulseekUsername) DO UPDATE SET
                PeerId = @peerId,
                UpdatedAt = @updatedAt";

        command.Parameters.AddWithValue("@username", soulseekUsername);
        command.Parameters.AddWithValue("@peerId", peerId);
        command.Parameters.AddWithValue("@updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<string?> GetPseudonymAsync(
        string soulseekUsername,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT PeerId
            FROM Pseudonyms
            WHERE SoulseekUsername = @username";

        command.Parameters.AddWithValue("@username", soulseekUsername);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }

    public async Task<string?> GetUsernameFromPseudonymAsync(
        string peerId,
        CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT SoulseekUsername
            FROM Pseudonyms
            WHERE PeerId = @peerId";

        command.Parameters.AddWithValue("@peerId", peerId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string;
    }
}
