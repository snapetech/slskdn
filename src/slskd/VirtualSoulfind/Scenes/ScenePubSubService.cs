// <copyright file="ScenePubSubService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace slskd.VirtualSoulfind.Scenes;

/// <summary>
/// Interface for overlay pubsub (scene gossip).
/// </summary>
public interface IScenePubSubService
{
    /// <summary>
    /// Subscribe to a scene's pubsub topic.
    /// </summary>
    Task SubscribeAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Unsubscribe from a scene's pubsub topic.
    /// </summary>
    Task UnsubscribeAsync(string sceneId, CancellationToken ct = default);
    
    /// <summary>
    /// Publish a message to a scene.
    /// </summary>
    Task PublishAsync(string sceneId, byte[] message, CancellationToken ct = default);
    
    /// <summary>
    /// Event fired when a message is received.
    /// </summary>
    event EventHandler<SceneMessageReceivedEventArgs> MessageReceived;
}

/// <summary>
/// Scene message received event args.
/// </summary>
public class SceneMessageReceivedEventArgs : EventArgs
{
    public string SceneId { get; set; } = string.Empty;
    public string PeerId { get; set; } = string.Empty;
    public byte[] Message { get; set; } = Array.Empty<byte>();
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Overlay pubsub service for scene gossip.
/// Phase 6C: T-816 - DHT-based pubsub implementation (can be enhanced with real overlay pubsub later).
/// </summary>
public class ScenePubSubService : IScenePubSubService
{
    private readonly ILogger<ScenePubSubService> logger;
    private readonly VirtualSoulfind.ShadowIndex.IDhtClient dht;
    private readonly ConcurrentDictionary<string, DateTimeOffset> subscriptions = new();
    private readonly System.Threading.Timer? pollTimer;

    public ScenePubSubService(
        ILogger<ScenePubSubService> logger,
        VirtualSoulfind.ShadowIndex.IDhtClient dht)
    {
        this.logger = logger;
        this.dht = dht;

        // Start polling timer for subscribed scenes (every 30 seconds)
        pollTimer = new System.Threading.Timer(
            async _ => await PollSubscribedScenesAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    public event EventHandler<SceneMessageReceivedEventArgs>? MessageReceived;

    public Task SubscribeAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-PUBSUB] Subscribing to scene {SceneId}", sceneId);

        // Phase 6C: T-816 - Track subscription (polling will check DHT for messages)
        subscriptions[sceneId] = DateTimeOffset.UtcNow;

        logger.LogInformation("[VSF-PUBSUB] Subscribed to scene {SceneId}", sceneId);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-PUBSUB] Unsubscribing from scene {SceneId}", sceneId);

        subscriptions.TryRemove(sceneId, out _);

        logger.LogInformation("[VSF-PUBSUB] Unsubscribed from scene {SceneId}", sceneId);
        return Task.CompletedTask;
    }

    public async Task PublishAsync(string sceneId, byte[] message, CancellationToken ct)
    {
        logger.LogDebug("[VSF-PUBSUB] Publishing message to scene {SceneId}: {Size} bytes",
            sceneId, message.Length);

        // Phase 6C: T-816 - Store message in DHT with scene-specific key
        var key = VirtualSoulfind.ShadowIndex.DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}:{Ulid.NewUlid()}");
        
        // Store with short TTL (5 minutes) - messages are ephemeral
        await dht.PutAsync(key, message, ttlSeconds: 300, ct);

        logger.LogInformation("[VSF-PUBSUB] Published message to scene {SceneId}", sceneId);
    }

    private async Task PollSubscribedScenesAsync()
    {
        if (subscriptions.IsEmpty)
        {
            return;
        }

        foreach (var (sceneId, subscribedAt) in subscriptions.ToList())
        {
            try
            {
                // Query DHT for recent messages in this scene
                var sceneKey = VirtualSoulfind.ShadowIndex.DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}");
                var messages = await dht.GetMultipleAsync(sceneKey, CancellationToken.None);

                foreach (var messageData in messages)
                {
                    // Fire event for received message
                    OnMessageReceived(new SceneMessageReceivedEventArgs
                    {
                        SceneId = sceneId,
                        PeerId = "unknown", // Would need to extract from message
                        Message = messageData,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-PUBSUB] Failed to poll scene {SceneId}", sceneId);
            }
        }
    }

    protected virtual void OnMessageReceived(SceneMessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, e);
    }
}
