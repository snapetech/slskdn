namespace slskd.Jobs.Metadata;

/// <summary>
/// Mesh network simulation / stress test job placeholder.
/// </summary>
public class NetworkSimulationJob : IMetadataJob
{
    public string JobId { get; } = Ulid.NewUlid().ToString();
    public string Kind => "network-simulation";

    public Task ExecuteAsync(CancellationToken ct = default)
    {
        // Placeholder for future simulation logic
        return Task.CompletedTask;
    }
}
