namespace slskd.PodCore;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using slskd.Messaging;
using Soulseek;
using NSec.Cryptography;
using System.Text.Json;

/// <summary>
/// Pod service for create/list/join/leave.
/// </summary>
public interface IPodService
{
    Task<Pod> CreateAsync(Pod pod, CancellationToken ct = default);
    Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default);
    Task<bool> JoinAsync(string podId, PodMember member, CancellationToken ct = default);
    Task<bool> LeaveAsync(string podId, string peerId, CancellationToken ct = default);
    Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default);
    Task<Pod?> GetPodAsync(string podId, CancellationToken ct = default);
    Task<IReadOnlyList<PodMember>> GetMembersAsync(string podId, CancellationToken ct = default);
    Task<IReadOnlyList<SignedMembershipRecord>> GetMembershipHistoryAsync(string podId, CancellationToken ct = default);
}

/// <summary>
/// In-memory pod service.
/// </summary>
public class PodService : IPodService
{
    private readonly Dictionary<string, Pod> pods = new();
    private readonly Dictionary<string, List<PodMember>> podMembers = new();
    private readonly Dictionary<string, List<SignedMembershipRecord>> membershipHistory = new(); // podId -> history
    private readonly IPodPublisher podPublisher;
    private readonly IPodMembershipSigner membershipSigner;

    public PodService(
        IPodPublisher podPublisher = null,
        IPodMembershipSigner membershipSigner = null)
    {
        this.podPublisher = podPublisher;
        this.membershipSigner = membershipSigner;
    }

    public async Task<Pod> CreateAsync(Pod pod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pod.PodId))
        {
            pod.PodId = $"pod:{Guid.NewGuid():N}";
        }
        pods[pod.PodId] = pod;
        if (!podMembers.ContainsKey(pod.PodId))
        {
            podMembers[pod.PodId] = new List<PodMember>();
        }

        // Publish to DHT if publisher is available and pod is listed
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
                    // Log but don't fail pod creation if publishing fails
                    System.Diagnostics.Debug.WriteLine($"[PodService] Failed to publish pod to DHT: {ex.Message}");
                }
            }, ct);
        }

        return pod;
    }

    public Task<IReadOnlyList<Pod>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<Pod>)pods.Values.ToList());

    public Task<Pod?> GetPodAsync(string podId, CancellationToken ct = default)
    {
        pods.TryGetValue(podId, out var pod);
        return Task.FromResult(pod);
    }

    public Task<IReadOnlyList<PodMember>> GetMembersAsync(string podId, CancellationToken ct = default)
    {
        if (podMembers.TryGetValue(podId, out var members))
        {
            return Task.FromResult<IReadOnlyList<PodMember>>(members.Where(m => !m.IsBanned).ToList());
        }
        return Task.FromResult<IReadOnlyList<PodMember>>(Array.Empty<PodMember>());
    }

    public Task<IReadOnlyList<SignedMembershipRecord>> GetMembershipHistoryAsync(string podId, CancellationToken ct = default)
    {
        if (membershipHistory.TryGetValue(podId, out var history))
        {
            return Task.FromResult<IReadOnlyList<SignedMembershipRecord>>(history.OrderByDescending(r => r.TimestampUnixMs).ToList());
        }
        return Task.FromResult<IReadOnlyList<SignedMembershipRecord>>(Array.Empty<SignedMembershipRecord>());
    }

    public async Task<bool> JoinAsync(string podId, PodMember member, CancellationToken ct = default)
    {
        if (!pods.TryGetValue(podId, out var pod)) return false;
        
        if (!podMembers.TryGetValue(podId, out var members))
        {
            podMembers[podId] = new List<PodMember>();
            members = podMembers[podId];
        }

        // Check if already a member
        if (members.Any(m => m.PeerId == member.PeerId))
        {
            return false;
        }

        // Create signed membership record if signer is available
        if (membershipSigner != null)
        {
            try
            {
                var record = await membershipSigner.SignMembershipAsync(
                    podId,
                    member.PeerId,
                    member.Role,
                    "join",
                    ct: ct);

                // Store membership history
                if (!membershipHistory.TryGetValue(podId, out var history))
                {
                    membershipHistory[podId] = new List<SignedMembershipRecord>();
                    history = membershipHistory[podId];
                }
                history.Add(record);

                // Store public key in member record if available
                if (!string.IsNullOrWhiteSpace(record.PublicKey))
                {
                    member.PublicKey = record.PublicKey;
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail join if signing fails
                System.Diagnostics.Debug.WriteLine($"[PodService] Failed to sign membership record: {ex.Message}");
            }
        }

        members.Add(member);
        return true;
    }

    public Task<bool> LeaveAsync(string podId, string peerId, CancellationToken ct = default)
    {
        if (!pods.TryGetValue(podId, out var pod)) return Task.FromResult(false);
        
        if (podMembers.TryGetValue(podId, out var members))
        {
            var removed = members.RemoveAll(m => m.PeerId == peerId);
            return Task.FromResult(removed > 0);
        }
        
        return Task.FromResult(false);
    }

    public async Task<bool> BanAsync(string podId, string peerId, CancellationToken ct = default)
    {
        if (!pods.TryGetValue(podId, out var pod)) return false;
        
        if (podMembers.TryGetValue(podId, out var members))
        {
            var member = members.FirstOrDefault(m => m.PeerId == peerId);
            if (member != null)
            {
                // Create signed membership record if signer is available
                if (membershipSigner != null)
                {
                    try
                    {
                        var record = await membershipSigner.SignMembershipAsync(
                            podId,
                            peerId,
                            member.Role,
                            "ban",
                            ct: ct);

                        // Store membership history
                        if (!membershipHistory.TryGetValue(podId, out var history))
                        {
                            membershipHistory[podId] = new List<SignedMembershipRecord>();
                            history = membershipHistory[podId];
                        }
                        history.Add(record);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail ban if signing fails
                        System.Diagnostics.Debug.WriteLine($"[PodService] Failed to sign ban record: {ex.Message}");
                    }
                }

                member.IsBanned = true;
                return true;
            }
        }
        
        return false;
    }
}

