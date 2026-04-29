// <copyright file="TransfersDbContextTests.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Tests.Unit.Transfers;

using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using slskd.Transfers;
using Soulseek;
using Xunit;

public sealed class TransfersDbContextTests
{
    [Fact]
    public void QueryingLegacyRows_AllowsNullTransferStrings()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TransfersDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new TransfersDbContext(options))
        {
            context.Database.EnsureCreated();
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                INSERT INTO Transfers
                (Id, Username, Direction, Filename, Size, StartOffset, State, StateDescription, RequestedAt, EnqueuedAt, StartedAt, EndedAt, BytesTransferred, AverageSpeed, PlaceInQueue, Exception, Removed, Attempts)
                VALUES
                ($id, $username, $direction, $filename, $size, $startOffset, $state, NULL, $requestedAt, NULL, NULL, NULL, $bytesTransferred, $averageSpeed, NULL, NULL, 0, 1)
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid());
            command.Parameters.AddWithValue("$username", "legacy-user");
            command.Parameters.AddWithValue("$direction", TransferDirection.Upload.ToString());
            command.Parameters.AddWithValue("$filename", "legacy.flac");
            command.Parameters.AddWithValue("$size", 1024L);
            command.Parameters.AddWithValue("$startOffset", 0L);
            command.Parameters.AddWithValue("$state", TransferStates.None.ToString());
            command.Parameters.AddWithValue("$requestedAt", DateTime.UtcNow);
            command.Parameters.AddWithValue("$bytesTransferred", 0L);
            command.Parameters.AddWithValue("$averageSpeed", 0d);
            command.ExecuteNonQuery();
        }

        using var verificationContext = new TransfersDbContext(options);
        var transfer = verificationContext.Transfers.Single();

        Assert.Equal("legacy-user", transfer.Username);
        Assert.Null(transfer.StateDescription);
        Assert.Null(transfer.Exception);
    }
}
