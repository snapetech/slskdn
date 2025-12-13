namespace slskd.Mesh;

public enum NatType
{
    Unknown,
    Direct,
    Restricted,
    Symmetric
}

/// <summary>
/// NAT detection service interface.
/// </summary>
public interface INatDetector
{
    Task<NatType> DetectAsync(CancellationToken ct = default);
}