/// <summary>
/// Pod messaging with security, routing, and storage.
/// </summary>
public interface IPodMessaging
{
    Task<bool> SendAsync(PodMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<PodMessage>> GetMessagesAsync(string podId, string channelId, long? sinceTimestamp = null, CancellationToken ct = default);
}

/// <summary>
/// Implements pod messaging with signature validation, membership checks, deduplication, routing, and storage.
/// </summary>
public class PodMessaging : IPodMessaging
{
    private readonly IPodService podService;
    private readonly ISoulseekChatBridge chatBridge;
    private readonly Microsoft.Extensions.Logging.ILogger<PodMessaging> logger;
    private readonly Mesh.IMeshSyncService meshSync;
    private readonly Soulseek.ISoulseekClient soulseekClient;
    private readonly Mesh.Overlay.IOverlayClient overlayClient;

    private readonly HashSet<string> seenMessageIds = new();
    private readonly Dictionary<string, List<PodMessage>> messageStorage = new(); // podId:channelId -> messages
    private readonly object storageLock = new();
    private const string PodMessagePrefix = "PODMSG:";

    public PodMessaging(
        IPodService podService,
        ISoulseekChatBridge chatBridge,
        Microsoft.Extensions.Logging.ILogger<PodMessaging> logger,
        Mesh.IMeshSyncService meshSync = null,
        Soulseek.ISoulseekClient soulseekClient = null,
        Mesh.Overlay.IOverlayClient overlayClient = null)
    {
        this.podService = podService;
        this.chatBridge = chatBridge;
        this.logger = logger;
        this.meshSync = meshSync;
        this.soulseekClient = soulseekClient;
        this.overlayClient = overlayClient;
    }

