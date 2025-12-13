// <copyright file="SqlitePodMessaging.cs" company="slskdn Team">
//     Copyright (c) slskdn Team. All rights reserved.
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

namespace slskd.PodCore
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// SQLite-backed pod messaging service.
    /// </summary>
    public class SqlitePodMessaging : IPodMessaging
    {
        private readonly PodDbContext dbContext;
        private readonly ILogger<SqlitePodMessaging> logger;

        public SqlitePodMessaging(
            PodDbContext dbContext,
            ILogger<SqlitePodMessaging> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        public async Task<bool> SendAsync(
            PodMessage message,
            CancellationToken ct = default)
        {
            // Extract podId and channelId from message
            // Since the interface signature doesn't include them, we need to parse from MessageId or add them to PodMessage
            // For now, let's assume they're embedded or we modify the call site
            // TEMPORARY FIX: This needs the interface updated or message to carry pod/channel info
            logger.LogWarning("SendAsync called without podId/channelId - interface mismatch needs fix");
            return false;
        }

        public async Task<bool> SendMessageAsync(
            string podId,
            string channelId,
            PodMessage message,
            CancellationToken ct = default)
        {
            // SECURITY: Validate all inputs
            if (!PodValidation.IsValidPodId(podId))
            {
                logger.LogWarning("Invalid pod ID in SendMessageAsync");
                return false;
            }

            if (!PodValidation.IsValidChannelId(channelId))
            {
                logger.LogWarning("Invalid channel ID in SendMessageAsync");
                return false;
            }

            var (isValid, error) = PodValidation.ValidateMessage(message);
            if (!isValid)
            {
                logger.LogWarning("Message validation failed: {Reason}", error);
                return false;
            }

            // SECURITY: Sanitize message body
            message.Body = PodValidation.Sanitize(message.Body, PodValidation.MaxMessageBodyLength);

            // SECURITY: Use transaction
            using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                // Verify pod exists
                var podExists = await dbContext.Pods.AnyAsync(p => p.PodId == podId, ct);
                if (!podExists)
                {
                    logger.LogWarning("Attempted to send message to non-existent pod");
                    return false;
                }

                // Verify sender is a member (AUTHORIZATION CHECK)
                var isMember = await dbContext.Members.AnyAsync(
                    m => m.PodId == podId && m.PeerId == message.SenderPeerId && !m.IsBanned,
                    ct);

                if (!isMember)
                {
                    logger.LogWarning("Non-member attempted to send message");
                    return false;
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
                await transaction.CommitAsync(ct);

                logger.LogDebug("Message saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error saving message");
                await transaction.RollbackAsync(ct);
                return false;
            }
        }

        public async Task<IReadOnlyList<PodMessage>> GetMessagesAsync(
            string podId,
            string channelId,
            long? sinceTimestamp = null,
            CancellationToken ct = default)
        {
            return await GetMessagesInternalAsync(podId, channelId, sinceTimestamp, 100, ct);
        }

        private async Task<IReadOnlyList<PodMessage>> GetMessagesInternalAsync(
            string podId,
            string channelId,
            long? sinceTimestampUnixMs = null,
            int limit = 100,
            CancellationToken ct = default)
        {
            // SECURITY: Validate inputs
            if (!PodValidation.IsValidPodId(podId))
            {
                logger.LogWarning("Invalid pod ID in GetMessagesInternalAsync");
                return Array.Empty<PodMessage>();
            }

            if (!PodValidation.IsValidChannelId(channelId))
            {
                logger.LogWarning("Invalid channel ID in GetMessagesInternalAsync");
                return Array.Empty<PodMessage>();
            }

            // SECURITY: Enforce reasonable limit
            const int MaxLimit = 1000;
            limit = Math.Min(limit, MaxLimit);

            try
            {
                var query = dbContext.Messages
                    .Where(m => m.PodId == podId && m.ChannelId == channelId);

                if (sinceTimestampUnixMs.HasValue)
                {
                    query = query.Where(m => m.TimestampUnixMs > sinceTimestampUnixMs.Value);
                }

                var entities = await query
                    .OrderBy(m => m.TimestampUnixMs)
                    .Take(limit)
                    .ToListAsync(ct);

                return entities.Select(e => new PodMessage
                {
                    TimestampUnixMs = e.TimestampUnixMs,
                    SenderPeerId = e.SenderPeerId,
                    Body = e.Body,
                    Signature = e.Signature,
                }).ToList();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving messages");
                return Array.Empty<PodMessage>();
            }
        }

        public async Task<int> GetMessageCountAsync(
            string podId,
            string channelId,
            CancellationToken ct = default)
        {
            return await dbContext.Messages
                .Where(m => m.PodId == podId && m.ChannelId == channelId)
                .CountAsync(ct);
        }

        public async Task<bool> DeleteMessageAsync(
            string podId,
            string channelId,
            long timestampUnixMs,
            string senderPeerId,
            CancellationToken ct = default)
        {
            var message = await dbContext.Messages
                .FirstOrDefaultAsync(
                    m => m.PodId == podId &&
                         m.ChannelId == channelId &&
                         m.TimestampUnixMs == timestampUnixMs &&
                         m.SenderPeerId == senderPeerId,
                    ct);

            if (message == null)
            {
                logger.LogWarning(
                    "Attempted to delete non-existent message in pod {PodId} channel {ChannelId}",
                    podId,
                    channelId);
                return false;
            }

            dbContext.Messages.Remove(message);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation(
                "Message from {PeerId} deleted from pod {PodId} channel {ChannelId}",
                senderPeerId,
                podId,
                channelId);

            return true;
        }

        public async Task<IReadOnlyList<PodMessage>> BackfillMessagesAsync(
            string podId,
            string channelId,
            IEnumerable<PodMessage> messages,
            CancellationToken ct = default)
        {
            var accepted = new List<PodMessage>();

            foreach (var message in messages)
            {
                // Check for duplicates
                var exists = await dbContext.Messages.AnyAsync(
                    m => m.PodId == podId &&
                         m.ChannelId == channelId &&
                         m.TimestampUnixMs == message.TimestampUnixMs &&
                         m.SenderPeerId == message.SenderPeerId,
                    ct);

                if (exists)
                {
                    logger.LogDebug(
                        "Skipping duplicate message from {PeerId} in pod {PodId} channel {ChannelId}",
                        message.SenderPeerId,
                        podId,
                        channelId);
                    continue;
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
                accepted.Add(message);
            }

            if (accepted.Count > 0)
            {
                await dbContext.SaveChangesAsync(ct);
                logger.LogInformation(
                    "Backfilled {Count} messages to pod {PodId} channel {ChannelId}",
                    accepted.Count,
                    podId,
                    channelId);
            }

            return accepted;
        }
    }
}















