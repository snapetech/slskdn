using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric;

/// <summary>
/// Interface for discovering mesh services via DHT.
/// </summary>
public interface IMeshServiceDirectory
{
    /// <summary>
    /// Finds all service instances with the given service name.
    /// </summary>
    /// <param name="serviceName">The service name to search for (e.g., "pods", "shadow-index").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching service descriptors (max 20, validated and filtered).</returns>
    Task<IReadOnlyList<MeshServiceDescriptor>> FindByNameAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a specific service instance by its service ID.
    /// </summary>
    /// <param name="serviceId">The deterministic service ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of matching service descriptors (should be 0 or 1).</returns>
    Task<IReadOnlyList<MeshServiceDescriptor>> FindByIdAsync(
        string serviceId,
        CancellationToken cancellationToken = default);
}
