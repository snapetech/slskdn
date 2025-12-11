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
/// Overlay pubsub service for scene gossip (stub for Phase 6C).
/// </summary>
public class ScenePubSubService : IScenePubSubService
{
    private readonly ILogger<ScenePubSubService> logger;

    public ScenePubSubService(ILogger<ScenePubSubService> logger)
    {
        this.logger = logger;
    }

    public event EventHandler<SceneMessageReceivedEventArgs>? MessageReceived;

    public Task SubscribeAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-PUBSUB] Subscribing to scene {SceneId}", sceneId);

        // TODO: Implement overlay pubsub subscription
        // For Phase 6C, this is a stub

        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(string sceneId, CancellationToken ct)
    {
        logger.LogDebug("[VSF-PUBSUB] Unsubscribing from scene {SceneId}", sceneId);

        // TODO: Implement overlay pubsub unsubscription

        return Task.CompletedTask;
    }

    public Task PublishAsync(string sceneId, byte[] message, CancellationToken ct)
    {
        logger.LogDebug("[VSF-PUBSUB] Publishing message to scene {SceneId}: {Size} bytes",
            sceneId, message.Length);

        // TODO: Implement overlay pubsub publish

        return Task.CompletedTask;
    }

    protected virtual void OnMessageReceived(SceneMessageReceivedEventArgs e)
    {
        MessageReceived?.Invoke(this, e);
    }
}
