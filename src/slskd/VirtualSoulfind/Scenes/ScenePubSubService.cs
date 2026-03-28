// <copyright file="ScenePubSubService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace slskd.VirtualSoulfind.Scenes;

/// <summary>
/// Interface for overlay pubsub (scene gossip).
/// </summary>
public interface IScenePubSubService : IDisposable
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
public class ScenePubSubService : IScenePubSubService, IDisposable
{
    private readonly ILogger<ScenePubSubService> logger;
    private readonly VirtualSoulfind.ShadowIndex.IDhtClient dht;
    private readonly ConcurrentDictionary<string, DateTimeOffset> subscriptions = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> seenMessages = new();
    private readonly CancellationTokenSource pollLoopCancellationTokenSource = new();
    private readonly Task pollLoopTask;
    private bool disposed;

    public ScenePubSubService(
        ILogger<ScenePubSubService> logger,
        VirtualSoulfind.ShadowIndex.IDhtClient dht)
        : this(logger, dht, TimeSpan.FromSeconds(30))
    {
    }

    internal ScenePubSubService(
        ILogger<ScenePubSubService> logger,
        VirtualSoulfind.ShadowIndex.IDhtClient dht,
        TimeSpan pollInterval)
    {
        this.logger = logger;
        this.dht = dht;

        pollLoopTask = Task.Factory.StartNew(() => RunPollLoopAsync(pollInterval, pollLoopCancellationTokenSource.Token), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
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

        // Phase 6C: T-816 - Store message in DHT with stable scene topic key so subscribers can query it
        var key = VirtualSoulfind.ShadowIndex.DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}");

        // Store with short TTL (5 minutes) - messages are ephemeral
        await dht.PutAsync(key, message, ttlSeconds: 300, ct);

        RememberMessage(sceneId, message);

        logger.LogInformation("[VSF-PUBSUB] Published message to scene {SceneId}", sceneId);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            pollLoopCancellationTokenSource.Cancel();

            try
            {
                pollLoopTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
            {
            }

            pollLoopCancellationTokenSource.Dispose();
        }

        disposed = true;
    }

    private async Task RunPollLoopAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(pollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await PollSubscribedScenesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task PollSubscribedScenesAsync(CancellationToken cancellationToken)
    {
        if (subscriptions.IsEmpty)
        {
            return;
        }

        foreach (var (sceneId, _) in subscriptions.ToList())
        {
            try
            {
                // Query DHT for recent messages in this scene
                var sceneKey = VirtualSoulfind.ShadowIndex.DhtKeyDerivation.DeriveSceneKey($"scene:pubsub:{sceneId}");
                var messages = await dht.GetMultipleAsync(sceneKey, cancellationToken);

                foreach (var messageData in messages)
                {
                    if (!ShouldDeliver(sceneId, messageData))
                    {
                        continue;
                    }

                    var peerId = TryExtractPeerId(messageData);

                    // Fire event for received message
                    OnMessageReceived(new SceneMessageReceivedEventArgs
                    {
                        SceneId = sceneId,
                        PeerId = peerId,
                        Message = messageData,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-PUBSUB] Failed to poll scene {SceneId}", sceneId);
            }
        }
    }

    protected virtual void OnMessageReceived(SceneMessageReceivedEventArgs e)
    {
        if (MessageReceived is null)
        {
            return;
        }

        foreach (EventHandler<SceneMessageReceivedEventArgs> handler in MessageReceived.GetInvocationList())
        {
            try
            {
                handler.Invoke(this, e);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[VSF-PUBSUB] MessageReceived subscriber failed");
            }
        }
    }

    private bool ShouldDeliver(string sceneId, byte[] message)
    {
        var messageId = GetMessageFingerprint(sceneId, message);
        return seenMessages.TryAdd(messageId, DateTimeOffset.UtcNow);
    }

    private void RememberMessage(string sceneId, byte[] message)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        foreach (var entry in seenMessages)
        {
            if (entry.Value < cutoff)
            {
                seenMessages.TryRemove(entry.Key, out _);
            }
        }

        seenMessages[GetMessageFingerprint(sceneId, message)] = DateTimeOffset.UtcNow;
    }

    private static string GetMessageFingerprint(string sceneId, byte[] message)
    {
        return $"{sceneId}:{Convert.ToHexString(SHA256.HashData(message))}";
    }

    private static string TryExtractPeerId(byte[] message)
    {
        try
        {
            var chatMessage = MessagePack.MessagePackSerializer.Deserialize<SceneChatMessage>(message);
            return chatMessage?.PeerId ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
