using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Dht;

namespace slskd.Mesh.Bootstrap;

/// <summary>
/// Hosted service to publish self descriptor and optionally warm up bootstrap.
/// </summary>
public class MeshBootstrapService : BackgroundService
{
    private readonly ILogger<MeshBootstrapService> logger;
    private readonly IPeerDescriptorPublisher publisher;
    private readonly MeshOptions options;
    private readonly TimeSpan refreshInterval = TimeSpan.FromMinutes(30);

    public MeshBootstrapService(
        ILogger<MeshBootstrapService> logger,
        IPeerDescriptorPublisher publisher,
        IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.publisher = publisher;
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PublishOnce(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(refreshInterval, stoppingToken);
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
        await publisher.PublishSelfAsync(ct);
        logger.LogDebug("[MeshBootstrap] Self descriptor published; bootstrap nodes={Count}", options.BootstrapNodes.Count);
    }
}