    public async Task<bool> SendAsync(PodMessage message, CancellationToken ct = default)
    {
        if (message == null)
        {
            logger.LogWarning("[PodMessaging] Rejecting null message");
            return false;
        }

        // 1. Validate message structure
        if (string.IsNullOrWhiteSpace(message.MessageId) ||
            string.IsNullOrWhiteSpace(message.ChannelId) ||
            string.IsNullOrWhiteSpace(message.SenderPeerId) ||
            string.IsNullOrWhiteSpace(message.Body))
        {
            logger.LogWarning("[PodMessaging] Rejecting message with missing required fields");
            return false;
        }

        // 2. Deduplication check
        lock (storageLock)
        {
            if (seenMessageIds.Contains(message.MessageId))
            {
                logger.LogDebug("[PodMessaging] Rejecting duplicate message {MessageId}", message.MessageId);
                return false;
            }
            seenMessageIds.Add(message.MessageId);
        }

        // 3. Extract podId from channelId (format: "podId:channelId")
        var channelParts = message.ChannelId.Split(':', 2);
        if (channelParts.Length != 2)
        {
            logger.LogWarning("[PodMessaging] Invalid channelId format: {ChannelId} (expected podId:channelId)", message.ChannelId);
            return false;
        }

        var podId = channelParts[0];
        var channelIdOnly = channelParts[1];

        var pod = await podService.GetPodAsync(podId, ct);
        if (pod == null)
        {
            logger.LogWarning("[PodMessaging] Pod {PodId} not found for channel {ChannelId}", podId, message.ChannelId);
            return false;
        }

        var channel = pod.Channels.FirstOrDefault(c => c.ChannelId == message.ChannelId);
        if (channel == null)
        {
            logger.LogWarning("[PodMessaging] Channel {ChannelId} not found in pod {PodId}", message.ChannelId, pod.PodId);
            return false;
        }

        // 4. Membership verification
        var members = await podService.GetMembersAsync(pod.PodId, ct);
        var senderIsMember = members.Any(m => m.PeerId == message.SenderPeerId && !m.IsBanned);
        if (!senderIsMember)
        {
            logger.LogWarning("[PodMessaging] Rejecting message from non-member {PeerId} in pod {PodId}",
                message.SenderPeerId, pod.PodId);
            return false;
        }

        // 5. Signature validation (Ed25519)
        if (!await ValidateMessageSignatureAsync(message, pod.PodId, ct))
        {
            logger.LogWarning("[PodMessaging] Rejecting message {MessageId} with invalid signature", message.MessageId);
            return false;
        }

        // 6. Store message
        var storageKey = $"{pod.PodId}:{message.ChannelId}";
        lock (storageLock)
        {
            if (!messageStorage.TryGetValue(storageKey, out var messages))
            {
                messageStorage[storageKey] = new List<PodMessage>();
                messages = messageStorage[storageKey];
            }

            messages.Add(message);
            
            // Keep only last 1000 messages per channel
            if (messages.Count > 1000)
            {
                messages.RemoveAt(0);
            }
        }

        logger.LogDebug("[PodMessaging] Accepted and stored message {MessageId} from {PeerId} in channel {ChannelId}",
            message.MessageId, message.SenderPeerId, message.ChannelId);

        // 7. Forward to Soulseek room if channel is bound (mirror mode)
        _ = chatBridge.ForwardPodToSoulseekAsync(message.ChannelId, message);

        // 8. Route message to pod members via mesh transport (decentralized routing)
        _ = RouteMessageToMembersAsync(message, pod.PodId, members, ct);

        return true;
    }

