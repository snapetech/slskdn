using System;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Client interface for making service calls over the mesh.
/// </summary>
public interface IMeshServiceClient
{
    /// <summary>
    /// Call a service on a remote peer.
    /// </summary>
    /// <param name="targetPeerId">Peer ID hosting the service.</param>
    /// <param name="call">The service call to make.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service reply from the remote peer.</returns>
    Task<ServiceReply> CallAsync(
        string targetPeerId,
        ServiceCall call,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Call a service by resolving it via the service directory first.
    /// </summary>
    /// <param name="serviceName">Service name to call.</param>
    /// <param name="method">Method to invoke.</param>
    /// <param name="payload">Request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Service reply from a discovered peer.</returns>
    Task<ServiceReply> CallServiceAsync(
        string serviceName,
        string method,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default);
}
