// <copyright file="SqliteSourceRegistry.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.VirtualSoulfind.v2.Sources
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using slskd.VirtualSoulfind.Core;
    using slskd.VirtualSoulfind.v2.Backends;

    /// <summary>
    ///     SQLite-backed implementation of <see cref="ISourceRegistry"/>.
    /// </summary>
    public sealed class SqliteSourceRegistry : ISourceRegistry
    {
        private readonly string _connectionString;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SqliteSourceRegistry"/> class.
        /// </summary>
        /// <param name="connectionString">SQLite connection string.</param>
        public SqliteSourceRegistry(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            InitializeSchema();
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            CancellationToken cancellationToken = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand(
                @"SELECT id, itemId, backend, backendRef, expectedQuality, trustScore, 
                         lastValidatedAt, lastSeenAt, isPreferred 
                  FROM source_candidates 
                  WHERE itemId = @itemId 
                  ORDER BY trustScore DESC, expectedQuality DESC",
                conn);

            cmd.Parameters.AddWithValue("@itemId", itemId.ToString());

            var candidates = new List<SourceCandidate>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var candidate = await ReadCandidateAsync(conn, reader, cancellationToken);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<SourceCandidate>> FindCandidatesForItemAsync(
            ContentItemId itemId,
            ContentBackendType backend,
            CancellationToken cancellationToken = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand(
                @"SELECT id, itemId, backend, backendRef, expectedQuality, trustScore, 
                         lastValidatedAt, lastSeenAt, isPreferred 
                  FROM source_candidates 
                  WHERE itemId = @itemId AND backend = @backend 
                  ORDER BY trustScore DESC, expectedQuality DESC",
                conn);

            cmd.Parameters.AddWithValue("@itemId", itemId.ToString());
            cmd.Parameters.AddWithValue("@backend", (int)backend);

            var candidates = new List<SourceCandidate>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                var candidate = await ReadCandidateAsync(conn, reader, cancellationToken);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }

            return candidates;
        }

        /// <inheritdoc/>
        public async Task UpsertCandidateAsync(
            SourceCandidate candidate,
            CancellationToken cancellationToken = default)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand(
                @"INSERT OR REPLACE INTO source_candidates 
                  (id, itemId, backend, backendRef, expectedQuality, trustScore, 
                   lastValidatedAt, lastSeenAt, isPreferred) 
                  VALUES 
                  (@id, @itemId, @backend, @backendRef, @expectedQuality, @trustScore, 
                   @lastValidatedAt, @lastSeenAt, @isPreferred)",
                conn);

            cmd.Parameters.AddWithValue("@id", candidate.Id);
            cmd.Parameters.AddWithValue("@itemId", candidate.ItemId.ToString());
            cmd.Parameters.AddWithValue("@backend", (int)candidate.Backend);
            cmd.Parameters.AddWithValue("@backendRef", candidate.BackendRef);
            cmd.Parameters.AddWithValue("@expectedQuality", candidate.ExpectedQuality);
            cmd.Parameters.AddWithValue("@trustScore", candidate.TrustScore);
            cmd.Parameters.AddWithValue("@lastValidatedAt",
                candidate.LastValidatedAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@lastSeenAt",
                candidate.LastSeenAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@isPreferred", candidate.IsPreferred ? 1 : 0);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task RemoveCandidateAsync(
            string candidateId,
            CancellationToken cancellationToken = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand(
                "DELETE FROM source_candidates WHERE id = @id",
                conn);

            cmd.Parameters.AddWithValue("@id", candidateId);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> RemoveStaleCandidatesAsync(
            DateTimeOffset olderThan,
            CancellationToken cancellationToken = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand(
                @"DELETE FROM source_candidates 
                  WHERE lastSeenAt IS NOT NULL AND lastSeenAt < @olderThan",
                conn);

            cmd.Parameters.AddWithValue("@olderThan", olderThan.ToUnixTimeSeconds());
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<int> CountCandidatesAsync(
            CancellationToken cancellationToken = default)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            using var cmd = new SqliteCommand(
                "SELECT COUNT(*) FROM source_candidates",
                conn);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result);
        }

        private void InitializeSchema()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            // Create table
            using (var cmd = new SqliteCommand(
                @"CREATE TABLE IF NOT EXISTS source_candidates (
                    id TEXT PRIMARY KEY,
                    itemId TEXT NOT NULL,
                    backend INTEGER NOT NULL,
                    backendRef TEXT NOT NULL,
                    expectedQuality REAL NOT NULL,
                    trustScore REAL NOT NULL,
                    lastValidatedAt INTEGER,
                    lastSeenAt INTEGER,
                    isPreferred INTEGER NOT NULL DEFAULT 0
                )",
                conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Create indexes (separate commands)
            using (var cmd = new SqliteCommand(
                "CREATE INDEX IF NOT EXISTS idx_source_candidates_itemId ON source_candidates(itemId)",
                conn))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqliteCommand(
                "CREATE INDEX IF NOT EXISTS idx_source_candidates_backend ON source_candidates(backend)",
                conn))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqliteCommand(
                "CREATE INDEX IF NOT EXISTS idx_source_candidates_trust ON source_candidates(trustScore DESC, expectedQuality DESC)",
                conn))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqliteCommand(
                "CREATE INDEX IF NOT EXISTS idx_source_candidates_stale ON source_candidates(lastSeenAt)",
                conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private async Task<SourceCandidate?> ReadCandidateAsync(
            SqliteConnection conn,
            SqliteDataReader reader,
            CancellationToken cancellationToken)
        {
            var candidateId = reader.GetString(0);

            if (!ContentItemId.TryParse(reader.GetString(1), out var itemId))
            {
                await DeleteCandidateAsync(conn, candidateId, cancellationToken);
                return null;
            }

            var backendValue = reader.GetInt32(2);
            if (!Enum.IsDefined(typeof(ContentBackendType), backendValue))
            {
                await DeleteCandidateAsync(conn, candidateId, cancellationToken);
                return null;
            }

            var backendRef = reader.GetString(3).Trim();
            if (string.IsNullOrWhiteSpace(backendRef))
            {
                await DeleteCandidateAsync(conn, candidateId, cancellationToken);
                return null;
            }

            var expectedQuality = Math.Clamp(reader.GetFloat(4), 0.0f, 1.0f);
            var trustScore = Math.Clamp(reader.GetFloat(5), 0.0f, 1.0f);

            return new SourceCandidate
            {
                Id = candidateId,
                ItemId = itemId,
                Backend = (ContentBackendType)backendValue,
                BackendRef = backendRef,
                ExpectedQuality = expectedQuality,
                TrustScore = trustScore,
                LastValidatedAt = reader.IsDBNull(6)
                    ? null
                    : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
                LastSeenAt = reader.IsDBNull(7)
                    ? null
                    : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(7)),
                IsPreferred = reader.GetInt32(8) != 0,
            };
        }

        private static async Task DeleteCandidateAsync(
            SqliteConnection conn,
            string candidateId,
            CancellationToken cancellationToken)
        {
            using var cleanup = new SqliteCommand(
                "DELETE FROM source_candidates WHERE id = @id",
                conn);
            cleanup.Parameters.AddWithValue("@id", candidateId);
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
