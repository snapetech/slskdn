// <copyright file="SceneChatService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.VirtualSoulfind.Scenes;

using Microsoft.Extensions.Options;
using slskd;
using OptionsModel = slskd.Options;
using System.Collections.Concurrent;

/// <summary>
/// Interface for scene chat.
/// </summary>
public interface ISceneChatService
{
    /// <summary>
    /// Send a chat message to a scene.
    /// </summary>
    Task SendMessageAsync(string sceneId, string content, CancellationToken ct = default);
    
    /// <summary>
    /// Get recent chat messages for a scene.
    /// </summary>
    Task<List<SceneChatMessage>> GetMessagesAsync(
        string sceneId,
        int limit = 100,
        CancellationToken ct = default);
    
    /// <summary>
    /// Event fired when a new message is received.
    /// </summary>
    event EventHandler<SceneChatMessage> MessageReceived;
}

/// <summary>
/// Optional scene chat service (overlay pubsub-based).
/// Phase 6C: T-819 - Real implementation with MessagePack serialization.
/// </summary>
public class SceneChatService : ISceneChatService
{
    private readonly ILogger<SceneChatService> logger;
    private readonly IScenePubSubService pubsub;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private readonly Identity.IProfileService? profileService;
    private readonly ConcurrentDictionary<string, List<SceneChatMessage>> messageCache = new();

    public SceneChatService(
        ILogger<SceneChatService> logger,
        IScenePubSubService pubsub,
        IOptionsMonitor<OptionsModel> optionsMonitor,
        Identity.IProfileService? profileService = null)
    {
        this.logger = logger;
        this.pubsub = pubsub;
        this.optionsMonitor = optionsMonitor;
        this.profileService = profileService;

        // Subscribe to pubsub messages
        pubsub.MessageReceived += OnPubSubMessageReceived;
    }

    public event EventHandler<SceneChatMessage>? MessageReceived;

    public async Task SendMessageAsync(string sceneId, string content, CancellationToken ct)
    {
        var options = optionsMonitor.CurrentValue;
        if (options.VirtualSoulfind?.Scenes?.EnableChat != true)
        {
            throw new InvalidOperationException("Scene chat is not enabled");
        }

        logger.LogDebug("[VSF-SCENE-CHAT] Sending message to scene {SceneId}: {Content}",
            sceneId, content);

        // Get actual peer ID
        string peerId = "local";
        if (profileService != null)
        {
            try
            {
                var profile = await profileService.GetMyProfileAsync(ct);
                peerId = profile.PeerId;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-SCENE-CHAT] Failed to get peer ID, using placeholder");
            }
        }

        // Create chat message
        var message = new SceneChatMessage
        {
            MessageId = Ulid.NewUlid().ToString(),
            SceneId = sceneId,
            PeerId = peerId,
            Timestamp = DateTimeOffset.UtcNow,
            Content = content
        };

        // Serialize and publish via pubsub
        var data = SerializeMessage(message);
        await pubsub.PublishAsync(sceneId, data, ct);

        // Store locally
        StoreMessage(message);

        logger.LogInformation("[VSF-SCENE-CHAT] Sent message {MessageId} to scene {SceneId}",
            message.MessageId, sceneId);
    }

    public Task<List<SceneChatMessage>> GetMessagesAsync(
        string sceneId,
        int limit,
        CancellationToken ct)
    {
        logger.LogDebug("[VSF-SCENE-CHAT] Getting messages for scene {SceneId}, limit={Limit}",
            sceneId, limit);

        if (messageCache.TryGetValue(sceneId, out var messages))
        {
            return Task.FromResult(messages.TakeLast(limit).ToList());
        }

        return Task.FromResult(new List<SceneChatMessage>());
    }

    private void OnPubSubMessageReceived(object? sender, SceneMessageReceivedEventArgs e)
    {
        try
        {
            var message = DeserializeMessage(e.Message);
            if (message != null)
            {
                StoreMessage(message);
                MessageReceived?.Invoke(this, message);

                logger.LogDebug("[VSF-SCENE-CHAT] Received message {MessageId} in scene {SceneId}",
                    message.MessageId, message.SceneId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[VSF-SCENE-CHAT] Failed to process pubsub message");
        }
    }

    private void StoreMessage(SceneChatMessage message)
    {
        var messages = messageCache.GetOrAdd(message.SceneId, _ => new List<SceneChatMessage>());

        lock (messages)
        {
            messages.Add(message);

            // Keep only last 1000 messages per scene
            if (messages.Count > 1000)
            {
                messages.RemoveAt(0);
            }
        }
    }

    private byte[] SerializeMessage(SceneChatMessage message)
    {
        // Phase 6C: T-819 - MessagePack serialization for scene chat
        try
        {
            return MessagePack.MessagePackSerializer.Serialize(message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-SCENE-CHAT] Failed to serialize message, falling back to UTF-8");
            return System.Text.Encoding.UTF8.GetBytes(message.Content);
        }
    }

    private SceneChatMessage? DeserializeMessage(byte[] data)
    {
        // Phase 6C: T-819 - MessagePack deserialization for scene chat
        try
        {
            return MessagePack.MessagePackSerializer.Deserialize<SceneChatMessage>(data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[VSF-SCENE-CHAT] Failed to deserialize message, falling back to simple parsing");
            // Fallback: try to parse as simple text
            try
            {
                var content = System.Text.Encoding.UTF8.GetString(data);
                return new SceneChatMessage
                {
                    MessageId = Ulid.NewUlid().ToString(),
                    Content = content,
                    Timestamp = DateTimeOffset.UtcNow
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
