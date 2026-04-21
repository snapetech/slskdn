// <copyright file="ContentPeerHintService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Dht;

/// <summary>
/// Enqueue content IDs to publish peer hints for, and publish in background.
/// </summary>
public interface IContentPeerHintService
{
    bool Enqueue(string contentId);
}

public class ContentPeerHintService : BackgroundService, IContentPeerHintService
{
    private readonly ILogger<ContentPeerHintService> logger;
    private readonly IContentPeerPublisher publisher;
    private readonly Channel<string> queue = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });
    private readonly TimeSpan delayBetween = TimeSpan.FromSeconds(1);

    public ContentPeerHintService(ILogger<ContentPeerHintService> logger, IContentPeerPublisher publisher)
    {
        logger.LogDebug("[ContentPeerHintService] Constructor called");
        this.logger = logger;
        this.publisher = publisher;
        logger.LogDebug("[ContentPeerHintService] Constructor completed");
    }

    public bool Enqueue(string contentId)
    {
        return queue.Writer.TryWrite(contentId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        await Task.Yield();

        logger.LogDebug("[ContentPeerHintService] ExecuteAsync called");
        await foreach (var contentId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await publisher.PublishAsync(contentId, stoppingToken);
                await Task.Delay(delayBetween, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MeshContent] Failed to publish peer hint for {ContentId}: {Message}", contentId, ex.Message);
            }
        }
    }
}
