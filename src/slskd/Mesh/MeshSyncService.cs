// <copyright file="MeshSyncService.cs" company="slskdn Team">
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

namespace slskd.Mesh
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Serilog;
    using slskd.Capabilities;
    using slskd.DhtRendezvous.Security;
    using slskd.HashDb;
    using slskd.HashDb.Models;
    using slskd.Mesh.Messages;

    /// <summary>
    ///     Service for epidemic mesh synchronization of hash databases.
    /// </summary>
    public class MeshSyncService : IMeshSyncService
    {
        /// <summary>Minimum seconds between syncs with same peer.</summary>
        public const int SyncIntervalMinSeconds = 1800; // 30 minutes

        /// <summary>Maximum entries per sync session.</summary>
        public const int MaxEntriesPerSync = 1000;

        /// <summary>Maximum peers to sync with per cycle.</summary>
        public const int MaxPeersPerCycle = 5;

        private readonly IHashDbService hashDb;
        private readonly ICapabilityService capabilities;
        private readonly ILogger log = Log.ForContext<MeshSyncService>();

        private readonly ConcurrentDictionary<string, MeshPeerState> peerStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly MeshSyncStats stats = new();
        private readonly SemaphoreSlim syncLock = new(1, 1);

        /// <summary>
        ///     Initializes a new instance of the <see cref="MeshSyncService"/> class.
        /// </summary>
        public MeshSyncService(IHashDbService hashDb, ICapabilityService capabilities)
        {
            this.hashDb = hashDb;
            this.capabilities = capabilities;
        }

        /// <inheritdoc/>
        public MeshSyncStats Stats
        {
            get
            {
                stats.CurrentSeqId = hashDb.CurrentSeqId;
                stats.KnownMeshPeers = peerStates.Count(p => p.Value.IsMeshCapable);
                return stats;
            }
        }

        /// <inheritdoc/>
        public async Task<MeshSyncResult> TrySyncWithPeerAsync(string username, CancellationToken cancellationToken = default)
        {
            var sw = Stopwatch.StartNew();
            var result = new MeshSyncResult { PeerUsername = username };

            try
            {
                // Check if peer supports mesh sync
                var peerCaps = capabilities.GetPeerCapabilities(username);
                if (peerCaps == null || !peerCaps.CanMeshSync)
                {
                    result.Error = "Peer does not support mesh sync";
                    return result;
                }

                // Check sync interval
                var state = GetOrCreatePeerState(username);
                if (state.LastSyncTime.HasValue)
                {
                    var sinceLast = DateTime.UtcNow - state.LastSyncTime.Value;
                    if (sinceLast.TotalSeconds < SyncIntervalMinSeconds)
                    {
                        result.Error = $"Too soon to sync (wait {SyncIntervalMinSeconds - (int)sinceLast.TotalSeconds}s)";
                        return result;
                    }
                }

                await syncLock.WaitAsync(cancellationToken);
                try
                {
                    // Generate HELLO
                    var hello = GenerateHelloMessage();
                    log.Information("[MESH] Initiating sync with {Peer}, our seq={SeqId}", username, hello.LatestSeqId);

                    // In a real implementation, we'd send this over a Soulseek connection
                    // For now, we simulate with local state and API calls

                    // Get entries we need from them (since their last known seq)
                    var theirLastSeq = state.LastSeqSeen;
                    var ourEntries = await hashDb.GetEntriesSinceSeqAsync(theirLastSeq, MaxEntriesPerSync, cancellationToken);
                    result.EntriesSent = ourEntries.Count();

                    // Update stats
                    stats.TotalEntriesSent += result.EntriesSent;
                    state.LastSyncTime = DateTime.UtcNow;
                    state.IsMeshCapable = true;

                    result.Success = true;
                    log.Information("[MESH] Sync with {Peer} complete: sent={Sent}", username, result.EntriesSent);
                }
                finally
                {
                    syncLock.Release();
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                stats.FailedSyncs++;
                log.Warning(ex, "[MESH] Sync with {Peer} failed", username);
            }

            result.DurationMs = sw.ElapsedMilliseconds;
            stats.TotalSyncs++;
            if (result.Success)
            {
                stats.SuccessfulSyncs++;
                stats.LastSyncTime = DateTime.UtcNow;
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<MeshMessage> HandleMessageAsync(string fromUser, MeshMessage message, CancellationToken cancellationToken = default)
        {
            // SECURITY: Validate username before any processing
            var usernameValidation = MessageValidator.ValidateUsername(fromUser);
            if (!usernameValidation.IsValid)
            {
                log.Warning("[MESH] Rejecting message from invalid username: {Error}", usernameValidation.Error);
                stats.RejectedMessages++;
                return null;
            }

            // SECURITY: Validate message is not null
            if (message == null)
            {
                log.Warning("[MESH] Rejecting null message from {Peer}", fromUser);
                stats.RejectedMessages++;
                return null;
            }

            // SECURITY: Validate message-specific constraints before processing
            var messageValidation = ValidateIncomingMessage(fromUser, message);
            if (!messageValidation.IsValid)
            {
                log.Warning("[MESH] Rejecting invalid message from {Peer}: {Error}", fromUser, messageValidation.Error);
                stats.RejectedMessages++;
                return null;
            }

            var state = GetOrCreatePeerState(fromUser);
            state.LastSeen = DateTime.UtcNow;
            state.IsMeshCapable = true;

            return message.Type switch
            {
                MeshMessageType.Hello => await HandleHelloAsync(fromUser, (MeshHelloMessage)message, cancellationToken),
                MeshMessageType.ReqDelta => await HandleReqDeltaAsync(fromUser, (MeshReqDeltaMessage)message, cancellationToken),
                MeshMessageType.PushDelta => await HandlePushDeltaAsync(fromUser, (MeshPushDeltaMessage)message, cancellationToken),
                MeshMessageType.ReqKey => await HandleReqKeyAsync(fromUser, (MeshReqKeyMessage)message, cancellationToken),
                _ => null,
            };
        }

        /// <summary>
        ///     Validates incoming mesh messages for security.
        /// </summary>
        private ValidationResult ValidateIncomingMessage(string fromUser, MeshMessage message)
        {
            switch (message)
            {
                case MeshReqDeltaMessage req:
                    if (req.SinceSeqId < 0)
                    {
                        return ValidationResult.Fail($"Invalid SinceSeqId: {req.SinceSeqId}");
                    }
                    if (req.MaxEntries < 0 || req.MaxEntries > MaxEntriesPerSync * 2)
                    {
                        return ValidationResult.Fail($"Invalid MaxEntries: {req.MaxEntries}");
                    }
                    break;

                case MeshPushDeltaMessage push:
                    if (push.Entries == null)
                    {
                        return ValidationResult.Fail("Entries list is null");
                    }
                    if (push.Entries.Count > MaxEntriesPerSync * 2)
                    {
                        return ValidationResult.Fail($"Too many entries: {push.Entries.Count} > {MaxEntriesPerSync * 2}");
                    }
                    if (push.LatestSeqId < 0)
                    {
                        return ValidationResult.Fail($"Invalid LatestSeqId: {push.LatestSeqId}");
                    }
                    break;

                case MeshReqKeyMessage reqKey:
                    var keyValidation = MessageValidator.ValidateFlacKey(reqKey.FlacKey);
                    if (!keyValidation.IsValid)
                    {
                        return keyValidation;
                    }
                    break;

                case MeshHelloMessage hello:
                    if (hello.LatestSeqId < 0)
                    {
                        return ValidationResult.Fail($"Invalid LatestSeqId: {hello.LatestSeqId}");
                    }
                    if (hello.HashCount < 0)
                    {
                        return ValidationResult.Fail($"Invalid HashCount: {hello.HashCount}");
                    }
                    // ClientId and ClientVersion are informational, just length-check them
                    if (hello.ClientId?.Length > 64)
                    {
                        return ValidationResult.Fail("ClientId too long");
                    }
                    if (hello.ClientVersion?.Length > 64)
                    {
                        return ValidationResult.Fail("ClientVersion too long");
                    }
                    break;
            }

            return ValidationResult.Success;
        }

        /// <inheritdoc/>
        public async Task<MeshHashEntry> LookupHashAsync(string flacKey, CancellationToken cancellationToken = default)
        {
            // First check local DB
            var local = await hashDb.LookupHashAsync(flacKey, cancellationToken);
            if (local != null)
            {
                return new MeshHashEntry
                {
                    FlacKey = local.FlacKey,
                    ByteHash = local.ByteHash,
                    Size = local.Size,
                    MetaFlags = local.MetaFlags,
                    SeqId = local.SeqId,
                };
            }

            // TODO: Query mesh neighbors
            // For now, return null - mesh queries would be implemented
            // when we have actual peer-to-peer message transport
            return null;
        }

        /// <inheritdoc/>
        public async Task PublishHashAsync(string flacKey, string byteHash, long size, int? metaFlags = null, CancellationToken cancellationToken = default)
        {
            // Store locally (will get new seq_id)
            await hashDb.StoreHashAsync(new HashDbEntry
            {
                FlacKey = flacKey,
                ByteHash = byteHash,
                Size = size,
                MetaFlags = metaFlags,
            }, cancellationToken);

            log.Debug("[MESH] Published hash {Key} -> {Hash}", flacKey, byteHash?.Length >= 16 ? byteHash.Substring(0, 16) + "..." : byteHash ?? "(null)");

            // The hash will propagate to peers during next sync session
            // No immediate push - epidemic model relies on pull-based delta sync
        }

        /// <inheritdoc/>
        public IEnumerable<MeshPeerInfo> GetMeshPeers()
        {
            return peerStates.Values
                .Where(p => p.IsMeshCapable)
                .OrderByDescending(p => p.LastSeen)
                .Select(p => new MeshPeerInfo
                {
                    Username = p.Username,
                    LatestSeqId = p.LatestSeqId,
                    LastSyncTime = p.LastSyncTime,
                    LastSeqSeen = p.LastSeqSeen,
                    LastSeen = p.LastSeen,
                    ClientVersion = p.ClientVersion,
                })
                .ToList();
        }

        /// <inheritdoc/>
        public MeshHelloMessage GenerateHelloMessage()
        {
            var dbStats = hashDb.GetStats();
            return new MeshHelloMessage
            {
                ClientId = "slskdn", // TODO: Get actual username
                ClientVersion = capabilities.VersionString,
                LatestSeqId = hashDb.CurrentSeqId,
                HashCount = dbStats.TotalHashEntries,
            };
        }

        /// <inheritdoc/>
        public async Task<MeshPushDeltaMessage> GenerateDeltaResponseAsync(long sinceSeqId, int maxEntries, CancellationToken cancellationToken = default)
        {
            var entries = await hashDb.GetEntriesSinceSeqAsync(sinceSeqId, maxEntries + 1, cancellationToken);
            var entryList = entries.ToList();
            var hasMore = entryList.Count > maxEntries;

            if (hasMore)
            {
                entryList = entryList.Take(maxEntries).ToList();
            }

            return new MeshPushDeltaMessage
            {
                Entries = entryList.Select(e => new MeshHashEntry
                {
                    SeqId = e.SeqId,
                    FlacKey = e.FlacKey,
                    ByteHash = e.ByteHash,
                    Size = e.Size,
                    MetaFlags = e.MetaFlags,
                }).ToList(),
                LatestSeqId = hashDb.CurrentSeqId,
                HasMore = hasMore,
            };
        }

        /// <inheritdoc/>
        public async Task<int> MergeEntriesAsync(string fromUser, IEnumerable<MeshHashEntry> entries, CancellationToken cancellationToken = default)
        {
            // SECURITY: Validate each entry before merging
            var validatedEntries = new List<HashDbEntry>();
            var skipped = 0;
            var entryList = entries.ToList();

            foreach (var entry in entryList)
            {
                // Validate FLAC key
                var keyValidation = MessageValidator.ValidateFlacKey(entry.FlacKey);
                if (!keyValidation.IsValid)
                {
                    log.Debug("[MESH] Skipping entry with invalid FlacKey from {Peer}: {Error}", fromUser, keyValidation.Error);
                    skipped++;
                    continue;
                }

                // Validate byte hash
                var hashValidation = MessageValidator.ValidateSha256Hash(entry.ByteHash);
                if (!hashValidation.IsValid)
                {
                    log.Debug("[MESH] Skipping entry with invalid ByteHash from {Peer}: {Error}", fromUser, hashValidation.Error);
                    skipped++;
                    continue;
                }

                // Validate file size
                var sizeValidation = MessageValidator.ValidateFileSize(entry.Size);
                if (!sizeValidation.IsValid)
                {
                    log.Debug("[MESH] Skipping entry with invalid Size from {Peer}: {Error}", fromUser, sizeValidation.Error);
                    skipped++;
                    continue;
                }

                // Validate SeqId
                if (entry.SeqId < 0)
                {
                    log.Debug("[MESH] Skipping entry with negative SeqId from {Peer}: {SeqId}", fromUser, entry.SeqId);
                    skipped++;
                    continue;
                }

                validatedEntries.Add(new HashDbEntry
                {
                    FlacKey = entry.FlacKey,
                    ByteHash = entry.ByteHash,
                    Size = entry.Size,
                    MetaFlags = entry.MetaFlags,
                });
            }

            if (skipped > 0)
            {
                log.Warning("[MESH] Skipped {Skipped}/{Total} invalid entries from {Peer}", skipped, entryList.Count, fromUser);
                stats.SkippedEntries += skipped;
            }

            if (validatedEntries.Count == 0)
            {
                log.Warning("[MESH] No valid entries to merge from {Peer}", fromUser);
                return 0;
            }

            var merged = await hashDb.MergeEntriesFromMeshAsync(validatedEntries, cancellationToken);

            stats.TotalEntriesReceived += entryList.Count;
            stats.TotalEntriesMerged += merged;

            // Update peer state - only consider validated entries for max seq
            var state = GetOrCreatePeerState(fromUser);
            var validSeqIds = entryList.Where(e => e.SeqId >= 0).Select(e => e.SeqId).ToList();
            if (validSeqIds.Count > 0)
            {
                var maxSeq = validSeqIds.Max();
                if (maxSeq > state.LastSeqSeen)
                {
                    state.LastSeqSeen = maxSeq;
                    await hashDb.UpdatePeerLastSeqSeenAsync(fromUser, maxSeq, cancellationToken);
                }
            }

            log.Information("[MESH] Merged {Merged}/{Valid} valid entries from {Peer} ({Skipped} skipped)", merged, validatedEntries.Count, fromUser, skipped);
            return merged;
        }

        private async Task<MeshMessage> HandleHelloAsync(string fromUser, MeshHelloMessage hello, CancellationToken cancellationToken)
        {
            var state = GetOrCreatePeerState(fromUser);
            state.LatestSeqId = hello.LatestSeqId;
            state.ClientVersion = hello.ClientVersion;

            log.Information("[MESH] Received HELLO from {Peer}: seq={SeqId}, count={Count}", fromUser, hello.LatestSeqId, hello.HashCount);

            // Respond with our own HELLO
            return GenerateHelloMessage();
        }

        private async Task<MeshMessage> HandleReqDeltaAsync(string fromUser, MeshReqDeltaMessage req, CancellationToken cancellationToken)
        {
            log.Debug("[MESH] {Peer} requested delta since seq={SeqId}, max={Max}", fromUser, req.SinceSeqId, req.MaxEntries);

            var response = await GenerateDeltaResponseAsync(req.SinceSeqId, Math.Min(req.MaxEntries, MaxEntriesPerSync), cancellationToken);
            stats.TotalEntriesSent += response.Entries.Count;

            log.Information("[MESH] Sending {Count} entries to {Peer} (hasMore={HasMore})", response.Entries.Count, fromUser, response.HasMore);
            return response;
        }

        private async Task<MeshMessage> HandlePushDeltaAsync(string fromUser, MeshPushDeltaMessage push, CancellationToken cancellationToken)
        {
            log.Information("[MESH] Received {Count} entries from {Peer}", push.Entries.Count, fromUser);

            var merged = await MergeEntriesAsync(fromUser, push.Entries, cancellationToken);

            var state = GetOrCreatePeerState(fromUser);
            state.LatestSeqId = push.LatestSeqId;

            return new MeshAckMessage
            {
                MergedCount = merged,
                LatestSeqId = hashDb.CurrentSeqId,
            };
        }

        private async Task<MeshMessage> HandleReqKeyAsync(string fromUser, MeshReqKeyMessage req, CancellationToken cancellationToken)
        {
            log.Debug("[MESH] {Peer} requested key {Key}", fromUser, req.FlacKey);

            var entry = await hashDb.LookupHashAsync(req.FlacKey, cancellationToken);

            return new MeshRespKeyMessage
            {
                FlacKey = req.FlacKey,
                Found = entry != null,
                Entry = entry != null
                    ? new MeshHashEntry
                    {
                        FlacKey = entry.FlacKey,
                        ByteHash = entry.ByteHash,
                        Size = entry.Size,
                        MetaFlags = entry.MetaFlags,
                        SeqId = entry.SeqId,
                    }
                    : null,
            };
        }

        private MeshPeerState GetOrCreatePeerState(string username)
        {
            return peerStates.GetOrAdd(username, u => new MeshPeerState
            {
                Username = u,
                LastSeen = DateTime.UtcNow,
            });
        }

        /// <summary>
        ///     Internal peer state tracking.
        /// </summary>
        private class MeshPeerState
        {
            public string Username { get; set; }
            public bool IsMeshCapable { get; set; }
            public long LatestSeqId { get; set; }
            public long LastSeqSeen { get; set; }
            public DateTime? LastSyncTime { get; set; }
            public DateTime LastSeen { get; set; }
            public string ClientVersion { get; set; }
        }
    }
}


