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
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Serilog;
    using slskd.Capabilities;
    using slskd.Core;
    using slskd.DhtRendezvous.Security;
    using slskd.HashDb;
    using slskd.HashDb.Models;
    using slskd.Mesh.Messages;
    using Soulseek;

    /// <summary>
    ///     Service for epidemic mesh synchronization of hash databases.
    /// </summary>
    public class MeshSyncService : IMeshSyncService, IChunkRequestSender
    {
        /// <summary>Minimum seconds between syncs with same peer.</summary>
        public const int SyncIntervalMinSeconds = 1800; // 30 minutes

        /// <summary>Maximum entries per sync session.</summary>
        public const int MaxEntriesPerSync = 1000;

        /// <summary>Maximum peers to sync with per cycle.</summary>
        public const int MaxPeersPerCycle = 5;

        /// <summary>
        ///     Maximum invalid entries allowed per time window (T-1432).
        /// </summary>
        private const int DefaultMaxInvalidEntriesPerWindow = 50;
        private const int DefaultMaxInvalidMessagesPerWindow = 10;
        private const int DefaultRateLimitWindowMinutes = 5;
        private const int DefaultQuarantineViolationThreshold = 3;

        private readonly IHashDbService hashDb;
        private readonly int _maxInvalidEntriesPerWindow;
        private readonly int _maxInvalidMessagesPerWindow;
        private readonly int _rateLimitWindowMinutes;
        private readonly int _quarantineViolationThreshold;
        private readonly int _quarantineDurationMinutes;
        private readonly IOptions<MeshSyncSecurityOptions> _syncSecurityOptions;
        private readonly ICapabilityService capabilities;
        private readonly IManagedState<State> appState;
        private readonly ISoulseekClient soulseekClient;
        private readonly IMeshMessageSigner messageSigner;
        private readonly Common.Security.PeerReputation? peerReputation;
        private readonly ILogger log = Log.ForContext<MeshSyncService>();

        private readonly ConcurrentDictionary<string, MeshPeerState> peerStates = new(StringComparer.OrdinalIgnoreCase);
        private readonly MeshSyncStats stats = new();
        private readonly SemaphoreSlim syncLock = new(1, 1);
        
        // Pending requests: requestId -> TaskCompletionSource
        private readonly ConcurrentDictionary<string, TaskCompletionSource<MeshRespKeyMessage>> pendingRequests = new();
        // Pending chunk requests for proof-of-possession (T-1434): "{peer}:{flacKey}:{offset}" -> TCS
        private readonly ConcurrentDictionary<string, TaskCompletionSource<MeshRespChunkMessage>> pendingChunkRequests = new();
        private const string MeshMessagePrefix = "MESH:";
        private readonly IFlacKeyToPathResolver _pathResolver;
        private readonly IProofOfPossessionService _proofOfPossession;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MeshSyncService"/> class.
        /// </summary>
        public MeshSyncService(
            IHashDbService hashDb,
            ICapabilityService capabilities,
            ISoulseekClient soulseekClient,
            IMeshMessageSigner messageSigner,
            Common.Security.PeerReputation? peerReputation = null,
            IManagedState<State> appState = null,
            IOptions<MeshSyncSecurityOptions> syncSecurityOptions = null,
            IFlacKeyToPathResolver pathResolver = null,
            IProofOfPossessionService proofOfPossession = null)
        {
            this.hashDb = hashDb;
            this.capabilities = capabilities;
            this.soulseekClient = soulseekClient;
            this.messageSigner = messageSigner;
            this.peerReputation = peerReputation;
            this.appState = appState;
            _syncSecurityOptions = syncSecurityOptions;
            _pathResolver = pathResolver;
            _proofOfPossession = proofOfPossession;
            var o = syncSecurityOptions?.Value;
            _maxInvalidEntriesPerWindow = o?.MaxInvalidEntriesPerWindow ?? DefaultMaxInvalidEntriesPerWindow;
            _maxInvalidMessagesPerWindow = o?.MaxInvalidMessagesPerWindow ?? DefaultMaxInvalidMessagesPerWindow;
            _rateLimitWindowMinutes = o?.RateLimitWindowMinutes ?? DefaultRateLimitWindowMinutes;
            _quarantineViolationThreshold = o?.QuarantineViolationThreshold ?? DefaultQuarantineViolationThreshold;
            _quarantineDurationMinutes = o?.QuarantineDurationMinutes ?? 30;
            
            // Subscribe to private messages for mesh protocol
            if (soulseekClient != null)
            {
                soulseekClient.PrivateMessageReceived += SoulseekClient_PrivateMessageReceived;
            }
        }
        
        private void SoulseekClient_PrivateMessageReceived(object sender, PrivateMessageReceivedEventArgs e)
        {
            // Check if this is a mesh message
            if (!e.Message.StartsWith(MeshMessagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            
            // Extract message type and payload
            var messageText = e.Message.Substring(MeshMessagePrefix.Length);
            var parts = messageText.Split(new[] { ':' }, 2);
            if (parts.Length != 2)
            {
                log.Warning("[MESH] Invalid mesh message format from {Peer}: {Message}", e.Username, e.Message);
                return;
            }
            
            var messageType = parts[0];
            var payload = parts[1];
            
            // Handle response messages
            if (messageType == "RESPKEY")
            {
                try
                {
                    var response = JsonSerializer.Deserialize<MeshRespKeyMessage>(payload);
                    if (response != null && !string.IsNullOrEmpty(response.FlacKey))
                    {
                        // Find pending request by FlacKey (simplified - in production would use request IDs)
                        var requestId = response.FlacKey;
                        if (pendingRequests.TryRemove(requestId, out var tcs))
                        {
                            tcs.SetResult(response);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "[MESH] Failed to deserialize RESPKEY message from {Peer}", e.Username);
                }
            }
            else if (messageType == "REQKEY" || messageType == "REQDELTA" || messageType == "PUSHDELTA" || messageType == "HELLO")
            {
                // Handle incoming requests by routing to HandleMessageAsync
                _ = Task.Run(async () =>
                {
                    try
                    {
                        MeshMessage message = messageType switch
                        {
                            "REQKEY" => JsonSerializer.Deserialize<MeshReqKeyMessage>(payload),
                            "REQDELTA" => JsonSerializer.Deserialize<MeshReqDeltaMessage>(payload),
                            "PUSHDELTA" => JsonSerializer.Deserialize<MeshPushDeltaMessage>(payload),
                            "HELLO" => JsonSerializer.Deserialize<MeshHelloMessage>(payload),
                            "REQCHUNK" => JsonSerializer.Deserialize<MeshReqChunkMessage>(payload),
                            _ => null,
                        };
                        
                        if (message != null)
                        {
                            var response = await HandleMessageAsync(e.Username, message);
                            if (response != null)
                            {
                                // Send response back
                                await SendMeshMessageAsync(e.Username, response);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Warning(ex, "[MESH] Error handling mesh message from {Peer}", e.Username);
                    }
                });
            }
        }
        
        private async Task SendMeshMessageAsync(string username, MeshMessage message)
        {
            if (soulseekClient == null)
            {
                log.Warning("[MESH] Cannot send mesh message - Soulseek client not available");
                return;
            }
            
            try
            {
                // SECURITY: Sign message before sending (T-1430)
                var signedMessage = messageSigner.SignMessage(message);
                
                var messageType = message.Type switch
                {
                    MeshMessageType.RespKey => "RESPKEY",
                    MeshMessageType.ReqKey => "REQKEY",
                    MeshMessageType.ReqDelta => "REQDELTA",
                    MeshMessageType.PushDelta => "PUSHDELTA",
                    MeshMessageType.Hello => "HELLO",
                    MeshMessageType.Ack => "ACK",
                    MeshMessageType.ReqChunk => "REQCHUNK",
                    MeshMessageType.RespChunk => "RESPCHUNK",
                    _ => "UNKNOWN",
                };
                
                var payload = JsonSerializer.Serialize(signedMessage);
                var messageText = $"{MeshMessagePrefix}{messageType}:{payload}";
                
                await soulseekClient.SendPrivateMessageAsync(username, messageText);
                log.Debug("[MESH] Sent signed {Type} message to {Peer}", messageType, username);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[MESH] Failed to send mesh message to {Peer}", username);
            }
        }

        /// <inheritdoc cref="IChunkRequestSender.RequestChunkAsync"/>
        public async Task<(string? DataBase64, bool Success)> RequestChunkAsync(string peer, string flacKey, long offset, int length, CancellationToken cancellationToken = default)
        {
            if (soulseekClient == null)
            {
                log.Debug("[MESH] Cannot request chunk - Soulseek client not available");
                return (null, false);
            }

            var req = new MeshReqChunkMessage { FlacKey = flacKey, Offset = offset, Length = length };
            var key = $"{peer}:{flacKey}:{offset}";
            var tcs = new TaskCompletionSource<MeshRespChunkMessage>();
            if (!pendingChunkRequests.TryAdd(key, tcs))
            {
                log.Warning("[MESH] Duplicate chunk request for {Key} from {Peer}", key, peer);
                return (null, false);
            }

            try
            {
                await SendMeshMessageAsync(peer, req);
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
                var resp = await tcs.Task.WaitAsync(timeoutCts.Token);
                return (resp?.DataBase64, resp?.Success ?? false);
            }
            catch (OperationCanceledException)
            {
                log.Debug("[MESH] Chunk request timeout for {Key} from {Peer}", key, peer);
                pendingChunkRequests.TryRemove(key, out _);
                return (null, false);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[MESH] Chunk request failed for {Key} from {Peer}", key, peer);
                pendingChunkRequests.TryRemove(key, out _);
                return (null, false);
            }
        }

        /// <inheritdoc/>
        public MeshSyncStats Stats
        {
            get
            {
                stats.CurrentSeqId = hashDb.CurrentSeqId;
                stats.KnownMeshPeers = peerStates.Count(p => p.Value.IsMeshCapable);
                
                // SECURITY: Update security metrics (T-1436)
                stats.QuarantinedPeers = peerStates.Count(p => 
                {
                    var state = p.Value;
                    lock (state)
                    {
                        return state.QuarantinedUntil.HasValue && state.QuarantinedUntil.Value > DateTime.UtcNow;
                    }
                });
                
                stats.Warnings = ComputeWarnings();
                return stats;
            }
        }
        
        /// <summary>Builds security warning messages when configured thresholds are exceeded.</summary>
        private List<string> ComputeWarnings()
        {
            var list = new List<string>();
            var o = _syncSecurityOptions?.Value;
            if (o == null) return list;
            if (o.AlertThresholdSignatureFailures > 0 && stats.SignatureVerificationFailures >= o.AlertThresholdSignatureFailures)
                list.Add($"Signature verification failures ({stats.SignatureVerificationFailures}) >= {o.AlertThresholdSignatureFailures}");
            if (o.AlertThresholdRateLimitViolations > 0 && stats.RateLimitViolations >= o.AlertThresholdRateLimitViolations)
                list.Add($"Rate limit violations ({stats.RateLimitViolations}) >= {o.AlertThresholdRateLimitViolations}");
            if (o.AlertThresholdQuarantineEvents > 0 && stats.QuarantineEvents >= o.AlertThresholdQuarantineEvents)
                list.Add($"Quarantine events ({stats.QuarantineEvents}) >= {o.AlertThresholdQuarantineEvents}");
            return list;
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

            // SECURITY: Check if peer is quarantined (T-1433)
            if (IsQuarantined(fromUser))
            {
                log.Warning("[MESH] Rejecting message from quarantined peer {Peer}", fromUser);
                stats.RejectedMessages++;
                // Note: QuarantinedPeers count is updated in Stats getter
                return null;
            }

            // SECURITY: Verify message signature (T-1430)
            if (!messageSigner.VerifyMessage(message))
            {
                log.Warning("[MESH] Rejecting message with invalid signature from {Peer}", fromUser);
                stats.RejectedMessages++;
                stats.SignatureVerificationFailures++; // T-1436
                return null;
            }

            // SECURITY: Validate message-specific constraints before processing
            var messageValidation = ValidateIncomingMessage(fromUser, message);
            if (!messageValidation.IsValid)
            {
                log.Warning("[MESH] Rejecting invalid message from {Peer}: {Error}", fromUser, messageValidation.Error);
                stats.RejectedMessages++;
                
                // SECURITY: Track invalid message for rate limiting (T-1432)
                RecordInvalidMessage(fromUser);
                
                // Check if peer exceeded rate limit
                if (IsRateLimited(fromUser, isMessage: true))
                {
                    log.Warning("[MESH] Peer {Peer} exceeded invalid message rate limit, rejecting", fromUser);
                    stats.RateLimitViolations++; // T-1436
                    
                    if (peerReputation != null)
                    {
                        peerReputation.RecordProtocolViolation(fromUser, "Exceeded invalid message rate limit");
                    }
                    
                    // SECURITY: Record rate limit violation and check for quarantine (T-1433)
                    RecordRateLimitViolation(fromUser);
                    if (ShouldQuarantine(fromUser))
                    {
                        QuarantinePeer(fromUser, "Exceeded invalid message rate limit multiple times");
                    }
                    
                    return null;
                }
                
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
                MeshMessageType.ReqChunk => await HandleReqChunkAsync(fromUser, (MeshReqChunkMessage)message, cancellationToken),
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

                case MeshReqChunkMessage reqChunk:
                    var chunkKeyValidation = MessageValidator.ValidateFlacKey(reqChunk.FlacKey);
                    if (!chunkKeyValidation.IsValid)
                    {
                        return chunkKeyValidation;
                    }
                    if (reqChunk.Offset < 0)
                    {
                        return ValidationResult.Fail($"Invalid Offset: {reqChunk.Offset}");
                    }
                    if (reqChunk.Length <= 0 || reqChunk.Length > 32768)
                    {
                        return ValidationResult.Fail($"Invalid Length: {reqChunk.Length} (max 32768)");
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

            // T-1435: Use ConsensusMinPeers and ConsensusMinAgreements from options (fallback 5, 3)
            var minPeers = _syncSecurityOptions?.Value?.ConsensusMinPeers ?? 5;
            var minAgreements = _syncSecurityOptions?.Value?.ConsensusMinAgreements ?? 3;

            var meshPeers = GetMeshPeers()
                .Where(p => p.LastSeen > DateTime.UtcNow.AddHours(-24)) // Only query recently seen peers
                .OrderByDescending(p => p.LastSyncTime ?? DateTime.MinValue) // Prefer recently synced peers
                .Take(minPeers)
                .ToList();

            if (meshPeers.Count == 0)
            {
                log.Debug("[MESH] No mesh peers available for hash lookup: {Key}", flacKey);
                return null;
            }

            log.Debug("[MESH] Querying {Count} mesh peers for hash: {Key} (consensus: minAgreements={Min})", meshPeers.Count, flacKey, minAgreements);

            // Query peers in parallel
            var queryTasks = meshPeers.Select(async peer =>
            {
                try
                {
                    return await QueryPeerForHashAsync(peer.Username, flacKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    log.Debug(ex, "[MESH] Failed to query peer {Peer} for hash {Key}", peer.Username, flacKey);
                    return null;
                }
            });

            var results = await Task.WhenAll(queryTasks);
            // T-1435: Group by (FlacKey, ByteHash, Size); only accept if >= ConsensusMinAgreements
            var groups = results
                .Where(r => r != null && !string.IsNullOrEmpty(r.ByteHash))
                .GroupBy(r => (r.FlacKey ?? string.Empty, r.ByteHash ?? string.Empty, r.Size))
                .ToList();
            var agreed = groups.FirstOrDefault(g => g.Count() >= minAgreements);
            var foundEntry = agreed?.FirstOrDefault();

            if (foundEntry != null)
            {
                log.Debug("[MESH] Found hash {Key} via mesh query", flacKey);
                // Optionally cache the result locally
                if (foundEntry.ByteHash != null && foundEntry.Size > 0)
                {
                    try
                    {
                        await hashDb.StoreHashAsync(new HashDbEntry
                        {
                            FlacKey = foundEntry.FlacKey,
                            ByteHash = foundEntry.ByteHash,
                            Size = foundEntry.Size,
                            MetaFlags = foundEntry.MetaFlags,
                        }, cancellationToken);
                        log.Debug("[MESH] Cached mesh query result for {Key}", flacKey);
                    }
                    catch (Exception ex)
                    {
                        log.Debug(ex, "[MESH] Failed to cache mesh query result for {Key}", flacKey);
                    }
                }
            }
            else
            {
                log.Debug("[MESH] Hash {Key} not found in any queried mesh peer", flacKey);
            }

            return foundEntry;
        }

        /// <summary>
        ///     Queries a specific peer for a hash entry. Overridable for tests.
        /// </summary>
        protected virtual async Task<MeshHashEntry> QueryPeerForHashAsync(string username, string flacKey, CancellationToken cancellationToken)
        {
            // Check if peer supports mesh sync
            var peerCaps = capabilities.GetPeerCapabilities(username);
            if (peerCaps == null || !peerCaps.CanMeshSync)
            {
                log.Debug("[MESH] Peer {Peer} does not support mesh sync", username);
                return null;
            }

            if (soulseekClient == null)
            {
                log.Debug("[MESH] Cannot query peer {Peer} - Soulseek client not available", username);
                return null;
            }

            // Create request message
            var request = new MeshReqKeyMessage
            {
                FlacKey = flacKey,
            };

            // Create TaskCompletionSource to wait for response
            var tcs = new TaskCompletionSource<MeshRespKeyMessage>();
            var requestId = flacKey; // Use FlacKey as request ID for simplicity
            
            // Register pending request
            if (!pendingRequests.TryAdd(requestId, tcs))
            {
                log.Warning("[MESH] Duplicate request for key {Key} to peer {Peer}", flacKey, username);
                return null;
            }

            try
            {
                // Send request message
                await SendMeshMessageAsync(username, request);
                
                log.Debug("[MESH] Sent REQKEY message to {Peer} for key {Key}", username, flacKey);

                // Wait for response with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(10)); // 10 second timeout
                
                var response = await tcs.Task.WaitAsync(timeoutCts.Token);
                
                if (response.Found && response.Entry != null)
                {
                    log.Debug("[MESH] Peer {Peer} found key {Key}", username, flacKey);
                    return response.Entry;
                }
                else
                {
                    log.Debug("[MESH] Peer {Peer} did not have key {Key}", username, flacKey);
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                log.Debug("[MESH] Request timeout for key {Key} from peer {Peer}", flacKey, username);
                pendingRequests.TryRemove(requestId, out _);
                return null;
            }
            catch (Exception ex)
            {
                log.Warning(ex, "[MESH] Error querying peer {Peer} for key {Key}", username, flacKey);
                pendingRequests.TryRemove(requestId, out _);
                return null;
            }
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
            
            // Get actual username from application state, fallback to "slskdn" if not available
            var currentState = this.appState?.CurrentValue;
            var username = currentState?.User?.Username;
            if (string.IsNullOrWhiteSpace(username))
            {
                username = "slskdn"; // Fallback if state not available
                log.Debug("[MESH] Using fallback username 'slskdn' (state not available)");
            }

            return new MeshHelloMessage
            {
                ClientId = username,
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
            // SECURITY: Check if peer is quarantined (T-1433)
            if (IsQuarantined(fromUser))
            {
                log.Warning("[MESH] Rejecting entries from quarantined peer {Peer}", fromUser);
                stats.RejectedMessages++;
                return 0;
            }

            // SECURITY: Check peer reputation before processing entries (T-1431)
            if (peerReputation != null && peerReputation.IsUntrusted(fromUser))
            {
                var score = peerReputation.GetScore(fromUser);
                log.Warning("[MESH] Rejecting entries from untrusted peer {Peer} (score={Score})", fromUser, score);
                stats.RejectedMessages++;
                stats.ReputationBasedRejections++; // T-1436
                
                // Record protocol violation for attempting to sync with low reputation
                peerReputation.RecordProtocolViolation(fromUser, "Attempted mesh sync with untrusted reputation");
                return 0;
            }

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
                
                // SECURITY: Track invalid entries for rate limiting (T-1432)
                RecordInvalidEntries(fromUser, skipped);
                
                // SECURITY: Check if peer exceeded rate limit
                if (IsRateLimited(fromUser, isMessage: false))
                {
                    log.Warning("[MESH] Peer {Peer} exceeded invalid entry rate limit, rejecting remaining entries", fromUser);
                    stats.RateLimitViolations++; // T-1436
                    
                    if (peerReputation != null)
                    {
                        peerReputation.RecordProtocolViolation(fromUser, $"Exceeded invalid entry rate limit ({skipped} invalid entries)");
                    }
                    
                    // SECURITY: Record rate limit violation and check for quarantine (T-1433)
                    RecordRateLimitViolation(fromUser);
                    if (ShouldQuarantine(fromUser))
                    {
                        QuarantinePeer(fromUser, "Exceeded invalid entry rate limit multiple times");
                    }
                    
                    return 0;
                }
                
                // SECURITY: Record malformed message for peers sending invalid entries (T-1431)
                if (peerReputation != null && skipped > entryList.Count / 2)
                {
                    // If more than half the entries are invalid, record as malformed message
                    peerReputation.RecordMalformedMessage(fromUser);
                }
            }

            if (validatedEntries.Count == 0)
            {
                log.Warning("[MESH] No valid entries to merge from {Peer}", fromUser);
                return 0;
            }

            // T-1434: Proof-of-possession when enabled
            if (_syncSecurityOptions?.Value?.ProofOfPossessionEnabled == true && _proofOfPossession != null)
            {
                var popCache = new Dictionary<string, bool>(StringComparer.Ordinal);
                var toMerge = new List<HashDbEntry>();
                foreach (var entry in validatedEntries)
                {
                    var key = $"{fromUser}:{entry.FlacKey}";
                    if (!popCache.TryGetValue(key, out var ok))
                    {
                        ok = await _proofOfPossession.VerifyAsync(fromUser, entry.FlacKey, entry.ByteHash, entry.Size, this, cancellationToken);
                        popCache[key] = ok;
                        if (!ok)
                        {
                            stats.ProofOfPossessionFailures++;
                            log.Debug("[MESH] Proof-of-possession failed for {Key} from {Peer}", entry.FlacKey, fromUser);
                        }
                    }
                    if (ok)
                    {
                        toMerge.Add(entry);
                    }
                }
                validatedEntries = toMerge;
                if (validatedEntries.Count == 0)
                {
                    log.Warning("[MESH] No entries passed proof-of-possession from {Peer}", fromUser);
                    return 0;
                }
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

        private async Task<MeshMessage> HandleReqChunkAsync(string fromUser, MeshReqChunkMessage req, CancellationToken cancellationToken)
        {
            log.Debug("[MESH] {Peer} requested chunk {Key} @ {Offset} len={Length}", fromUser, req.FlacKey, req.Offset, req.Length);

            string path = _pathResolver != null ? await _pathResolver.TryGetFilePathAsync(req.FlacKey, cancellationToken) : null;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return new MeshRespChunkMessage { FlacKey = req.FlacKey, Offset = req.Offset, DataBase64 = null, Success = false };
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (req.Offset >= fs.Length)
                {
                    return new MeshRespChunkMessage { FlacKey = req.FlacKey, Offset = req.Offset, DataBase64 = null, Success = false };
                }
                var toRead = (int)Math.Min(req.Length, fs.Length - req.Offset);
                fs.Seek(req.Offset, SeekOrigin.Begin);
                var buf = new byte[toRead];
                var read = await fs.ReadAsync(buf, 0, toRead, cancellationToken);
                if (read <= 0)
                {
                    return new MeshRespChunkMessage { FlacKey = req.FlacKey, Offset = req.Offset, DataBase64 = null, Success = false };
                }
                var b64 = read < buf.Length ? Convert.ToBase64String(buf.AsSpan(0, read)) : Convert.ToBase64String(buf);
                return new MeshRespChunkMessage { FlacKey = req.FlacKey, Offset = req.Offset, DataBase64 = b64, Success = true };
            }
            catch (Exception ex)
            {
                log.Debug(ex, "[MESH] Failed to read chunk for {Key} from {Peer}", req.FlacKey, fromUser);
                return new MeshRespChunkMessage { FlacKey = req.FlacKey, Offset = req.Offset, DataBase64 = null, Success = false };
            }
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
        ///     Records invalid entries for rate limiting (T-1432).
        /// </summary>
        private void RecordInvalidEntries(string username, int count)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                var now = DateTime.UtcNow;
                var cutoff = now.AddMinutes(-_rateLimitWindowMinutes);
                
                // Clean up old timestamps outside the window
                while (state.InvalidEntryTimestamps.Count > 0 && state.InvalidEntryTimestamps.Peek() < cutoff)
                {
                    state.InvalidEntryTimestamps.Dequeue();
                    state.InvalidEntryCount--;
                }
                
                // Add new invalid entry timestamps
                for (int i = 0; i < count; i++)
                {
                    state.InvalidEntryTimestamps.Enqueue(now);
                    state.InvalidEntryCount++;
                }
            }
        }
        
        /// <summary>
        ///     Records invalid message for rate limiting (T-1432).
        /// </summary>
        private void RecordInvalidMessage(string username)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                var now = DateTime.UtcNow;
                var cutoff = now.AddMinutes(-_rateLimitWindowMinutes);
                
                // Clean up old timestamps outside the window
                while (state.InvalidMessageTimestamps.Count > 0 && state.InvalidMessageTimestamps.Peek() < cutoff)
                {
                    state.InvalidMessageTimestamps.Dequeue();
                    state.InvalidMessageCount--;
                }
                
                // Add new invalid message timestamp
                state.InvalidMessageTimestamps.Enqueue(now);
                state.InvalidMessageCount++;
            }
        }
        
        /// <summary>
        ///     Checks if a peer has exceeded the rate limit (T-1432).
        /// </summary>
        private bool IsRateLimited(string username, bool isMessage)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                var now = DateTime.UtcNow;
                var cutoff = now.AddMinutes(-_rateLimitWindowMinutes);
                
                if (isMessage)
                {
                    // Clean up old message timestamps
                    while (state.InvalidMessageTimestamps.Count > 0 && state.InvalidMessageTimestamps.Peek() < cutoff)
                    {
                        state.InvalidMessageTimestamps.Dequeue();
                        state.InvalidMessageCount--;
                    }
                    
                    return state.InvalidMessageCount >= _maxInvalidMessagesPerWindow;
                }
                else
                {
                    // Clean up old entry timestamps
                    while (state.InvalidEntryTimestamps.Count > 0 && state.InvalidEntryTimestamps.Peek() < cutoff)
                    {
                        state.InvalidEntryTimestamps.Dequeue();
                        state.InvalidEntryCount--;
                    }
                    
                    return state.InvalidEntryCount >= _maxInvalidEntriesPerWindow;
                }
            }
        }
        
        /// <summary>
        ///     Records a rate limit violation for quarantine tracking (T-1433).
        /// </summary>
        private void RecordRateLimitViolation(string username)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                state.RateLimitViolationCount++;
                state.LastRateLimitViolation = DateTime.UtcNow;
                log.Debug("[MESH] Peer {Peer} rate limit violation count: {Count}", username, state.RateLimitViolationCount);
            }
        }
        
        /// <summary>
        ///     Checks if a peer should be quarantined (T-1433).
        /// </summary>
        private bool ShouldQuarantine(string username)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                // Reset violation count if last violation was more than the rate limit window ago
                if (state.LastRateLimitViolation.HasValue)
                {
                    var timeSinceLastViolation = DateTime.UtcNow - state.LastRateLimitViolation.Value;
                    if (timeSinceLastViolation.TotalMinutes > _rateLimitWindowMinutes)
                    {
                        // Reset count if violations are old
                        state.RateLimitViolationCount = 1;
                        return false;
                    }
                }
                
                return state.RateLimitViolationCount >= _quarantineViolationThreshold;
            }
        }
        
        /// <summary>
        ///     Quarantines a peer for the configured duration (T-1433).
        /// </summary>
        private void QuarantinePeer(string username, string reason)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                if (state.QuarantinedUntil.HasValue && state.QuarantinedUntil.Value > DateTime.UtcNow)
                {
                    // Already quarantined, extend duration
                    state.QuarantinedUntil = DateTime.UtcNow.AddMinutes(_quarantineDurationMinutes);
                    log.Warning("[MESH] Extended quarantine for peer {Peer} until {Until} (reason: {Reason})", 
                        username, state.QuarantinedUntil, reason);
                }
                else
                {
                    // New quarantine
                    state.QuarantinedUntil = DateTime.UtcNow.AddMinutes(_quarantineDurationMinutes);
                    log.Warning("[MESH] Quarantined peer {Peer} until {Until} (reason: {Reason}, violations: {Count})", 
                        username, state.QuarantinedUntil, reason, state.RateLimitViolationCount);
                    
                    stats.QuarantineEvents++; // T-1436
                    
                    // Reset violation count after quarantine
                    state.RateLimitViolationCount = 0;
                }
            }
        }
        
        /// <summary>
        ///     Checks if a peer is currently quarantined (T-1433).
        /// </summary>
        private bool IsQuarantined(string username)
        {
            var state = GetOrCreatePeerState(username);
            lock (state)
            {
                if (!state.QuarantinedUntil.HasValue)
                {
                    return false;
                }
                
                // Check if quarantine has expired
                if (state.QuarantinedUntil.Value <= DateTime.UtcNow)
                {
                    // Quarantine expired, lift it
                    state.QuarantinedUntil = null;
                    log.Information("[MESH] Quarantine lifted for peer {Peer}", username);
                    return false;
                }
                
                return true;
            }
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
            
            // SECURITY: Rate limiting for invalid entries/messages (T-1432)
            public readonly Queue<DateTime> InvalidEntryTimestamps = new();
            public readonly Queue<DateTime> InvalidMessageTimestamps = new();
            public int InvalidEntryCount { get; set; }
            public int InvalidMessageCount { get; set; }
            
            // SECURITY: Quarantine tracking (T-1433)
            public DateTime? QuarantinedUntil { get; set; }
            public int RateLimitViolationCount { get; set; }
            public DateTime? LastRateLimitViolation { get; set; }
        }
    }
}


