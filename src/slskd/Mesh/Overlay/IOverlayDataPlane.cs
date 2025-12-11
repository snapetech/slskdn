using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Data-plane overlay for bulk payloads over QUIC.
/// </summary>
public interface IOverlayDataPlane
{
    Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default);
}
