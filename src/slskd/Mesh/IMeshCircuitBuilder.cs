// <copyright file="IMeshCircuitBuilder.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

/// <summary>
/// Builds and maintains mesh circuits. Abstraction to allow tests to mock PerformMaintenance and GetStatistics.
/// </summary>
public interface IMeshCircuitBuilder
{
    /// <summary>Removes expired circuits.</summary>
    void PerformMaintenance();

    /// <summary>Returns current circuit statistics.</summary>
    CircuitStatistics GetStatistics();

    /// <summary>Builds a new circuit to the target peer.</summary>
    Task<MeshCircuit> BuildCircuitAsync(string targetPeerId, int circuitLength = 3, CancellationToken cancellationToken = default);

    /// <summary>Destroys a circuit by ID.</summary>
    void DestroyCircuit(string circuitId);

    /// <summary>Returns the list of active circuits.</summary>
    System.Collections.Generic.List<MeshCircuit> GetActiveCircuits();
}