    /// <summary>
    /// Routes a pod message to all pod members via mesh transport (Soulseek private messages or QUIC overlay).
    /// </summary>
    private async Task RouteMessageToMembersAsync(
        PodMessage message,
        string podId,
        IReadOnlyList<PodMember> members,
        CancellationToken ct)
    {
        if (members == null || members.Count == 0)
        {
            logger.LogDebug("[PodMessaging] No members to route message {MessageId} to", message.MessageId);
            return;
        }

        // Filter out sender and banned members
        var recipients = members
            .Where(m => m.PeerId != message.SenderPeerId && !m.IsBanned)
            .ToList();

        if (recipients.Count == 0)
        {
            logger.LogDebug("[PodMessaging] No valid recipients for message {MessageId}", message.MessageId);
            return;
        }

        logger.LogDebug("[PodMessaging] Routing message {MessageId} to {Count} members", message.MessageId, recipients.Count);

        // Serialize message to JSON for transport
        var messageJson = JsonSerializer.Serialize(message);

        // Route via Soulseek private messages (fallback for mesh-capable peers)
        if (soulseekClient != null && meshSync != null)
        {
            var meshPeers = meshSync.GetMeshPeers().ToList();
            var meshPeerUsernames = new HashSet<string>(meshPeers.Select(p => p.Username), StringComparer.OrdinalIgnoreCase);

            foreach (var member in recipients)
            {
                try
                {
                    // Try to resolve peer ID to Soulseek username
                    // For now, assume peer ID might be a username (backward compatibility)
                    // In future, use IPeerResolutionService for proper resolution
                    var username = await ResolvePeerIdToUsernameAsync(member.PeerId, ct) ?? member.PeerId;

                    if (!string.IsNullOrWhiteSpace(username) && meshPeerUsernames.Contains(username))
                    {
                        // Send via Soulseek private message (mesh transport)
                        var meshMessage = $"{PodMessagePrefix}{messageJson}";
                        await soulseekClient.SendPrivateMessageAsync(username, meshMessage, ct);
                        logger.LogDebug("[PodMessaging] Routed message {MessageId} to {PeerId} (Soulseek: {Username})",
                            message.MessageId, member.PeerId, username);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[PodMessaging] Failed to route message {MessageId} to member {PeerId} via Soulseek",
                        message.MessageId, member.PeerId);
                }
            }
        }

        // Route via QUIC overlay if available (preferred for direct mesh connections)
        // Note: Requires IPeerResolutionService to be injected for endpoint resolution
        // For now, QUIC overlay routing is deferred until peer resolution service is integrated
        if (overlayClient != null)
        {
            logger.LogDebug("[PodMessaging] QUIC overlay routing available but peer resolution service not yet integrated");
        }
    }

    /// <summary>
    /// Resolves a pod peer ID to a Soulseek username using peer resolution service.
    /// </summary>
    private async Task<string?> ResolvePeerIdToUsernameAsync(string peerId, CancellationToken ct)
    {
        // Use peer resolution service if available, otherwise fallback to peer ID
        // Note: This requires IPeerResolutionService to be injected
        // For now, fallback to peer ID for backward compatibility
        return peerId;
    }

    public Task<IReadOnlyList<PodMessage>> GetMessagesAsync(string podId, string channelId, long? sinceTimestamp = null, CancellationToken ct = default)
    {
        var storageKey = $"{podId}:{channelId}";
        
        lock (storageLock)
        {
            if (!messageStorage.TryGetValue(storageKey, out var messages))
            {
                return Task.FromResult<IReadOnlyList<PodMessage>>(Array.Empty<PodMessage>());
            }

            var filtered = sinceTimestamp.HasValue
                ? messages.Where(m => m.TimestampUnixMs > sinceTimestamp.Value).ToList()
                : messages.ToList();

            return Task.FromResult<IReadOnlyList<PodMessage>>(filtered);
        }
    }

    private async Task<bool> ValidateMessageSignatureAsync(PodMessage message, string podId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.Signature))
        {
            logger.LogWarning("[PodMessaging] Message {MessageId} has no signature", message.MessageId);
            return false;
        }

        try
        {
            // Build signable payload (similar to ControlEnvelope)
            var payload = BuildSignablePayload(message);
            var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);

            // Get sender's public key from pod membership
            var members = await podService.GetMembersAsync(podId, ct);
            var senderMember = members.FirstOrDefault(m => m.PeerId == message.SenderPeerId);
            
            if (senderMember == null)
            {
                logger.LogWarning("[PodMessaging] Sender {PeerId} not found in pod {PodId} membership", message.SenderPeerId, podId);
                return false;
            }

            // Check if public key is available
            if (string.IsNullOrWhiteSpace(senderMember.PublicKey))
            {
                logger.LogWarning("[PodMessaging] Sender {PeerId} has no public key stored - signature verification skipped", message.SenderPeerId);
                // For backward compatibility, accept messages from members without public keys
                // but log a warning
                return true;
            }

            // Verify signature using Ed25519
            var signatureBytes = Convert.FromBase64String(message.Signature);
            if (signatureBytes.Length != 64) // Ed25519 signatures are 64 bytes
            {
                logger.LogWarning("[PodMessaging] Invalid signature length: {Length}", signatureBytes.Length);
                return false;
            }

