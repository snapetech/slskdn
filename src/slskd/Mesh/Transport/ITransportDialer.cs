// <copyright file="ITransportDialer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Net;

namespace slskd.Mesh.Transport;

/// <summary>
/// Interface for transport dialers that establish connections to peers via different methods.
/// </summary>
public interface ITransportDialer
{
    /// <summary>
    /// Gets the transport type this dialer handles.
    /// </summary>
    TransportType TransportType { get; }

    /// <summary>
    /// Determines if this dialer can handle the given transport endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to check.</param>
    /// <returns>True if this dialer can handle the endpoint.</returns>
    bool CanHandle(TransportEndpoint endpoint);

    /// <summary>
    /// Attempts to establish a connection to the given transport endpoint.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream to the peer.</returns>
    Task<Stream> DialAsync(TransportEndpoint endpoint, string? isolationKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to establish a connection with certificate pinning validation.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="certificatePins">Certificate pins for validation.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream to the peer.</returns>
    Task<Stream> DialWithPinsAsync(TransportEndpoint endpoint, IEnumerable<string> certificatePins, string? isolationKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to establish a connection with peer-aware certificate pinning.
    /// </summary>
    /// <param name="endpoint">The transport endpoint to connect to.</param>
    /// <param name="peerId">The peer ID for pin management.</param>
    /// <param name="isolationKey">Optional isolation key for stream isolation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A connected stream to the peer.</returns>
    Task<Stream> DialWithPeerValidationAsync(TransportEndpoint endpoint, string peerId, string? isolationKey = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the dialer is available (e.g., proxy services are running).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the dialer is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about this dialer's usage.
    /// </summary>
    /// <returns>Dialer statistics.</returns>
    DialerStatistics GetStatistics();
}

/// <summary>
/// Statistics for a transport dialer.
/// </summary>
public class DialerStatistics
{
    /// <summary>
    /// Gets or sets the transport type.
    /// </summary>
    public TransportType TransportType { get; set; }

    /// <summary>
    /// Gets or sets the total number of dial attempts.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Gets or sets the number of successful connections.
    /// </summary>
    public int SuccessfulConnections { get; set; }

    /// <summary>
    /// Gets or sets the number of failed connections.
    /// </summary>
    public int FailedConnections { get; set; }

    /// <summary>
    /// Gets or sets the current number of active connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the average connection time in milliseconds.
    /// </summary>
    public double AverageConnectionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the dialer is currently available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the last error message, if any.
    /// </summary>
    public string? LastError { get; set; }
}
