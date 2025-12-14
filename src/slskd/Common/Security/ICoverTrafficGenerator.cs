// <copyright file="ICoverTrafficGenerator.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for cover traffic generation.
/// </summary>
public interface ICoverTrafficGenerator
{
    /// <summary>
    /// Starts the cover traffic generation.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when generation starts.</returns>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the cover traffic generation.
    /// </summary>
    /// <returns>A task that completes when generation stops.</returns>
    Task StopAsync();

    /// <summary>
    /// Notifies the generator that real traffic was sent, resetting the idle timer.
    /// </summary>
    void NotifyTraffic();

    /// <summary>
    /// Gets statistics about cover traffic generation.
    /// </summary>
    CoverTrafficStats GetStats();
}

/// <summary>
/// Cover traffic generation statistics.
/// </summary>
public class CoverTrafficStats
{
    /// <summary>
    /// Gets the number of cover messages sent.
    /// </summary>
    public long CoverMessagesSent { get; set; }

    /// <summary>
    /// Gets the time since last real traffic.
    /// </summary>
    public TimeSpan TimeSinceLastTraffic { get; set; }

    /// <summary>
    /// Gets whether cover traffic generation is currently active.
    /// </summary>
    public bool IsActive { get; set; }
}