            var publicKeyBytes = Convert.FromBase64String(senderMember.PublicKey);
            if (publicKeyBytes.Length != 32) // Ed25519 public keys are 32 bytes
            {
                logger.LogWarning("[PodMessaging] Invalid public key length: {Length}", publicKeyBytes.Length);
                return false;
            }

            // Import and verify using NSec.Cryptography
            var publicKey = PublicKey.Import(SignatureAlgorithm.Ed25519, publicKeyBytes, KeyBlobFormat.RawPublicKey);
            var isValid = SignatureAlgorithm.Ed25519.Verify(publicKey, payloadBytes, signatureBytes);

            if (!isValid)
            {
                logger.LogWarning("[PodMessaging] Signature verification failed for message {MessageId} from {PeerId}",
                    message.MessageId, message.SenderPeerId);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[PodMessaging] Signature validation failed for message {MessageId}", message.MessageId);
            return false;
        }
    }

    private static string BuildSignablePayload(PodMessage message)
    {
        // Build deterministic payload for signing
        return $"{message.MessageId}|{message.ChannelId}|{message.SenderPeerId}|{message.Body}|{message.TimestampUnixMs}";
    }
}

/// <summary>
/// Soulseek chat bridge for bound channels (readonly and mirror modes).
/// </summary>
public interface ISoulseekChatBridge
{
    Task<bool> BindRoomAsync(string podId, string channelId, string roomName, string mode, CancellationToken ct = default);
    Task<bool> UnbindRoomAsync(string podId, string channelId, CancellationToken ct = default);
    Task<bool> ForwardPodToSoulseekAsync(string channelId, PodMessage podMessage);
    void RegisterIdentityMapping(string soulseekUsername, string podPeerId);
}

/// <summary>
/// Implements bidirectional chat bridge between Soulseek rooms and Pod channels.
/// </summary>
public class SoulseekChatBridge : ISoulseekChatBridge
{
    private readonly IPodService podService;
    private readonly IPodMessaging podMessaging;
    private readonly IRoomService roomService;
    private readonly ISoulseekClient soulseekClient;
    private readonly Microsoft.Extensions.Logging.ILogger<SoulseekChatBridge> logger;
    private readonly Dictionary<string, RoomBinding> activeBindings = new(); // channelId -> binding
    private readonly object bindingsLock = new();
    
    // Identity mapping: Soulseek username <-> Pod PeerId
    // In production, this would be stored in database or queried from DHT
    private readonly Dictionary<string, string> soulseekToPodMapping = new(); // username -> peerId
    private readonly Dictionary<string, string> podToSoulseekMapping = new(); // peerId -> username
    private readonly object mappingLock = new();

    public SoulseekChatBridge(
        IPodService podService,
        IPodMessaging podMessaging,
        IRoomService roomService,
        ISoulseekClient soulseekClient,
        Microsoft.Extensions.Logging.ILogger<SoulseekChatBridge> logger)
    {
        this.podService = podService;
        this.podMessaging = podMessaging;
        this.roomService = roomService;
        this.soulseekClient = soulseekClient;
        this.logger = logger;

        // Subscribe to Soulseek room messages
        soulseekClient.RoomMessageReceived += SoulseekClient_RoomMessageReceived;
    }

