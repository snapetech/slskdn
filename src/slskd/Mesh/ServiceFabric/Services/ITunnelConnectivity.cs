// <copyright file="ITunnelConnectivity.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace slskd.Mesh.ServiceFabric.Services;

/// <summary>
/// Abstracts TCP connection to a destination for tunnel establishment.
/// Allows tests to inject a fake that connects to an in-process listener.
/// </summary>
public interface ITunnelConnectivity
{
    /// <summary>
    /// Establishes a TCP connection to the destination.
    /// </summary>
    /// <param name="host">Destination host.</param>
    /// <param name="port">Destination port.</param>
    /// <param name="resolvedIPs">IPs that DNS/validation allowed (caller may use for rebinding check).</param>
    /// <param name="cancellationToken">Cancellation (may be linked to a connect timeout).</param>
    /// <returns>(stream, connectedIP for rebinding check, or null to skip).</returns>
    Task<(NetworkStream Stream, string? ConnectedIP)> ConnectAsync(
        string host,
        int port,
        IReadOnlyList<string> resolvedIPs,
        CancellationToken cancellationToken);
}

/// <summary>
/// Production implementation using TcpClient.
/// </summary>
public sealed class DefaultTunnelConnectivity : ITunnelConnectivity
{
    public async Task<(NetworkStream Stream, string? ConnectedIP)> ConnectAsync(
        string host,
        int port,
        IReadOnlyList<string> resolvedIPs,
        CancellationToken cancellationToken)
    {
        var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        var stream = tcpClient.GetStream();
        var remote = tcpClient.Client.RemoteEndPoint as System.Net.IPEndPoint;
        var connectedIP = remote?.Address.ToString();
        return (stream, connectedIP);
    }
}
