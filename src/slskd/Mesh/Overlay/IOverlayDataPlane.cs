using System.Net;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Data-plane overlay for bulk payloads over QUIC.
/// </summary>
public interface IOverlayDataPlane
{
    Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default);
}
