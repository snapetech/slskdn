// <copyright file="SqliteSourceRegistry.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
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
                candidates.Add(ReadCandidate(reader));
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
                candidates.Add(ReadCandidate(reader));
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

        private SourceCandidate ReadCandidate(SqliteDataReader reader)
        {
            return new SourceCandidate
            {
                Id = reader.GetString(0),
                ItemId = ContentItemId.Parse(reader.GetString(1)),
                Backend = (ContentBackendType)reader.GetInt32(2),
                BackendRef = reader.GetString(3),
                ExpectedQuality = reader.GetFloat(4),
                TrustScore = reader.GetFloat(5),
                LastValidatedAt = reader.IsDBNull(6) 
                    ? null 
                    : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
                LastSeenAt = reader.IsDBNull(7) 
                    ? null 
                    : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(7)),
                IsPreferred = reader.GetInt32(8) != 0,
            };
        }
    }
}
