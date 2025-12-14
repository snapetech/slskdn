// <copyright file="IAnonymityTransport.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

namespace slskd.Common.Security;

/// <summary>
/// Interface for anonymity transport implementations.
/// </summary>
public interface IAnonymityTransport
{
    /// <summary>
    /// Gets the transport type.
    /// </summary>
    AnonymityTransportType TransportType { get; }

    /// <summary>
    /// Checks if the transport is available and functional.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the transport is available, false otherwise.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Establishes a connection through the anonymity transport.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the anonymous connection.</returns>
    Task<Stream> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Establishes a connection through the anonymity transport with stream isolation.
    /// </summary>
    /// <param name="host">The target host.</param>
    /// <param name="port">The target port.</param>
    /// <param name="isolationKey">Optional key for stream isolation (different keys use different circuits).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A stream for the anonymous connection.</returns>
    Task<Stream> ConnectAsync(string host, int port, string? isolationKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of the anonymity transport.
    /// </summary>
    AnonymityTransportStatus GetStatus();
}

/// <summary>
/// Anonymity transport types.
/// </summary>
public enum AnonymityTransportType
{
    /// <summary>
    /// Direct connection (no anonymity).
    /// </summary>
    Direct,

    /// <summary>
    /// Tor SOCKS proxy.
    /// </summary>
    Tor,

    /// <summary>
    /// I2P network.
    /// </summary>
    I2P,

    /// <summary>
    /// Relay-only (no direct connections).
    /// </summary>
    RelayOnly,

    /// <summary>
    /// WebSocket tunneling for DPI evasion.
    /// </summary>
    WebSocket,

    /// <summary>
    /// HTTP tunnel for DPI evasion.
    /// </summary>
    HttpTunnel,

    /// <summary>
    /// Obfs4 pluggable transport for censorship resistance.
    /// </summary>
    Obfs4,

    /// <summary>
    /// Meek domain fronting for firewall bypass.
    /// </summary>
    Meek,
}

/// <summary>
/// Status of an anonymity transport.
/// </summary>
public class AnonymityTransportStatus
{
    /// <summary>
    /// Gets or sets whether the transport is available.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Gets or sets the last error message, if any.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets the time of last successful connection.
    /// </summary>
    public DateTimeOffset? LastSuccessfulConnection { get; set; }

    /// <summary>
    /// Gets or sets the number of active connections.
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// Gets or sets the total connections attempted.
    /// </summary>
    public long TotalConnectionsAttempted { get; set; }

    /// <summary>
    /// Gets or sets the total successful connections.
    /// </summary>
    public long TotalConnectionsSuccessful { get; set; }
}
