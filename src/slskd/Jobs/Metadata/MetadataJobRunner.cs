// <copyright file="MetadataJobRunner.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using System.Threading;
using System.Threading.Tasks;

namespace slskd.Jobs.Metadata;

/// <summary>
/// Background runner for metadata jobs using channel-based queue.
/// </summary>
public class MetadataJobRunner : BackgroundService
{
    private readonly ILogger<MetadataJobRunner> logger;
    private readonly Channel<IMetadataJob> channel = Channel.CreateUnbounded<IMetadataJob>();

    public MetadataJobRunner(ILogger<MetadataJobRunner> logger)
    {
        this.logger = logger;
    }

    public bool Enqueue(IMetadataJob job)
    {
        logger.LogInformation("[MetadataJobRunner] Enqueue {JobId} ({Kind})", job.JobId, job.Kind);
        return channel.Writer.TryWrite(job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                logger.LogInformation("[MetadataJobRunner] Start {JobId} ({Kind})", job.JobId, job.Kind);
                await job.ExecuteAsync(stoppingToken);
                logger.LogInformation("[MetadataJobRunner] Completed {JobId}", job.JobId);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[MetadataJobRunner] Failed {JobId}: {Message}", job.JobId, ex.Message);
            }
        }
    }
}
