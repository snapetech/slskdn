namespace slskd.Swarm;

/// <summary>
/// Centralized verification engine for swarm chunks.
/// </summary>
public interface IVerificationEngine
{
    Task<bool> VerifyChunkAsync(string contentId, int chunkIndex, byte[] data, CancellationToken ct = default);
}
