// <copyright file="Z04292026_TransferRetryBatchMigration.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.Migrations;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Serilog;

/// <summary>
///     Adds retry and batch metadata to transfer history.
/// </summary>
public class Z04292026_TransferRetryBatchMigration : IMigration
{
    public Z04292026_TransferRetryBatchMigration(ConnectionStringDictionary connectionStrings)
    {
        ConnectionString = connectionStrings[Database.Transfers];
    }

    private ILogger Log { get; } = Serilog.Log.ForContext<Z04292026_TransferRetryBatchMigration>();
    private string ConnectionString { get; }

    public bool NeedsToBeApplied()
    {
        var columns = SchemaInspector.GetDatabaseSchema(ConnectionString);
        var transfers = columns["Transfers"];

        return !HasColumn(transfers, "BatchId")
            || !HasColumn(transfers, "Attempts")
            || !HasColumn(transfers, "NextAttemptAt");
    }

    public void Apply()
    {
        if (!NeedsToBeApplied())
        {
            Log.Information("> Migration {Name} is not necessary or has already been applied", nameof(Z04292026_TransferRetryBatchMigration));
            return;
        }

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            void AddColumn(string name, string definition)
            {
                using var command = new SqliteCommand($"ALTER TABLE Transfers ADD COLUMN {name} {definition}", connection, transaction);
                command.ExecuteNonQuery();
            }

            var columns = SchemaInspector.GetDatabaseSchema(ConnectionString)["Transfers"];

            if (!HasColumn(columns, "BatchId"))
            {
                AddColumn("BatchId", "TEXT NULL");
            }

            if (!HasColumn(columns, "Attempts"))
            {
                AddColumn("Attempts", "INTEGER NOT NULL DEFAULT 1");
            }

            if (!HasColumn(columns, "NextAttemptAt"))
            {
                AddColumn("NextAttemptAt", "TEXT NULL");
            }

            using var indexCommand = new SqliteCommand(
                "CREATE INDEX IF NOT EXISTS IDX_Transfers_BatchId ON Transfers (BatchId)",
                connection,
                transaction);
            indexCommand.ExecuteNonQuery();

            transaction.Commit();
            Log.Information("> Added transfer retry and batch metadata");
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static bool HasColumn(IEnumerable<SchemaInspector.ColumnInfo> columns, string name)
        => columns.Any(column => column.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
