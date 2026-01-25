// <copyright file="IMeshStatsCollector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Mesh;

/// <summary>
/// Provides mesh transport statistics. Abstraction to allow tests to mock GetStatsAsync.
/// </summary>
public interface IMeshStatsCollector
{
    /// <summary>Gets current mesh transport statistics.</summary>
    Task<MeshTransportStats> GetStatsAsync();
}
