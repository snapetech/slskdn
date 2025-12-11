namespace slskd.Signals.Swarm;

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using slskd.Swarm;

/// <summary>
/// Stores and manages swarm transfer jobs for signal handlers.
/// </summary>
public interface ISwarmJobStore
{
    Task<SwarmJob?> TryGetJobAsync(string jobId, CancellationToken ct = default);
}

/// <summary>
/// In-memory implementation of swarm job store.
/// </summary>
public class InMemorySwarmJobStore : ISwarmJobStore
{
    private readonly ConcurrentDictionary<string, SwarmJob> jobs = new();
    
    public Task<SwarmJob?> TryGetJobAsync(string jobId, CancellationToken ct = default)
    {
        jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }
}
