using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace slskd.Swarm;

/// <summary>
/// Background orchestrator for swarm downloads.
/// </summary>
public class SwarmDownloadOrchestrator : BackgroundService
{
    private readonly ILogger<SwarmDownloadOrchestrator> logger;
    private readonly IVerificationEngine verifier;
    private readonly Channel<SwarmJob> jobs = Channel.CreateUnbounded<SwarmJob>();

    public SwarmDownloadOrchestrator(ILogger<SwarmDownloadOrchestrator> logger, IVerificationEngine verifier)
    {
        this.logger = logger;
        this.verifier = verifier;
    }

    public bool Enqueue(SwarmJob job)
    {
        logger.LogDebug("[SwarmOrchestrator] Enqueue {JobId} ({ContentId})", job.JobId, job.File.ContentId);
        return jobs.Writer.TryWrite(job);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in jobs.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                logger.LogInformation("[SwarmOrchestrator] Start {JobId} ({ContentId})", job.JobId, job.File.ContentId);
                await ProcessJob(job, stoppingToken);
                logger.LogInformation("[SwarmOrchestrator] Completed {JobId}", job.JobId);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[SwarmOrchestrator] Failed {JobId}: {Message}", job.JobId, ex.Message);
            }
        }
    }

    private Task ProcessJob(SwarmJob job, CancellationToken ct)
    {
        // Placeholder: actual chunk scheduling and download would go here
        return Task.CompletedTask;
    }
}
