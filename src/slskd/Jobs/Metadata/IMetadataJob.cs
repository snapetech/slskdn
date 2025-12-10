namespace slskd.Jobs.Metadata;

/// <summary>
/// Base abstraction for metadata-related jobs (MB backfill, discography, repair, stress tests).
/// </summary>
public interface IMetadataJob
{
    string JobId { get; }
    string Kind { get; }
    Task ExecuteAsync(CancellationToken ct = default);
}
