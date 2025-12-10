using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace slskd.MediaCore;

/// <summary>
/// Source of descriptors to publish.
/// </summary>
public interface IContentDescriptorSource
{
    IAsyncEnumerable<ContentDescriptor> GetDescriptorsAsync(CancellationToken ct = default);
}

/// <summary>
/// In-memory descriptor source (placeholder).
/// </summary>
public class InMemoryContentDescriptorSource : IContentDescriptorSource
{
    private readonly List<ContentDescriptor> descriptors;

    public InMemoryContentDescriptorSource(IEnumerable<ContentDescriptor>? seed = null)
    {
        descriptors = seed?.ToList() ?? new List<ContentDescriptor>();
    }

    public void Add(ContentDescriptor descriptor) => descriptors.Add(descriptor);

    public async IAsyncEnumerable<ContentDescriptor> GetDescriptorsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var d in descriptors)
        {
            ct.ThrowIfCancellationRequested();
            yield return d;
            await Task.Yield();
        }
    }
}

/// <summary>
/// Background service to publish descriptors to DHT.
/// </summary>
public class ContentPublisherService : BackgroundService
{
    private readonly ILogger<ContentPublisherService> logger;
    private readonly IDescriptorPublisher publisher;
    private readonly IContentDescriptorSource source;
    private readonly TimeSpan interval = TimeSpan.FromMinutes(30);

    public ContentPublisherService(
        ILogger<ContentPublisherService> logger,
        IDescriptorPublisher publisher,
        IContentDescriptorSource source)
    {
        this.logger = logger;
        this.publisher = publisher;
        this.source = source;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PublishOnce(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await PublishOnce(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
        }
    }

    private async Task PublishOnce(CancellationToken ct)
    {
        await foreach (var descriptor in source.GetDescriptorsAsync(ct))
        {
            await publisher.PublishAsync(descriptor, ct);
        }
        logger.LogDebug("[MediaCore] Published descriptors batch");
    }
}
