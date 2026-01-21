// <copyright file="MeshBootstrapService.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

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
        logger.LogInformation("[MeshBootstrapService] Constructor called");
        this.logger = logger;
        this.publisher = publisher;
        this.options = options.Value;
        logger.LogInformation("[MeshBootstrapService] Constructor completed");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[MeshBootstrapService] ExecuteAsync called");
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
        logger.LogInformation("[MeshBootstrap] Publishing self descriptor to DHT");
        await publisher.PublishSelfAsync(ct);
        logger.LogDebug("[MeshBootstrap] Self descriptor published; bootstrap nodes={Count}", options.BootstrapNodes.Count);
    }
}
