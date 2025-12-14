// <copyright file="ConversationService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

// <copyright file="ConversationService.cs" company="slskd Team">
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

namespace slskd.Messaging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Serilog;
    using slskd.Events;
    using slskd.PodCore;
    using Soulseek;

    /// <summary>
    ///     Manages private messages.
    /// </summary>
    public interface IConversationService
    {
        /// <summary>
        ///     Acknowledges all unacknowledged <see cref="PrivateMessage"/> records from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        Task AcknowledgeAsync(string username);

        /// <summary>
        ///     Acknowledges the <see cref="PrivateMessage"/> record associated with the specified <paramref name="username"/> and <paramref name="id"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="id">The ID of the message.</param>
        /// <returns>The operation context.</returns>
        Task AcknowledgeMessageAsync(string username, int id);

        /// <summary>
        ///     Creates a new, or activates an existing, conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        Task CreateAsync(string username);

        /// <summary>
        ///     Returns the <see cref="Conversation"/> record associated with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="includeInactive">A value indicating whether to include conversations marked as inactive.</param>
        /// <param name="includeMessages">A value indicating whether <see cref="PrivateMessage"/> records should be included in the return value.</param>
        /// <returns>The operation context, including the located conversation, if one was found.</returns>
        Task<Conversation> FindAsync(string username, bool includeInactive = true, bool includeMessages = false);

        /// <summary>
        ///     Returns the <see cref="PrivateMessage"/> record associated with the specified <paramref name="username"/> and <paramref name="id"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="id">The ID of the message.</param>
        /// <returns>The operation context, including the located message, if one was found.</returns>
        Task<PrivateMessage> FindMessageAsync(string username, int id);

        /// <summary>
        ///     Returns the list of all <see cref="Conversation"/> records matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to locate conversations.</param>
        /// <returns>The operation context, including the list of found conversations.</returns>
        Task<IEnumerable<Conversation>> ListAsync(Expression<Func<Conversation, bool>> expression = null);

        /// <summary>
        ///     Returns the list of all <see cref="PrivateMessage"/> records matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to locate private messages.</param>
        /// <returns>The operation context, including the list of found private messages.</returns>
        Task<IEnumerable<PrivateMessage>> ListMessagesAsync(Expression<Func<PrivateMessage, bool>> expression = null);

        /// <summary>
        ///     Handles the receipt of an inbound <see cref="PrivateMessage"/>.
        /// </summary>
        /// <param name="username">The username associated with the message.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        Task HandleMessageAsync(string username, PrivateMessage message);

        /// <summary>
        ///     Removes (marks inactive) the conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        Task RemoveAsync(string username);

        /// <summary>
        ///     Sends the specified <paramref name="message"/> to the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the recipient.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        Task SendMessageAsync(string username, string message);
    }

    public class ConversationService : IConversationService
    {
        public ConversationService(
            ISoulseekClient soulseekClient,
            EventBus eventBus,
            IDbContextFactory<MessagingDbContext> contextFactory,
            PodCore.IPodService podService)
        {
            SoulseekClient = soulseekClient;
            EventBus = eventBus;
            ContextFactory = contextFactory;
            PodService = podService;
        }

        private EventBus EventBus { get; }
        private IDbContextFactory<MessagingDbContext> ContextFactory { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ConversationService>();
        private ISoulseekClient SoulseekClient { get; }
        private PodCore.IPodService PodService { get; }

        // HARDENING: DM pod creation rate limiting
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTimeOffset> lastPodCreationByPeer = new();
        private readonly object rateLimitLock = new();
        private const int MinHoursBetweenPodCreationsPerPeer = 24; // 1 pod per peer per day
        private const int MaxNewPodsPerDay = 50; // Global limit
        private DateTimeOffset currentDayStart = DateTimeOffset.UtcNow.Date;
        private int podsCreatedToday = 0;

        /// <summary>
        /// Validates a Soulseek username for DM pod creation.
        /// </summary>
        private static bool IsValidSoulseekUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return false;
            }

            // Basic length check (Soulseek allows up to 64 chars, but be conservative)
            if (username.Length > 64 || username.Length < 1)
            {
                return false;
            }

            // Allow only alphanumeric, underscore, hyphen, dot (common in usernames)
            // Reject control characters, whitespace, colon (to prevent injection)
            foreach (var c in username)
            {
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                      (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.'))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if DM pod creation is allowed based on rate limits to prevent DoS attacks.
        /// </summary>
        private bool IsDmPodCreationAllowed(string username)
        {
            lock (rateLimitLock)
            {
                var now = DateTimeOffset.UtcNow;

                // Reset daily counter if it's a new day
                if (now.Date != currentDayStart.Date)
                {
                    currentDayStart = now.Date;
                    podsCreatedToday = 0;
                }

                // Check global daily limit
                if (podsCreatedToday >= MaxNewPodsPerDay)
                {
                    Log.Warning("Global DM pod creation limit ({Limit}) exceeded for today", MaxNewPodsPerDay);
                    return false;
                }

                // Check per-peer rate limit
                var peerId = $"bridge:{username}"; // Use bridge format for peer ID
                if (lastPodCreationByPeer.TryGetValue(peerId, out var lastCreation))
                {
                    var timeSinceLastCreation = now - lastCreation;
                    if (timeSinceLastCreation.TotalHours < MinHoursBetweenPodCreationsPerPeer)
                    {
                        Log.Warning("DM pod creation rate limit exceeded for user {Username}. Next allowed: {NextAllowed}",
                            username, lastCreation.AddHours(MinHoursBetweenPodCreationsPerPeer));
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Creates a DM pod for the given username if it doesn't exist and rate limits allow.
        /// </summary>
        private async Task EnsureDmPodAsync(string username)
        {
            try
            {
                // HARDENING: Validate username format
                if (!IsValidSoulseekUsername(username))
                {
                    Log.Warning("Invalid username format for DM pod creation: {Username}", username);
                    return;
                }

                // Check rate limits first
                if (!IsDmPodCreationAllowed(username))
                {
                    Log.Debug("DM pod creation blocked by rate limits for user {Username}", username);
                    return;
                }

                // Generate deterministic pod ID
                var localPeerId = "peer:mesh:self"; // Assume local peer
                var remotePeerId = $"bridge:{username}";
                var podId = PodIdFactory.ConversationPodId(localPeerId, remotePeerId);

                // Check if pod already exists
                var existingPod = await PodService.GetPodAsync(podId);
                if (existingPod != null)
                {
                    Log.Debug("DM pod {PodId} already exists for user {Username}", podId, username);
                    return;
                }

                // Create DM pod
                Log.Information("Creating DM pod {PodId} for user {Username}", podId, username);

                var pod = new PodCore.Pod
                {
                    PodId = podId,
                    Name = username,
                    Visibility = PodCore.PodVisibility.Private,
                    Tags = new List<string> { "dm" },
                    Channels = new List<PodCore.PodChannel>
                    {
                        new PodCore.PodChannel
                        {
                            ChannelId = "dm",
                            Name = "DM",
                            Kind = PodCore.PodChannelKind.General,
                            BindingInfo = $"soulseek-dm:{username}"
                        }
                    }
                };

                var createdPod = await PodService.CreateAsync(pod);
                if (createdPod != null)
                {
                    // Add members
                    var localMember = new PodCore.PodMember
                    {
                        PeerId = localPeerId,
                        Role = "member"
                    };
                    await PodService.JoinAsync(podId, localMember);

                    var remoteMember = new PodCore.PodMember
                    {
                        PeerId = remotePeerId,
                        Role = "member"
                    };
                    await PodService.JoinAsync(podId, remoteMember);

                    // Update rate limit counters
                    lock (rateLimitLock)
                    {
                        lastPodCreationByPeer[remotePeerId] = DateTimeOffset.UtcNow;
                        podsCreatedToday++;
                    }

                    Log.Information("Successfully created DM pod {PodId} for user {Username}", podId, username);
                }
                else
                {
                    Log.Error("Failed to create DM pod for user {Username}", username);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating DM pod for user {Username}", username);
            }
        }

        /// <summary>
        ///     Acknowledges all unacknowledged <see cref="PrivateMessage"/> records from the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        public async Task AcknowledgeAsync(string username)
        {
            using var context = ContextFactory.CreateDbContext();

            var unacked = context.PrivateMessages
                .Where(m => m.Username == username && !m.IsAcknowledged);

            foreach (var message in unacked)
            {
                await SoulseekClient.AcknowledgePrivateMessageAsync(message.Id);
                message.IsAcknowledged = true;
            }

            context.SaveChanges();
        }

        /// <summary>
        ///     Acknowledges the <see cref="PrivateMessage"/> record associated with the specified <paramref name="username"/> and <paramref name="id"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="id">The ID of the message.</param>
        /// <returns>The operation context.</returns>
        public async Task AcknowledgeMessageAsync(string username, int id)
        {
            using var context = ContextFactory.CreateDbContext();
            var message = context.PrivateMessages.SingleOrDefault(m => m.Username == username && m.Id == id);

            if (message != default)
            {
                await SoulseekClient.AcknowledgePrivateMessageAsync(message.Id);
                message.IsAcknowledged = true;
                context.SaveChanges();
            }
            else
            {
                Log.Warning("Attempted to acknowledge an unknown private message from {Username} with ID {Id}", username, id);
            }
        }

        /// <summary>
        ///     Creates a new, or activates an existing, conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        public Task CreateAsync(string username)
        {
            ActivateConversation(username);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Returns the <see cref="Conversation"/> record associated with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="includeInactive">A value indicating whether to include conversations marked as inactive.</param>
        /// <param name="includeMessages">A value indicating whether <see cref="PrivateMessage"/> records should be included in the return value.</param>
        /// <returns>The operation context, including the located conversation, if one was found.</returns>
        public async Task<Conversation> FindAsync(string username, bool includeInactive = true, bool includeMessages = false)
        {
            using var context = ContextFactory.CreateDbContext();

            var conversation = context.Conversations
                .AsNoTracking()
                .Where(c => c.Username == username && (includeInactive || c.IsActive))
                .SingleOrDefault();

            if (conversation != default)
            {
                if (includeMessages)
                {
                    conversation.Messages = await ListMessagesAsync(m => m.Username == conversation.Username);
                    conversation.UnAcknowledgedMessageCount = conversation.Messages.Count(m => !m.IsAcknowledged);
                }
                else
                {
                    var unacked = await ListMessagesAsync(m => m.Username == conversation.Username && !m.IsAcknowledged);
                    conversation.UnAcknowledgedMessageCount = unacked.Count();
                }
            }

            return conversation;
        }

        /// <summary>
        ///     Returns the <see cref="PrivateMessage"/> record associated with the specified <paramref name="username"/> and <paramref name="id"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <param name="id">The ID of the message.</param>
        /// <returns>The operation context, including the located message, if one was found.</returns>
        public Task<PrivateMessage> FindMessageAsync(string username, int id)
        {
            using var context = ContextFactory.CreateDbContext();

            var message = context.PrivateMessages
                .AsNoTracking()
                .Where(m => m.Username == username && m.Id == id)
                .SingleOrDefault();

            return Task.FromResult(message);
        }

        /// <summary>
        ///     Returns the list of all <see cref="Conversation"/> records matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to locate conversations.</param>
        /// <returns>The operation context, including the list of found conversations.</returns>
        public Task<IEnumerable<Conversation>> ListAsync(Expression<Func<Conversation, bool>> expression = null)
        {
            using var context = ContextFactory.CreateDbContext();

            // todo: replace this garbage with Dapper and a real SQL query
            var unAckedMessages = context.PrivateMessages
                .AsNoTracking()
                .Where(m => !m.IsAcknowledged)
                .ToList();

            var conversations = context.Conversations
                .AsNoTracking()
                .Where(expression)
                .OrderBy(c => c.Username)
                .ToList();

            var response = conversations.Select(c => new Conversation
            {
                Username = c.Username,
                IsActive = c.IsActive,
                UnAcknowledgedMessageCount = unAckedMessages.Count(m => m.Username == c.Username),
            });

            return Task.FromResult(response);
        }

        /// <summary>
        ///     Returns the list of all <see cref="PrivateMessage"/> records matching the specified <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">An optional expression used to locate private messages.</param>
        /// <returns>The operation context, including the list of found private messages.</returns>
        public Task<IEnumerable<PrivateMessage>> ListMessagesAsync(Expression<Func<PrivateMessage, bool>> expression = null)
        {
            using var context = ContextFactory.CreateDbContext();

            var response = context.PrivateMessages
                .AsNoTracking()
                .Where(expression)
                .OrderByDescending(m => m.Timestamp)
                .Take(100) // stupid.  TakeLast doesn't work
                .OrderBy(m => m.Timestamp)
                .ToList()
                .AsEnumerable();

            return Task.FromResult(response);
        }

        /// <summary>
        ///     Handles the receipt of an inbound <see cref="PrivateMessage"/>.
        /// </summary>
        /// <param name="username">The username associated with the message.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        public Task HandleMessageAsync(string username, PrivateMessage message)
        {
            ActivateConversation(username);

            using var context = ContextFactory.CreateDbContext();

            // the server replays unacked messages when we log in. figure out if we've seen this message before, and if so sync it.
            // note that the table for PMs uses a composite key; this expression should match whatever is in the DbContext.
            var existing = context.PrivateMessages.FirstOrDefault(m =>
                m.Username == message.Username &&
                m.Id == message.Id &&
                m.Timestamp == message.Timestamp);

            if (existing != null)
            {
                // the message was replayed. either we haven't ACKed it, or we did and the ACK didn't make it to the
                // server. either way we need to ensure that the local db understands the message needs to be ACKed, so
                // force the bit false. we shouldn't need to update any other part of the message because IDs are unique
                // on the soulseek server
                existing.IsAcknowledged = false;
                existing.WasReplayed = message.WasReplayed;
            }
            else
            {
                // this is a new message, so append it
                context.PrivateMessages.Add(message);
            }

            context.SaveChanges();

            EventBus.Raise(new PrivateMessageReceivedEvent
            {
                Message = message,
            });

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Removes (marks inactive) the conversation with the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username associated with the conversation.</param>
        /// <returns>The operation context.</returns>
        public async Task RemoveAsync(string username)
        {
            await AcknowledgeAsync(username);

            using var context = ContextFactory.CreateDbContext();

            var conversation = context.Conversations.FirstOrDefault(c => c.Username == username);

            if (conversation != default)
            {
                conversation.IsActive = false;
                context.SaveChanges();
            }
            else
            {
                Log.Warning("Attempted to remove an unknown conversation with {Username}", username);
            }
        }

        /// <summary>
        ///     Sends the specified <paramref name="message"/> to the specified <paramref name="username"/>.
        /// </summary>
        /// <param name="username">The username of the recipient.</param>
        /// <param name="message">The message.</param>
        /// <returns>The operation context.</returns>
        public async Task SendMessageAsync(string username, string message)
        {
            ActivateConversation(username);

            // send the message over the network, then persist this should *probably* use a transaction but i'm scared of locking
            // with SQLite so i won't
            await SoulseekClient.SendPrivateMessageAsync(username, message);

            using var context = ContextFactory.CreateDbContext();

            context.PrivateMessages.Add(new PrivateMessage
            {
                Timestamp = DateTime.UtcNow,
                Id = 0, // the server assigns IDs. this message will get one but it'll only be known to the recipient
                Username = username,
                Direction = MessageDirection.Out,
                Message = message,
                IsAcknowledged = true,
            });

            context.SaveChanges();
        }

        private void ActivateConversation(string username)
        {
            using var context = ContextFactory.CreateDbContext();

            var conversation = context.Conversations.FirstOrDefault(c => c.Username == username);

            if (conversation != default)
            {
                conversation.IsActive = true;
            }
            else
            {
                context.Conversations.Add(new Conversation { Username = username, IsActive = true });

                // HARDENING: Create DM pod for new conversation
                _ = Task.Run(() => EnsureDmPodAsync(username));
            }

            try
            {
                context.SaveChanges();
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("UNIQUE constraint failed: Conversations.Username") == true)
            {
                // Duplicate insert under contention; safe to ignore
                Log.Debug("Ignored duplicate conversation insert for {Username}", username);
            }
        }
    }
}
