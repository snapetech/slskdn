using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Dht;

namespace slskd.Mesh.Bootstrap;

/// <summary>
/// Hosted service to publish self descriptor and optionally warm up bootstrap.
/// Also populates the computed PeerId in MeshOptions at startup.
/// </summary>
public class MeshBootstrapService : BackgroundService
{
    private readonly ILogger<MeshBootstrapService> logger;
    private readonly IPeerDescriptorPublisher publisher;
    private readonly IOptionsMonitor<MeshOptions> optionsMonitor;
    private readonly Security.IIdentityKeyStore identityKeyStore;
    private readonly TimeSpan refreshInterval = TimeSpan.FromMinutes(30);

    public MeshBootstrapService(
        ILogger<MeshBootstrapService> logger,
        IPeerDescriptorPublisher publisher,
        IOptionsMonitor<MeshOptions> optionsMonitor,
        Security.IIdentityKeyStore identityKeyStore)
    {
        this.logger = logger;
        this.publisher = publisher;
        this.optionsMonitor = optionsMonitor;
        this.identityKeyStore = identityKeyStore;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // CRITICAL: Populate MeshOptions.SelfPeerId from identity key
        var computedPeerId = identityKeyStore.ComputePeerId();
        var options = optionsMonitor.CurrentValue;
        options.SelfPeerId = computedPeerId;

        logger.LogInformation("[MeshBootstrap] Computed PeerId from identity: {PeerId}", computedPeerId);
        logger.LogInformation("[MeshBootstrap] Starting service (refresh interval: {Minutes} minutes, bootstrap nodes: {Count})", 
            refreshInterval.TotalMinutes, options.BootstrapNodes.Count);
        
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
        
        logger.LogInformation("[MeshBootstrap] Service stopped");
    }

    private async Task PublishOnce(CancellationToken ct)
    {
        logger.LogInformation("[MeshBootstrap] Publishing signed descriptor to DHT for PeerId={PeerId}",
            optionsMonitor.CurrentValue.SelfPeerId);
        await publisher.PublishSelfAsync(ct);
        logger.LogDebug("[MeshBootstrap] Descriptor published; bootstrap nodes={Count}",
            optionsMonitor.CurrentValue.BootstrapNodes.Count);
    }
}






















