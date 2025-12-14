// <copyright file="SqlitePodMessageStorage.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
// </copyright>

namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
///     SQLite-backed pod message storage with full-text search and retention policies.
/// </summary>
public class SqlitePodMessageStorage : IPodMessageStorage
{
    private readonly PodDbContext dbContext;
    private readonly ILogger<SqlitePodMessageStorage> logger;
    private readonly string connectionString;

    private const int DefaultMessageLimit = 100;
    private const int MaxMessageLimit = 1000;
    private const int DefaultSearchLimit = 50;
    private const int MaxSearchLimit = 500;

    public SqlitePodMessageStorage(
        PodDbContext dbContext,
        ILogger<SqlitePodMessageStorage> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        connectionString = dbContext.Database.GetConnectionString();

        // Ensure FTS table and triggers exist
        InitializeFtsTables().GetAwaiter().GetResult();
    }

    public async Task<bool> StoreMessageAsync(string podId, string channelId, PodMessage message, CancellationToken ct = default)
    {
        // Validate inputs
        if (!PodValidation.IsValidPodId(podId))
        {
            logger.LogWarning("Invalid pod ID in StoreMessageAsync: {PodId}", podId);
            return false;
        }

        if (!PodValidation.IsValidChannelId(channelId))
        {
            logger.LogWarning("Invalid channel ID in StoreMessageAsync: {ChannelId}", channelId);
            return false;
        }

        var (isValid, error) = PodValidation.ValidateMessage(message);
        if (!isValid)
        {
            logger.LogWarning("Message validation failed: {Reason}", error);
            return false;
        }

        // Sanitize message body
        message.Body = PodValidation.Sanitize(message.Body, PodValidation.MaxMessageBodyLength);

        try
        {
            // Check for duplicate (same pod, channel, timestamp, sender)
            var exists = await dbContext.Messages.AnyAsync(
                m => m.PodId == podId &&
                     m.ChannelId == channelId &&
                     m.TimestampUnixMs == message.TimestampUnixMs &&
                     m.SenderPeerId == message.SenderPeerId,
                ct);

            if (exists)
            {
                logger.LogDebug("Duplicate message detected, skipping storage");
                return true; // Not an error, just already exists
            }

            var entity = new PodMessageEntity
            {
                PodId = podId,
                ChannelId = channelId,
                TimestampUnixMs = message.TimestampUnixMs,
                SenderPeerId = message.SenderPeerId,
                Body = message.Body,
                Signature = message.Signature,
            };

            dbContext.Messages.Add(entity);
            await dbContext.SaveChangesAsync(ct);

            logger.LogDebug("Message stored successfully in pod {PodId} channel {ChannelId}", podId, channelId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error storing message in pod {PodId} channel {ChannelId}", podId, channelId);
            return false;
        }
    }

    public async Task<IReadOnlyList<PodMessage>> GetMessagesAsync(string podId, string channelId, long? sinceTimestamp = null, int limit = DefaultMessageLimit, CancellationToken ct = default)
    {
        // Validate inputs
        if (!PodValidation.IsValidPodId(podId))
        {
            logger.LogWarning("Invalid pod ID in GetMessagesAsync: {PodId}", podId);
            return Array.Empty<PodMessage>();
        }

        if (!PodValidation.IsValidChannelId(channelId))
        {
            logger.LogWarning("Invalid channel ID in GetMessagesAsync: {ChannelId}", channelId);
            return Array.Empty<PodMessage>();
        }

        // Enforce reasonable limits
        limit = Math.Min(Math.Max(1, limit), MaxMessageLimit);

        try
        {
            var query = dbContext.Messages
                .Where(m => m.PodId == podId && m.ChannelId == channelId);

            if (sinceTimestamp.HasValue)
            {
                query = query.Where(m => m.TimestampUnixMs > sinceTimestamp.Value);
            }

            var entities = await query
                .OrderBy(m => m.TimestampUnixMs)
                .Take(limit)
                .ToListAsync(ct);

            return entities.Select(e => new PodMessage
            {
                MessageId = $"{e.PodId}:{e.ChannelId}:{e.TimestampUnixMs}:{e.SenderPeerId}",
                ChannelId = e.ChannelId,
                TimestampUnixMs = e.TimestampUnixMs,
                SenderPeerId = e.SenderPeerId,
                Body = e.Body,
                Signature = e.Signature,
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving messages from pod {PodId} channel {ChannelId}", podId, channelId);
            return Array.Empty<PodMessage>();
        }
    }

    public async Task<IReadOnlyList<PodMessage>> SearchMessagesAsync(string podId, string query, string channelId = null, int limit = DefaultSearchLimit, CancellationToken ct = default)
    {
        // Validate inputs
        if (!PodValidation.IsValidPodId(podId))
        {
            logger.LogWarning("Invalid pod ID in SearchMessagesAsync: {PodId}", podId);
            return Array.Empty<PodMessage>();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Empty search query provided");
            return Array.Empty<PodMessage>();
        }

        // Enforce reasonable limits
        limit = Math.Min(Math.Max(1, limit), MaxSearchLimit);

        try
        {
            // Build FTS query
            var sql = @"
                SELECT m.PodId, m.ChannelId, m.TimestampUnixMs, m.SenderPeerId, m.Body, m.Signature
                FROM Messages m
                JOIN Messages_fts fts ON m.PodId = fts.PodId
                                      AND m.ChannelId = fts.ChannelId
                                      AND m.TimestampUnixMs = fts.TimestampUnixMs
                                      AND m.SenderPeerId = fts.SenderPeerId
                WHERE fts.Body MATCH @query
                  AND m.PodId = @podId";

            var parameters = new[]
            {
                new SqliteParameter("@query", query),
                new SqliteParameter("@podId", podId),
            };

            if (!string.IsNullOrEmpty(channelId))
            {
                if (!PodValidation.IsValidChannelId(channelId))
                {
                    logger.LogWarning("Invalid channel ID in SearchMessagesAsync: {ChannelId}", channelId);
                    return Array.Empty<PodMessage>();
                }

                sql += " AND m.ChannelId = @channelId";
                parameters = parameters.Append(new SqliteParameter("@channelId", channelId)).ToArray();
            }

            sql += " ORDER BY m.TimestampUnixMs DESC LIMIT @limit";
            parameters = parameters.Append(new SqliteParameter("@limit", limit)).ToArray();

            var messages = new List<PodMessage>();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddRange(parameters);

            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                messages.Add(new PodMessage
                {
                    MessageId = $"{reader.GetString(0)}:{reader.GetString(1)}:{reader.GetInt64(2)}:{reader.GetString(3)}",
                    ChannelId = reader.GetString(1),
                    TimestampUnixMs = reader.GetInt64(2),
                    SenderPeerId = reader.GetString(3),
                    Body = reader.GetString(4),
                    Signature = reader.GetString(5),
                });
            }

            logger.LogDebug("Found {Count} messages matching query '{Query}' in pod {PodId}", messages.Count, query, podId);
            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching messages in pod {PodId} with query '{Query}'", podId, query);
            return Array.Empty<PodMessage>();
        }
    }

    public async Task<long> DeleteMessagesOlderThanAsync(long olderThanTimestamp, CancellationToken ct = default)
    {
        try
        {
            var deletedCount = await dbContext.Messages
                .Where(m => m.TimestampUnixMs < olderThanTimestamp)
                .ExecuteDeleteAsync(ct);

            logger.LogInformation("Deleted {Count} messages older than {Timestamp}", deletedCount, olderThanTimestamp);
            return deletedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting messages older than {Timestamp}", olderThanTimestamp);
            return 0;
        }
    }

    public async Task<long> DeleteMessagesInChannelOlderThanAsync(string podId, string channelId, long olderThanTimestamp, CancellationToken ct = default)
    {
        // Validate inputs
        if (!PodValidation.IsValidPodId(podId))
        {
            logger.LogWarning("Invalid pod ID in DeleteMessagesInChannelOlderThanAsync: {PodId}", podId);
            return 0;
        }

        if (!PodValidation.IsValidChannelId(channelId))
        {
            logger.LogWarning("Invalid channel ID in DeleteMessagesInChannelOlderThanAsync: {ChannelId}", channelId);
            return 0;
        }

        try
        {
            var deletedCount = await dbContext.Messages
                .Where(m => m.PodId == podId && m.ChannelId == channelId && m.TimestampUnixMs < olderThanTimestamp)
                .ExecuteDeleteAsync(ct);

            logger.LogInformation("Deleted {Count} messages in pod {PodId} channel {ChannelId} older than {Timestamp}",
                deletedCount, podId, channelId, olderThanTimestamp);
            return deletedCount;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting messages in pod {PodId} channel {ChannelId} older than {Timestamp}",
                podId, channelId, olderThanTimestamp);
            return 0;
        }
    }

    public async Task<long> GetMessageCountAsync(string podId, string channelId, CancellationToken ct = default)
    {
        // Validate inputs
        if (!PodValidation.IsValidPodId(podId))
        {
            logger.LogWarning("Invalid pod ID in GetMessageCountAsync: {PodId}", podId);
            return 0;
        }

        if (!PodValidation.IsValidChannelId(channelId))
        {
            logger.LogWarning("Invalid channel ID in GetMessageCountAsync: {ChannelId}", channelId);
            return 0;
        }

        try
        {
            return await dbContext.Messages
                .Where(m => m.PodId == podId && m.ChannelId == channelId)
                .LongCountAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting message count for pod {PodId} channel {ChannelId}", podId, channelId);
            return 0;
        }
    }

    public async Task<PodMessageStorageStats> GetStorageStatsAsync(CancellationToken ct = default)
    {
        try
        {
            var totalMessages = await dbContext.Messages.LongCountAsync(ct);

            // Get oldest and newest timestamps
            var oldestQuery = dbContext.Messages.OrderBy(m => m.TimestampUnixMs).FirstOrDefaultAsync(ct);
            var newestQuery = dbContext.Messages.OrderByDescending(m => m.TimestampUnixMs).FirstOrDefaultAsync(ct);

            await Task.WhenAll(oldestQuery, newestQuery);

            var oldestEntity = await oldestQuery;
            var newestEntity = await newestQuery;

            var oldestMessage = oldestEntity != null
                ? DateTimeOffset.FromUnixTimeMilliseconds(oldestEntity.TimestampUnixMs)
                : (DateTimeOffset?)null;

            var newestMessage = newestEntity != null
                ? DateTimeOffset.FromUnixTimeMilliseconds(newestEntity.TimestampUnixMs)
                : (DateTimeOffset?)null;

            // Get messages per pod
            var messagesPerPod = await dbContext.Messages
                .GroupBy(m => m.PodId)
                .Select(g => new { PodId = g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(x => x.PodId, x => x.Count, ct);

            // Get messages per channel (across all pods)
            var messagesPerChannel = await dbContext.Messages
                .GroupBy(m => m.ChannelId)
                .Select(g => new { ChannelId = g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(x => x.ChannelId, x => x.Count, ct);

            // Estimate total size (rough calculation: 200 bytes per message average)
            var estimatedTotalSize = totalMessages * 200;

            return new PodMessageStorageStats(
                TotalMessages: totalMessages,
                TotalSizeBytes: estimatedTotalSize,
                OldestMessage: oldestMessage,
                NewestMessage: newestMessage,
                MessagesPerPod: messagesPerPod,
                MessagesPerChannel: messagesPerChannel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting storage statistics");
            return new PodMessageStorageStats(0, 0, null, null, new(), new());
        }
    }

    public async Task<bool> RebuildSearchIndexAsync(CancellationToken ct = default)
    {
        try
        {
            // Rebuild FTS table from scratch
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(ct);

            await using var command = new SqliteCommand(@"
                INSERT INTO Messages_fts (PodId, ChannelId, TimestampUnixMs, SenderPeerId, Body)
                SELECT PodId, ChannelId, TimestampUnixMs, SenderPeerId, Body
                FROM Messages;", connection);

            await command.ExecuteNonQueryAsync(ct);

            logger.LogInformation("Successfully rebuilt message search index");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rebuilding search index");
            return false;
        }
    }

    public async Task<bool> VacuumAsync(CancellationToken ct = default)
    {
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync("VACUUM;", ct);
            logger.LogInformation("Database vacuum completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during database vacuum");
            return false;
        }
    }

    private async Task InitializeFtsTables()
    {
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            // Create FTS virtual table if it doesn't exist
            var createFtsTableSql = @"
                CREATE VIRTUAL TABLE IF NOT EXISTS Messages_fts USING fts5(
                    PodId, ChannelId, TimestampUnixMs, SenderPeerId, Body,
                    content='',
                    contentless_delete=1
                );";

            await using var createCommand = new SqliteCommand(createFtsTableSql, connection);
            await createCommand.ExecuteNonQueryAsync();

            // Create triggers to keep FTS table in sync
            var createTriggersSql = @"
                CREATE TRIGGER IF NOT EXISTS messages_fts_insert AFTER INSERT ON Messages
                BEGIN
                    INSERT INTO Messages_fts (PodId, ChannelId, TimestampUnixMs, SenderPeerId, Body)
                    VALUES (new.PodId, new.ChannelId, new.TimestampUnixMs, new.SenderPeerId, new.Body);
                END;

                CREATE TRIGGER IF NOT EXISTS messages_fts_delete AFTER DELETE ON Messages
                BEGIN
                    DELETE FROM Messages_fts WHERE PodId = old.PodId
                                                   AND ChannelId = old.ChannelId
                                                   AND TimestampUnixMs = old.TimestampUnixMs
                                                   AND SenderPeerId = old.SenderPeerId;
                END;

                CREATE TRIGGER IF NOT EXISTS messages_fts_update AFTER UPDATE ON Messages
                BEGIN
                    UPDATE Messages_fts SET Body = new.Body
                    WHERE PodId = new.PodId
                          AND ChannelId = new.ChannelId
                          AND TimestampUnixMs = new.TimestampUnixMs
                          AND SenderPeerId = new.SenderPeerId;
                END;";

            await using var triggerCommand = new SqliteCommand(createTriggersSql, connection);
            await triggerCommand.ExecuteNonQueryAsync();

            logger.LogDebug("FTS tables and triggers initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing FTS tables and triggers");
        }
    }
}
