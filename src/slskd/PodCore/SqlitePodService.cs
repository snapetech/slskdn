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
        private readonly IDbContextFactory<PodDbContext> dbFactory;
        private readonly IPodPublisher podPublisher;
        private readonly IPodMembershipSigner membershipSigner;
        private readonly ILogger<SqlitePodService> logger;
        private readonly IServiceScopeFactory? scopeFactory;

    public SqlitePodService(
        IDbContextFactory<PodDbContext> dbFactory,
        IPodPublisher podPublisher,
        IPodMembershipSigner membershipSigner,
        ILogger<SqlitePodService> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        this.dbFactory = dbFactory;
        this.podPublisher = podPublisher;
        this.membershipSigner = membershipSigner;
        this.logger = logger;
        this.scopeFactory = scopeFactory;
    }

    private IContentLinkService? ContentLinkService => scopeFactory?.CreateScope().ServiceProvider.GetService<IContentLinkService>();

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
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            using var transaction = await db.Database.BeginTransactionAsync(ct);
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
                    Capabilities = System.Text.Json.JsonSerializer.Serialize(pod.Capabilities ?? new List<PodCapability>()),
                    PrivateServicePolicy = pod.PrivateServicePolicy != null ? System.Text.Json.JsonSerializer.Serialize(pod.PrivateServicePolicy) : null,
                };

                db.Pods.Add(entity);
                await db.SaveChangesAsync(ct);
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

        public async Task<Pod> UpdateAsync(Pod pod, CancellationToken ct = default)
        {
            // SECURITY: Validate input
            var (isValid, error) = PodValidation.ValidatePod(pod);
            if (!isValid)
            {
                logger.LogWarning("Pod update rejected: {Reason}", error);
                throw new ArgumentException(error, nameof(pod));
            }

            if (string.IsNullOrWhiteSpace(pod.PodId))
            {
                throw new ArgumentException("PodId is required for update", nameof(pod));
            }

            // SECURITY: Sanitize inputs
            pod.Name = PodValidation.Sanitize(pod.Name, PodValidation.MaxPodNameLength);

            // SECURITY: Use transaction for atomicity
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var existingEntity = await db.Pods.FirstOrDefaultAsync(p => p.PodId == pod.PodId, ct);
                if (existingEntity == null)
                {
                    throw new KeyNotFoundException($"Pod with ID {pod.PodId} not found");
                }

                // Update entity properties
                existingEntity.Name = pod.Name;
                existingEntity.Description = pod.Description;
                existingEntity.IsPublic = pod.IsPublic;
                existingEntity.MaxMembers = pod.MaxMembers;
                existingEntity.AllowGuests = pod.AllowGuests;
                existingEntity.RequireApproval = pod.RequireApproval;
                existingEntity.UpdatedAt = DateTimeOffset.UtcNow;

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                // Publish update
                await podPublisher.PublishAsync(pod, ct);

                return pod;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating pod {PodId}", pod.PodId);
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var entities = await db.Pods.ToListAsync(ct);
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
                await using var db = await dbFactory.CreateDbContextAsync(ct);
                var entity = await db.Pods.FindAsync(new object[] { podId }, ct);
                return entity == null ? null : EntityToPod(entity);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error retrieving pod");
                return null;
            }
        }

        public async Task<bool> DeletePodAsync(string podId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(podId) || !PodValidation.IsValidPodId(podId))
            {
                logger.LogWarning("Invalid pod ID in DeletePodAsync");
                return false;
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var pod = await db.Pods.FindAsync(new object[] { podId }, ct);
                if (pod == null)
                    return false;

                db.Messages.RemoveRange(await db.Messages.Where(m => m.PodId == podId).ToListAsync(ct));
                db.Members.RemoveRange(await db.Members.Where(m => m.PodId == podId).ToListAsync(ct));
                db.MembershipRecords.RemoveRange(await db.MembershipRecords.Where(r => r.PodId == podId).ToListAsync(ct));
                db.Pods.Remove(pod);

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                logger.LogInformation("Pod {PodId} deleted", podId);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting pod {PodId}", podId);
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<PodMember>> GetMembersAsync(string podId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var entities = await db.Members
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
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var entities = await db.MembershipRecords
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
            if (member == null)
                throw new ArgumentNullException(nameof(member));
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
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var pod = await db.Pods.FindAsync(new object[] { podId }, ct);
                if (pod == null)
                {
                    logger.LogWarning("Attempted to join non-existent pod");
                    return false;
                }

                var existingMember = await db.Members
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

                // Enforce MaxMembers for VPN (PrivateServiceGateway) pods
                var capabilities = !string.IsNullOrEmpty(pod.Capabilities)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<PodCapability>>(pod.Capabilities) : null;
                var policy = !string.IsNullOrEmpty(pod.PrivateServicePolicy) && pod.PrivateServicePolicy != "null"
                    ? System.Text.Json.JsonSerializer.Deserialize<PodPrivateServicePolicy>(pod.PrivateServicePolicy) : null;
                if (capabilities?.Contains(PodCapability.PrivateServiceGateway) == true && policy != null && policy.MaxMembers > 0)
                {
                    var memberCount = await db.Members.CountAsync(m => m.PodId == podId && !m.IsBanned, ct);
                    if (memberCount >= policy.MaxMembers)
                    {
                        logger.LogWarning("Join rejected: VPN pod {PodId} at capacity ({Count} >= {Max})", podId, memberCount, policy.MaxMembers);
                        return false;
                    }
                }

                var memberEntity = new PodMemberEntity
                {
                    PodId = podId,
                    PeerId = member.PeerId,
                    Role = member.Role ?? "member",
                    PublicKey = member.PublicKey,
                    IsBanned = false,
                };

                db.Members.Add(memberEntity);

                // Add membership record
                var membershipRecord = new SignedMembershipRecordEntity
                {
                    PodId = podId,
                    PeerId = member.PeerId,
                    TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Action = "join",
                    Signature = string.Empty, // Will be populated by signing service
                };

                db.MembershipRecords.Add(membershipRecord);
                await db.SaveChangesAsync(ct);
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
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var member = await db.Members
                .FirstOrDefaultAsync(m => m.PodId == podId && m.PeerId == peerId, ct);

            if (member == null)
            {
                logger.LogWarning("Attempted to remove non-member {PeerId} from pod {PodId}", peerId, podId);
                return false;
            }

            db.Members.Remove(member);

            // Add membership record
            var membershipRecord = new SignedMembershipRecordEntity
            {
                PodId = podId,
                PeerId = peerId,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Action = "leave",
                Signature = string.Empty,
            };

            db.MembershipRecords.Add(membershipRecord);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("User {PeerId} left pod {PodId}", peerId, podId);
            return true;
        }

        public async Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var member = await db.Members
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

            db.MembershipRecords.Add(membershipRecord);
            await db.SaveChangesAsync(ct);

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
                    Capabilities = System.Text.Json.JsonSerializer.Deserialize<List<PodCapability>>(entity.Capabilities ?? "[]") ?? new List<PodCapability>(),
                    PrivateServicePolicy = !string.IsNullOrEmpty(entity.PrivateServicePolicy) && entity.PrivateServicePolicy != "null"
                        ? System.Text.Json.JsonSerializer.Deserialize<PodPrivateServicePolicy>(entity.PrivateServicePolicy) : null,
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
                    Capabilities = new List<PodCapability>(),
                    PrivateServicePolicy = null,
                };
            }
        }

        // Channel management implementation
        public async Task<PodChannel> CreateChannelAsync(string podId, PodChannel channel, CancellationToken ct = default)
        {
            // Verify pod exists
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var podEntity = await db.Pods.FindAsync(new object[] { podId }, ct);
            if (podEntity == null)
            {
                throw new ArgumentException($"Pod {podId} does not exist", nameof(podId));
            }

            // Generate channel ID if not provided
            if (string.IsNullOrWhiteSpace(channel.ChannelId))
            {
                channel.ChannelId = $"channel:{Guid.NewGuid():N}";
            }

            // Validate channel data
            if (string.IsNullOrWhiteSpace(channel.Name))
            {
                throw new ArgumentException("Channel name is required", nameof(channel));
            }

            // Load current pod data
            var pod = await GetPodAsync(podId, ct);
            if (pod == null)
            {
                throw new ArgumentException($"Pod {podId} does not exist", nameof(podId));
            }

            // Add channel
            pod.Channels ??= new List<PodChannel>();
            pod.Channels.Add(channel);

            // Update database
            podEntity.Channels = System.Text.Json.JsonSerializer.Serialize(pod.Channels);
            await db.SaveChangesAsync(ct);

            // Publish updated pod to DHT if listed
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
                        logger.LogError(ex, "Failed to publish pod update to DHT");
                    }
                }, ct);
            }

            logger.LogInformation("Created channel {ChannelId} in pod {PodId}", channel.ChannelId, podId);
            return channel;
        }

        public async Task<bool> DeleteChannelAsync(string podId, string channelId, CancellationToken ct = default)
        {
            // Verify pod exists
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var podEntity = await db.Pods.FindAsync(new object[] { podId }, ct);
            if (podEntity == null)
            {
                return false;
            }

            // Load current pod data
            var pod = await GetPodAsync(podId, ct);
            if (pod?.Channels == null)
            {
                return false;
            }

            var channelIndex = pod.Channels.FindIndex(c => c.ChannelId == channelId);
            if (channelIndex == -1)
            {
                return false;
            }

            // Don't allow deletion of system channels
            var channel = pod.Channels[channelIndex];
            if (channel.Kind == PodChannelKind.General && channel.Name.ToLowerInvariant() == "general")
            {
                throw new InvalidOperationException("Cannot delete the default general channel");
            }

            // Remove channel
            pod.Channels.RemoveAt(channelIndex);

            // Update database
            podEntity.Channels = System.Text.Json.JsonSerializer.Serialize(pod.Channels);
            await db.SaveChangesAsync(ct);

            // Publish updated pod to DHT if listed
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
                        logger.LogError(ex, "Failed to publish pod update to DHT");
                    }
                }, ct);
            }

            logger.LogInformation("Deleted channel {ChannelId} from pod {PodId}", channelId, podId);
            return true;
        }

        public async Task<PodChannel?> GetChannelAsync(string podId, string channelId, CancellationToken ct = default)
        {
            var pod = await GetPodAsync(podId, ct);
            return pod?.Channels?.FirstOrDefault(c => c.ChannelId == channelId);
        }

        public async Task<IReadOnlyList<PodChannel>> GetChannelsAsync(string podId, CancellationToken ct = default)
        {
            var pod = await GetPodAsync(podId, ct);
            return (IReadOnlyList<PodChannel>)(pod?.Channels ?? new List<PodChannel>());
        }

        public async Task<bool> UpdateChannelAsync(string podId, PodChannel channel, CancellationToken ct = default)
        {
            // Verify pod exists
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var podEntity = await db.Pods.FindAsync(new object[] { podId }, ct);
            if (podEntity == null)
            {
                return false;
            }

            // Load current pod data
            var pod = await GetPodAsync(podId, ct);
            if (pod?.Channels == null)
            {
                return false;
            }

            var existingChannel = pod.Channels.FirstOrDefault(c => c.ChannelId == channel.ChannelId);
            if (existingChannel == null)
            {
                return false;
            }

            // Validate channel data
            if (string.IsNullOrWhiteSpace(channel.Name))
            {
                throw new ArgumentException("Channel name is required", nameof(channel));
            }

            // Don't allow changing system channels
            if (existingChannel.Kind == PodChannelKind.General &&
                existingChannel.Name.ToLowerInvariant() == "general" &&
                (channel.Kind != PodChannelKind.General || channel.Name.ToLowerInvariant() != "general"))
            {
                throw new InvalidOperationException("Cannot modify the default general channel");
            }

            // Update channel
            var index = pod.Channels.IndexOf(existingChannel);
            pod.Channels[index] = channel;

            // Update database
            podEntity.Channels = System.Text.Json.JsonSerializer.Serialize(pod.Channels);
            await db.SaveChangesAsync(ct);

            // Publish updated pod to DHT if listed
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
                        logger.LogError(ex, "Failed to publish pod update to DHT");
                    }
                }, ct);
            }

            logger.LogInformation("Updated channel {ChannelId} in pod {PodId}", channel.ChannelId, podId);
            return true;
        }

        // Content linking implementation
        public async Task<ContentValidationResult> ValidateContentLinkAsync(string contentId, CancellationToken ct = default)
        {
            if (ContentLinkService == null)
            {
                // If no content service available, only do basic format validation
                var isValid = !string.IsNullOrWhiteSpace(contentId) &&
                             contentId.StartsWith("content:", StringComparison.OrdinalIgnoreCase);

                return new ContentValidationResult(
                    IsValid: isValid,
                    ContentId: contentId,
                    ErrorMessage: isValid ? null : "Invalid content ID format or content link service unavailable");
            }

            return await ContentLinkService.ValidateContentIdAsync(contentId, ct);
        }

        public async Task<Pod> CreateContentLinkedPodAsync(Pod pod, CancellationToken ct = default)
        {
            // Validate content link if specified
            if (!string.IsNullOrWhiteSpace(pod.FocusContentId))
            {
                var validation = await ValidateContentLinkAsync(pod.FocusContentId, ct);
                if (!validation.IsValid)
                {
                    throw new ArgumentException($"Invalid content link: {validation.ErrorMessage}", nameof(pod));
                }

                // Enhance pod name with content metadata if available
                if (validation.Metadata != null && string.IsNullOrWhiteSpace(pod.Name))
                {
                    pod.Name = $"{validation.Metadata.Artist} - {validation.Metadata.Title}";
                }

                // Add content-based tags
                pod.Tags ??= new List<string>();
                if (validation.Metadata != null)
                {
                    if (!pod.Tags.Contains($"content:{validation.Metadata.Domain}"))
                    {
                        pod.Tags.Add($"content:{validation.Metadata.Domain}");
                    }
                    if (!pod.Tags.Contains($"type:{validation.Metadata.Type}"))
                    {
                        pod.Tags.Add($"type:{validation.Metadata.Type}");
                    }
                }
            }

            // Create the pod normally
            return await CreateAsync(pod, ct);
        }
    }
}
