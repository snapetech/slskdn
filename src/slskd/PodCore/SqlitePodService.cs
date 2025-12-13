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
            // SECURITY: Validate input
            var (isValid, error) = PodValidation.ValidatePod(pod);
            if (!isValid)
            {
                logger.LogWarning("Pod creation rejected: {Reason}", error);
                throw new ArgumentException(error, nameof(pod));
            }

            if (string.IsNullOrWhiteSpace(pod.PodId))
            {
                pod.PodId = $"pod:{Guid.NewGuid():N}";
            }

            // SECURITY: Sanitize inputs
            pod.Name = PodValidation.Sanitize(pod.Name, PodValidation.MaxPodNameLength);

            // SECURITY: Use transaction for atomicity
            using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var entity = new PodEntity
                {
                    PodId = pod.PodId,
                    Name = pod.Name,
                    Visibility = pod.Visibility,
                    FocusContentId = pod.FocusContentId,
                    Tags = System.Text.Json.JsonSerializer.Serialize(pod.Tags ?? new List<string>()),
                    Channels = System.Text.Json.JsonSerializer.Serialize(pod.Channels ?? new List<PodChannel>()),
                    ExternalBindings = System.Text.Json.JsonSerializer.Serialize(pod.ExternalBindings ?? new List<ExternalBinding>()),
                };

                dbContext.Pods.Add(entity);
                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                // SECURITY: Don't log sensitive pod details
                logger.LogInformation("Pod created successfully (ID length: {IdLength})", pod.PodId?.Length ?? 0);

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
                            logger.LogWarning(ex, "Failed to publish pod to DHT");
                        }
                    }, ct);
                }

                return pod;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating pod");
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default)
        {
            var entities = await dbContext.Pods.ToListAsync(ct);
            return entities.Select(EntityToPod).ToList();
        }

        public async Task<Pod?> GetPodAsync(string podId, CancellationToken ct = default)
        {
            // SECURITY: Validate pod ID format
            if (!PodValidation.IsValidPodId(podId))
            {
                logger.LogWarning("Invalid pod ID format in GetPodAsync");
                return null;
            }

            try
            {
                var entity = await dbContext.Pods.FindAsync(new object[] { podId }, ct);
                return entity == null ? null : EntityToPod(entity);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving pod");
                return null;
            }
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
            // SECURITY: Validate inputs
            if (!PodValidation.IsValidPodId(podId))
            {
                logger.LogWarning("Invalid pod ID in JoinAsync");
                return false;
            }

            var (isValid, error) = PodValidation.ValidateMember(member);
            if (!isValid)
            {
                logger.LogWarning("Member validation failed: {Reason}", error);
                return false;
            }

            // SECURITY: Use transaction
            using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var pod = await dbContext.Pods.FindAsync(new object[] { podId }, ct);
                if (pod == null)
                {
                    logger.LogWarning("Attempted to join non-existent pod");
                    return false;
                }

                var existingMember = await dbContext.Members
                    .FirstOrDefaultAsync(m => m.PodId == podId && m.PeerId == member.PeerId, ct);

                if (existingMember != null)
                {
                    if (existingMember.IsBanned)
                    {
                        logger.LogWarning("Banned user attempted to join pod");
                        return false;
                    }

                    logger.LogInformation("User already member of pod");
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
                await transaction.CommitAsync(ct);

                logger.LogInformation("User joined pod successfully");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during pod join operation");
                await transaction.RollbackAsync(ct);
                return false;
            }
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

        private static Pod EntityToPod(PodEntity entity)
        {
            try
            {
                return new Pod
                {
                    PodId = entity.PodId,
                    Name = entity.Name,
                    Visibility = entity.Visibility,
                    FocusContentId = entity.FocusContentId,
                    Tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(entity.Tags ?? "[]") ?? new List<string>(),
                    Channels = System.Text.Json.JsonSerializer.Deserialize<List<PodChannel>>(entity.Channels ?? "[]") ?? new List<PodChannel>(),
                    ExternalBindings = System.Text.Json.JsonSerializer.Deserialize<List<ExternalBinding>>(entity.ExternalBindings ?? "[]") ?? new List<ExternalBinding>(),
                };
            }
            catch (System.Text.Json.JsonException)
            {
                // SECURITY: Handle malformed JSON gracefully
                return new Pod
                {
                    PodId = entity.PodId,
                    Name = entity.Name,
                    Visibility = entity.Visibility,
                    FocusContentId = entity.FocusContentId,
                    Tags = new List<string>(),
                    Channels = new List<PodChannel>(),
                    ExternalBindings = new List<ExternalBinding>(),
                };
            }
        }
    }
}