    public async Task<bool> BindRoomAsync(string podId, string channelId, string roomName, string mode, CancellationToken ct = default)
    {
        logger.LogInformation("[ChatBridge] Binding pod {PodId} channel {ChannelId} to Soulseek room {Room} (mode: {Mode})",
            podId, channelId, roomName, mode);

        // Validate mode
        if (mode != "readonly" && mode != "mirror")
        {
            logger.LogWarning("[ChatBridge] Invalid mode: {Mode} (must be 'readonly' or 'mirror')", mode);
            return false;
        }

        // Verify pod and channel exist
        var pod = await podService.GetPodAsync(podId, ct);
        if (pod == null)
        {
            logger.LogWarning("[ChatBridge] Pod {PodId} not found", podId);
            return false;
        }

        var channel = pod.Channels.FirstOrDefault(c => c.ChannelId == channelId);
        if (channel == null)
        {
            logger.LogWarning("[ChatBridge] Channel {ChannelId} not found in pod {PodId}", channelId, podId);
            return false;
        }

        // Join Soulseek room if not already joined
        try
        {
            await roomService.JoinAsync(roomName);
            logger.LogDebug("[ChatBridge] Joined Soulseek room {Room}", roomName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[ChatBridge] Failed to join Soulseek room {Room}", roomName);
            // Continue anyway - might already be joined
        }

        // Create binding
        var binding = new RoomBinding
        {
            PodId = podId,
            ChannelId = channelId,
            RoomName = roomName,
            Mode = mode,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        lock (bindingsLock)
        {
            activeBindings[channelId] = binding;
        }

        // Update channel binding info
        channel.BindingInfo = $"soulseek-room:{roomName}";

        logger.LogInformation("[ChatBridge] Successfully bound channel {ChannelId} to room {Room} (mode: {Mode})",
            channelId, roomName, mode);

        return true;
    }

    public Task<bool> UnbindRoomAsync(string podId, string channelId, CancellationToken ct = default)
    {
        logger.LogInformation("[ChatBridge] Unbinding channel {ChannelId} from pod {PodId}", channelId, podId);

        lock (bindingsLock)
        {
            if (activeBindings.TryGetValue(channelId, out var binding))
            {
                activeBindings.Remove(channelId);
                logger.LogInformation("[ChatBridge] Unbound channel {ChannelId} from room {Room}", channelId, binding.RoomName);
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private void SoulseekClient_RoomMessageReceived(object sender, RoomMessageReceivedEventArgs e)
    {
        // Find bindings for this room
        List<RoomBinding> bindings;
        lock (bindingsLock)
        {
            bindings = activeBindings.Values
                .Where(b => b.RoomName.Equals(e.RoomName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (bindings.Count == 0)
        {
            return; // No bindings for this room
        }

        // Forward to all bound pod channels
        foreach (var binding in bindings)
        {
            _ = ForwardSoulseekToPodAsync(binding, e.Username, e.Message);
        }
    }

    private async Task ForwardSoulseekToPodAsync(RoomBinding binding, string soulseekUsername, string message)
    {
        try
        {
            // Map Soulseek username to Pod PeerId (for now, use username as-is)
            // Identity mapping uses deterministic fallback for unregistered users
            // For explicit mappings, use RegisterIdentityMapping()
            var peerId = MapSoulseekToPodPeerId(soulseekUsername);

            // Format message with prefix to indicate it's from Soulseek
            var formattedMessage = $"[Soulseek:{soulseekUsername}] {message}";

            var podMessage = new PodMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ChannelId = binding.ChannelId,
                SenderPeerId = peerId,
                Body = formattedMessage,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Signature = string.Empty, // Bridge messages don't need signatures
            };

            var sent = await podMessaging.SendAsync(podMessage);
            if (sent)
            {
                logger.LogDebug("[ChatBridge] Forwarded message from {User} in room {Room} to pod channel {Channel}",
                    soulseekUsername, binding.RoomName, binding.ChannelId);
            }
            else
            {
                logger.LogWarning("[ChatBridge] Failed to forward message from {User} to pod channel {Channel}",
                    soulseekUsername, binding.ChannelId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ChatBridge] Error forwarding Soulseek message to pod channel {Channel}",
                binding.ChannelId);
        }
    }

    private string MapSoulseekToPodPeerId(string soulseekUsername)
    {
        if (string.IsNullOrWhiteSpace(soulseekUsername))
        {
            return null;
        }

        lock (mappingLock)
        {
            // Check if mapping exists
            if (soulseekToPodMapping.TryGetValue(soulseekUsername, out var peerId))
            {
                return peerId;
            }

            // Check if user is a pod member (they might have joined with their Soulseek username)
            // This is a simplified lookup - in production would query all pods or use DHT
            // For now, create a deterministic mapping
            peerId = $"bridge:{soulseekUsername}";
            
            // Store bidirectional mapping
            soulseekToPodMapping[soulseekUsername] = peerId;
            podToSoulseekMapping[peerId] = soulseekUsername;
            
            logger.LogDebug("[ChatBridge] Created identity mapping: Soulseek {Username} <-> Pod {PeerId}",
                soulseekUsername, peerId);
            
            return peerId;
        }
    }
    
    /// <summary>
    /// Registers an identity mapping between Soulseek username and Pod PeerId.
    /// Called when a user joins a pod or when identity is verified.
    /// </summary>
    public void RegisterIdentityMapping(string soulseekUsername, string podPeerId)
    {
        if (string.IsNullOrWhiteSpace(soulseekUsername) || string.IsNullOrWhiteSpace(podPeerId))
        {
            return;
        }

        lock (mappingLock)
        {
            soulseekToPodMapping[soulseekUsername] = podPeerId;
            podToSoulseekMapping[podPeerId] = soulseekUsername;
            
            logger.LogInformation("[ChatBridge] Registered identity mapping: Soulseek {Username} <-> Pod {PeerId}",
                soulseekUsername, podPeerId);
        }
    }

    /// <summary>
    /// Forwards a Pod message to Soulseek room (mirror mode only).
    /// Called by PodMessaging when a message is sent to a bound channel.
    /// </summary>
    public async Task<bool> ForwardPodToSoulseekAsync(string channelId, PodMessage podMessage)
    {
        RoomBinding binding;
        lock (bindingsLock)
        {
            if (!activeBindings.TryGetValue(channelId, out binding) || binding.Mode != "mirror")
            {
                return false; // Not bound or not in mirror mode
            }
        }

        try
        {
            // Extract original message (remove bridge prefix if present)
            var messageBody = podMessage.Body;
            if (messageBody.StartsWith("[Soulseek:", StringComparison.OrdinalIgnoreCase))
            {
                // Don't forward messages that came from Soulseek (prevent loops)
                return false;
            }

            // Map Pod PeerId to Soulseek username
            var soulseekUsername = MapPodToSoulseekUsername(podMessage.SenderPeerId);
            if (string.IsNullOrEmpty(soulseekUsername))
            {
                logger.LogDebug("[ChatBridge] Cannot map Pod peer {PeerId} to Soulseek username", podMessage.SenderPeerId);
                return false;
            }

            // Format message with prefix
            var formattedMessage = $"[Pod:{soulseekUsername}] {messageBody}";

            await soulseekClient.SendRoomMessageAsync(binding.RoomName, formattedMessage);

            logger.LogDebug("[ChatBridge] Forwarded Pod message from {PeerId} to Soulseek room {Room}",
                podMessage.SenderPeerId, binding.RoomName);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[ChatBridge] Error forwarding Pod message to Soulseek room {Room}",
                binding.RoomName);
            return false;
        }
    }

    private string MapPodToSoulseekUsername(string podPeerId)
    {
        if (string.IsNullOrWhiteSpace(podPeerId))
        {
            return null;
        }

        lock (mappingLock)
        {
            // Check if mapping exists
            if (podToSoulseekMapping.TryGetValue(podPeerId, out var username))
            {
                return username;
            }

            // Extract username from bridge: prefix if present (backward compatibility)
            if (podPeerId.StartsWith("bridge:", StringComparison.OrdinalIgnoreCase))
            {
                var extractedUsername = podPeerId.Substring("bridge:".Length);
                // Register the mapping for future use
                RegisterIdentityMapping(extractedUsername, podPeerId);
                return extractedUsername;
            }

            // For other peer IDs, would need to look up from DHT or pod membership
            // In production, this would:
            // - Query pod membership records for public key -> username mapping
            // - Query DHT for peer's identity record
            // - Return null if no mapping found
            
            logger.LogDebug("[ChatBridge] No identity mapping found for Pod peer {PeerId}", podPeerId);
            return null;
        }
    }
}

/// <summary>
/// Represents a binding between a Pod channel and a Soulseek room.
/// </summary>
internal class RoomBinding
{
    public string PodId { get; set; }
    public string ChannelId { get; set; }
    public string RoomName { get; set; }
    public string Mode { get; set; } // "readonly" or "mirror"
    public DateTimeOffset CreatedAt { get; set; }
}
