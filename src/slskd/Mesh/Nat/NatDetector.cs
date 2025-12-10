namespace slskd.Mesh;

public enum NatType
{
    Unknown,
    Direct,
    Restricted,
    Symmetric
}

/// <summary>
/// NAT detection stub.
/// </summary>
public interface INatDetector
{
    Task<NatType> DetectAsync(CancellationToken ct = default);
}

public class NatDetector : INatDetector
{
    public Task<NatType> DetectAsync(CancellationToken ct = default)
    {
        // TODO: Implement STUN-based detection. Stub returns Unknown.
        return Task.FromResult(NatType.Unknown);
    }
}
