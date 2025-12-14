// <copyright file="IAnonymityTransportSelector.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for selecting and managing anonymity transports with policy-aware selection.
/// </summary>
public interface IAnonymityTransportSelector
{
    /// <summary>
    /// Selects the best available transport for a connection with policy consideration.
    /// </summary>
    /// <param name="peerId">The target peer ID for policy lookup.</param>
    /// <param name="podId">The pod ID for policy lookup (optional).</param>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected transport and connection stream.</returns>
    Task<(IAnonymityTransport Transport, Stream Stream)> SelectAndConnectAsync(
        string peerId,
        string? podId,
        string host,
        int port,
        string? isolationKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects the best available transport for a connection (legacy method without policy).
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The selected transport and connection stream.</returns>
    Task<(IAnonymityTransport Transport, Stream Stream)> SelectAndConnectAsync(
        string host,
        int port,
        string? isolationKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of all available transports.
    /// </summary>
    Dictionary<AnonymityTransportType, AnonymityTransportStatus> GetTransportStatuses();

    /// <summary>
    /// Tests connectivity for all transports.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Task that completes when all connectivity tests are done.</returns>
    Task TestConnectivityAsync(CancellationToken cancellationToken = default);
}


