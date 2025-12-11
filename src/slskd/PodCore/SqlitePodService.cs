// <copyright file="SqlitePodService.cs" company="slskdn Team">
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
    /// SQLite-backed pod service with persistence.
    /// </summary>
    public class SqlitePodService : IPodService
    {
        private readonly PodDbContext dbContext;
        private readonly IPodPublisher podPublisher;
        private readonly IPodMembershipSigner membershipSigner;
        private readonly ILogger<SqlitePodService> logger;

        public SqlitePodService(
            PodDbContext dbContext,
            IPodPublisher podPublisher,
            IPodMembershipSigner membershipSigner,
            ILogger<SqlitePodService> logger)
        {
            this.dbContext = dbContext;
            this.podPublisher = podPublisher;
            this.membershipSigner = membershipSigner;
            this.logger = logger;
        }

        public async Task<Pod> CreateAsync(Pod pod, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(pod.PodId))
            {
                pod.PodId = $"pod:{Guid.NewGuid():N}";
            }

            var entity = new PodEntity
            {
                PodId = pod.PodId,
                Name = pod.Name,
                Visibility = pod.Visibility,
                FocusContentId = pod.FocusContentId,
                Tags = System.Text.Json.JsonSerializer.Serialize(pod.Tags),
                Channels = System.Text.Json.JsonSerializer.Serialize(pod.Channels),
                ExternalBindings = System.Text.Json.JsonSerializer.Serialize(pod.ExternalBindings),
            };

            dbContext.Pods.Add(entity);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("Pod {PodId} created successfully", pod.PodId);

            // Publish to DHT if pod is listed
            if (podPublisher != null && pod.Visibility == PodVisibility.Listed)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await podPublisher.PublishPodAsync(pod, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to publish pod {PodId} to DHT", pod.PodId);
                    }
                }, ct);
            }

            return pod;
        }

        public async Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default)
        {
            var entities = await dbContext.Pods.ToListAsync(ct);
            return entities.Select(EntityToPod).ToList();
        }

        public async Task<Pod?> GetPodAsync(string podId, CancellationToken ct = default)
        {
            var entity = await dbContext.Pods.FindAsync(new object[] { podId }, ct);
            return entity == null ? null : EntityToPod(entity);
        }

        public async Task<IReadOnlyList<PodMember>> GetMembersAsync(string podId, CancellationToken ct = default)
        {
            var entities = await dbContext.Members
                .Where(m => m.PodId == podId && !m.IsBanned)
                .ToListAsync(ct);

            return entities.Select(e => new PodMember
            {
                PeerId = e.PeerId,
                Role = e.Role,
                PublicKey = e.PublicKey,
                IsBanned = e.IsBanned,
            }).ToList();
        }

        public async Task<IReadOnlyList<SignedMembershipRecord>> GetMembershipHistoryAsync(string podId, CancellationToken ct = default)
        {
            var entities = await dbContext.MembershipRecords
                .Where(r => r.PodId == podId)
                .OrderBy(r => r.TimestampUnixMs)
                .ToListAsync(ct);

            return entities.Select(e => new SignedMembershipRecord
            {
                PodId = e.PodId,
                PeerId = e.PeerId,
                TimestampUnixMs = e.TimestampUnixMs,
                Action = e.Action,
                Signature = e.Signature,
            }).ToList();
        }

        public async Task<bool> JoinAsync(string podId, PodMember member, CancellationToken ct = default)
        {
            var pod = await dbContext.Pods.FindAsync(new object[] { podId }, ct);
            if (pod == null)
            {
                logger.LogWarning("Attempted to join non-existent pod {PodId}", podId);
                return false;
            }

            var existingMember = await dbContext.Members
                .FirstOrDefaultAsync(m => m.PodId == podId && m.PeerId == member.PeerId, ct);

            if (existingMember != null)
            {
                if (existingMember.IsBanned)
                {
                    logger.LogWarning("Banned user {PeerId} attempted to join pod {PodId}", member.PeerId, podId);
                    return false;
                }

                logger.LogInformation("User {PeerId} already member of pod {PodId}", member.PeerId, podId);
                return true;
            }

            var memberEntity = new PodMemberEntity
            {
                PodId = podId,
                PeerId = member.PeerId,
                Role = member.Role ?? "member",
                PublicKey = member.PublicKey,
                IsBanned = false,
            };

            dbContext.Members.Add(memberEntity);

            // Add membership record
            var membershipRecord = new SignedMembershipRecordEntity
            {
                PodId = podId,
                PeerId = member.PeerId,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Action = "join",
                Signature = string.Empty, // Will be populated by signing service
            };

            dbContext.MembershipRecords.Add(membershipRecord);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("User {PeerId} joined pod {PodId}", member.PeerId, podId);
            return true;
        }

        public async Task<bool> LeaveAsync(string podId, string peerId, CancellationToken ct = default)
        {
            var member = await dbContext.Members
                .FirstOrDefaultAsync(m => m.PodId == podId && m.PeerId == peerId, ct);

            if (member == null)
            {
                logger.LogWarning("Attempted to remove non-member {PeerId} from pod {PodId}", peerId, podId);
                return false;
            }

            dbContext.Members.Remove(member);

            // Add membership record
            var membershipRecord = new SignedMembershipRecordEntity
            {
                PodId = podId,
                PeerId = peerId,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Action = "leave",
                Signature = string.Empty,
            };

            dbContext.MembershipRecords.Add(membershipRecord);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("User {PeerId} left pod {PodId}", peerId, podId);
            return true;
        }

        public async Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default)
        {
            var member = await dbContext.Members
                .FirstOrDefaultAsync(m => m.PodId == podId && m.PeerId == peerId, ct);

            if (member == null)
            {
                logger.LogWarning("Attempted to ban non-member {PeerId} from pod {PodId}", peerId, podId);
                return false;
            }

            member.IsBanned = true;

            // Add membership record
            var membershipRecord = new SignedMembershipRecordEntity
            {
                PodId = podId,
                PeerId = peerId,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Action = "ban",
                Signature = string.Empty,
            };

            dbContext.MembershipRecords.Add(membershipRecord);
            await dbContext.SaveChangesAsync(ct);

            logger.LogInformation("User {PeerId} banned from pod {PodId}", peerId, podId);
            return true;
        }

        private static Pod EntityToPod(PodEntity entity) => new Pod
        {
            PodId = entity.PodId,
            Name = entity.Name,
            Visibility = entity.Visibility,
            FocusContentId = entity.FocusContentId,
            Tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(entity.Tags ?? "[]"),
            Channels = System.Text.Json.JsonSerializer.Deserialize<List<PodChannel>>(entity.Channels ?? "[]"),
            ExternalBindings = System.Text.Json.JsonSerializer.Deserialize<List<ExternalBinding>>(entity.ExternalBindings ?? "[]"),
        };
    }
}
