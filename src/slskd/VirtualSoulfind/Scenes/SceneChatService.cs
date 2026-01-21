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
/// </summary>
public class SceneChatService : ISceneChatService
{
    private readonly ILogger<SceneChatService> logger;
    private readonly IScenePubSubService pubsub;
    private readonly IOptionsMonitor<OptionsModel> optionsMonitor;
    private readonly ConcurrentDictionary<string, List<SceneChatMessage>> messageCache = new();

    public SceneChatService(
        ILogger<SceneChatService> logger,
        IScenePubSubService pubsub,
        IOptionsMonitor<OptionsModel> optionsMonitor)
    {
        this.logger = logger;
        this.pubsub = pubsub;
        this.optionsMonitor = optionsMonitor;

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

        // Create chat message
        var message = new SceneChatMessage
        {
            MessageId = Ulid.NewUlid().ToString(),
            SceneId = sceneId,
            PeerId = "local", // TODO: Use actual peer ID
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
        // TODO: Implement proper serialization (MessagePack or similar)
        return System.Text.Encoding.UTF8.GetBytes(message.Content);
    }

    private SceneChatMessage? DeserializeMessage(byte[] data)
    {
        // TODO: Implement proper deserialization
        return new SceneChatMessage
        {
            MessageId = Ulid.NewUlid().ToString(),
            Content = System.Text.Encoding.UTF8.GetString(data),
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
