namespace slskd.Integrations.Brainz;

/// <summary>
/// Unified client placeholder for MB/AcoustID/Soulbeet with caching and backoff.
/// </summary>
public interface IBrainzClient
{
    Task<string?> GetRecordingAsync(string mbid, CancellationToken ct = default);
}

public class BrainzClient : IBrainzClient
{
    public Task<string?> GetRecordingAsync(string mbid, CancellationToken ct = default)
    {
        // Placeholder for unified client
        return Task.FromResult<string?>(null);
    }
}

