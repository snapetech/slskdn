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
    private readonly Channel<string> queue = Channel.CreateUnbounded<string>();
    private readonly TimeSpan delayBetween = TimeSpan.FromSeconds(1);

    public ContentPeerHintService(ILogger<ContentPeerHintService> logger, IContentPeerPublisher publisher)
    {
        this.logger = logger;
        this.publisher = publisher;
    }

    public bool Enqueue(string contentId)
    {
        return queue.Writer.TryWrite(contentId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
