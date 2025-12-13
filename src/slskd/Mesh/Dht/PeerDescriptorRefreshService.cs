using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Dht;

/// <summary>
/// Periodically republishes our peer descriptor to the DHT (refresh TTL).
/// </summary>
public class PeerDescriptorRefreshService : BackgroundService
{
    private readonly ILogger<PeerDescriptorRefreshService> logger;
    private readonly IPeerDescriptorPublisher publisher;
    private readonly MeshOptions options;

    public PeerDescriptorRefreshService(
        ILogger<PeerDescriptorRefreshService> logger,
        IPeerDescriptorPublisher publisher,
        IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.publisher = publisher;
        this.options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Refresh every TTL/2; default TTL = 3600s, so refresh at 1800s
        var refreshMs = 1800_000;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await publisher.PublishSelfAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MeshDHT] Peer descriptor refresh failed");
            }

            try
            {
                await Task.Delay(refreshMs, stoppingToken);
            }
            catch (OperationCanceledException) { }
        }
    }
}















