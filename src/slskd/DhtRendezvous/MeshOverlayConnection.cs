// <copyright file="MeshOverlayConnection.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using slskd.DhtRendezvous.Messages;
using slskd.DhtRendezvous.Security;
using Serilog;

using ProtocolViolationException = slskd.DhtRendezvous.Security.ProtocolViolationException;

/// <summary>
/// Represents a secure overlay connection to a mesh peer.
/// Wraps a TCP connection with TLS and message framing.
/// </summary>
public sealed class MeshOverlayConnection : IAsyncDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly SslStream _sslStream;
    private readonly SecureMessageFramer _framer;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();

    private DateTimeOffset _lastActivity;
    private DateTimeOffset? _lastPingSent;
    private bool _disposed;

    /// <summary>
    /// Unique identifier for this connection.
    /// </summary>
    public string ConnectionId { get; }

    /// <summary>
    /// Remote endpoint.
    /// </summary>
    public IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Remote IP address.
    /// </summary>
    public IPAddress RemoteAddress => RemoteEndPoint.Address;

    /// <summary>
    /// Soulseek username of the remote peer (set after handshake).
    /// </summary>
    public string? Username { get; private set; }

    /// <summary>
    /// Features supported by the remote peer.
    /// </summary>
    public IReadOnlyList<string> Features { get; private set; } = Array.Empty<string>();

    /// <summary>
    /// Remote peer's advertised overlay listener port, if supplied during handshake.
    /// </summary>
    public int? RemoteOverlayPort { get; private set; }

    /// <summary>
    /// Whether handshake has completed successfully.
    /// </summary>
    public bool IsHandshakeComplete { get; private set; }

    /// <summary>
    /// Remote peer's certificate thumbprint.
    /// </summary>
    public string? CertificateThumbprint { get; private set; }

    /// <summary>
    /// When the connection was established.
    /// </summary>
    public DateTimeOffset ConnectedAt { get; }

    /// <summary>
    /// When the last activity occurred.
    /// </summary>
    public DateTimeOffset LastActivity
    {
        get
        {
            lock (_lock) return _lastActivity;
        }
        private set
        {
            lock (_lock) _lastActivity = value;
        }
    }

    /// <summary>
    /// Whether the connection is still active.
    /// </summary>
    public bool IsConnected => !_disposed && _tcpClient.Connected;

    /// <summary>
    /// Current state of the connection.
    /// </summary>
    public ConnectionState State { get; private set; } = ConnectionState.Connecting;

    /// <summary>
    /// True if we initiated the connection (outbound/client). False if the peer connected to us (inbound/server).
    /// Used to decide if we can do request-response (e.g. mesh_search_req) on this connection without
    /// competing with the server's read loop; only outbound connections are used for that.
    /// </summary>
    public bool IsOutbound { get; init; }

    private MeshOverlayConnection(
        TcpClient tcpClient,
        SslStream sslStream,
        IPEndPoint remoteEndPoint)
    {
        _tcpClient = tcpClient;
        _sslStream = sslStream;
        _framer = new SecureMessageFramer(sslStream);

        RemoteEndPoint = remoteEndPoint;
        ConnectionId = Guid.NewGuid().ToString("N")[..12];
        ConnectedAt = DateTimeOffset.UtcNow;
        _lastActivity = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Create a client connection (outbound).
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "TCP and TLS resources are transferred to the returned MeshOverlayConnection on success and disposed on failure.")]
    public static async Task<MeshOverlayConnection> ConnectAsync(
        IPEndPoint endpoint,
        X509Certificate2 clientCertificate,
        CancellationToken cancellationToken = default)
    {
        TcpClient? tcpClient = null;
        SslStream? sslStream = null;

        try
        {
            tcpClient = new TcpClient();

            // Connect with timeout
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(OverlayTimeouts.Connect);

            await tcpClient.ConnectAsync(endpoint.Address, endpoint.Port, connectCts.Token);

            // Establish TLS
            sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                ValidateServerCertificate);

            using var tlsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tlsCts.CancelAfter(OverlayTimeouts.TlsHandshake);

            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = "slskdn-overlay",
                ClientCertificates = new X509CertificateCollection { clientCertificate },
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, tlsCts.Token);

            var ownedTcpClient = tcpClient ?? throw new InvalidOperationException("TCP client was not initialized");
            var ownedSslStream = sslStream ?? throw new InvalidOperationException("TLS stream was not initialized");
            tcpClient = null;
            sslStream = null;

            var connection = new MeshOverlayConnection(ownedTcpClient, ownedSslStream, endpoint)
            {
                State = ConnectionState.TlsEstablished,
                CertificateThumbprint = ownedSslStream.RemoteCertificate?.GetCertHashString(),
                IsOutbound = true,
            };
            return connection;
        }
        catch
        {
            sslStream?.Dispose();
            tcpClient?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Create a server connection (inbound).
    /// </summary>
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "TCP and TLS resources are transferred to the returned MeshOverlayConnection on success and disposed on failure.")]
    public static async Task<MeshOverlayConnection> AcceptAsync(
        TcpClient tcpClient,
        X509Certificate2 serverCertificate,
        CancellationToken cancellationToken = default)
    {
        var remoteEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;

        SslStream? sslStream = null;

        try
        {
            // Establish TLS
            sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                ValidateClientCertificate);

            using var tlsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            tlsCts.CancelAfter(OverlayTimeouts.TlsHandshake);

            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = serverCertificate,
                ClientCertificateRequired = false,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            }, tlsCts.Token);

            var ownedSslStream = sslStream ?? throw new InvalidOperationException("TLS stream was not initialized");
            sslStream = null;

            var connection = new MeshOverlayConnection(tcpClient, ownedSslStream, remoteEndPoint)
            {
                State = ConnectionState.TlsEstablished,
                CertificateThumbprint = ownedSslStream.RemoteCertificate?.GetCertHashString(),
            };
            return connection;
        }
        catch
        {
            sslStream?.Dispose();
            tcpClient.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Perform the protocol handshake as the client (send HELLO, receive ACK).
    /// </summary>
    public async Task<MeshHelloAckMessage> PerformClientHandshakeAsync(
        string username,
        SoulseekPorts? ports = null,
        int? overlayPort = null,
        CancellationToken cancellationToken = default)
    {
        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeCts.CancelAfter(OverlayTimeouts.ProtocolHandshake);

        // Generate nonce for replay prevention
        var nonce = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("/", "_").Replace("+", "-")[..22];

        // Send HELLO
        var hello = new MeshHelloMessage
        {
            Username = username,
            Features = OverlayFeatures.All.ToList(),
            SoulseekPorts = ports,
            OverlayPort = overlayPort,
            Nonce = nonce,
        };

        await _framer.WriteMessageAsync(hello, handshakeCts.Token);
        LastActivity = DateTimeOffset.UtcNow;

        // Receive ACK
        var ack = await _framer.ReadMessageAsync<MeshHelloAckMessage>(handshakeCts.Token);
        LastActivity = DateTimeOffset.UtcNow;

        // Validate
        var validation = MessageValidator.ValidateMeshHelloAck(ack);
        if (!validation.IsValid)
        {
            throw new ProtocolViolationException("Invalid HELLO_ACK");
        }

        // Verify nonce echo (optional but recommended)
        if (ack.NonceEcho != nonce)
        {
            throw new ProtocolViolationException("Nonce mismatch - possible replay attack");
        }

        Username = ack.Username;
        Features = ack.Features?.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();
        RemoteOverlayPort = ack.OverlayPort;
        IsHandshakeComplete = true;
        State = ConnectionState.Active;

        return ack;
    }

    /// <summary>
    /// Perform the protocol handshake as the server (receive HELLO, send ACK).
    /// </summary>
    public async Task<MeshHelloMessage> PerformServerHandshakeAsync(
        string username,
        SoulseekPorts? ports = null,
        int? overlayPort = null,
        CancellationToken cancellationToken = default)
    {
        using var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshakeCts.CancelAfter(OverlayTimeouts.ProtocolHandshake);

        // Receive HELLO
        var hello = await _framer.ReadMessageAsync<MeshHelloMessage>(handshakeCts.Token);
        LastActivity = DateTimeOffset.UtcNow;

        // Validate
        var validation = MessageValidator.ValidateMeshHello(hello);
        if (!validation.IsValid)
        {
            throw new ProtocolViolationException("Invalid HELLO");
        }

        // Send ACK
        var ack = new MeshHelloAckMessage
        {
            Username = username,
            Features = OverlayFeatures.All.ToList(),
            SoulseekPorts = ports,
            OverlayPort = overlayPort,
            NonceEcho = hello.Nonce,
        };

        await _framer.WriteMessageAsync(ack, handshakeCts.Token);
        LastActivity = DateTimeOffset.UtcNow;

        Username = hello.Username;
        Features = hello.Features?.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>();
        RemoteOverlayPort = hello.OverlayPort;
        IsHandshakeComplete = true;
        State = ConnectionState.Active;

        return hello;
    }

    /// <summary>
    /// Read a message from the connection.
    /// </summary>
    public async Task<T> ReadMessageAsync<T>(CancellationToken cancellationToken = default)
    {
        ThrowIfNotActive();

        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        readCts.CancelAfter(OverlayTimeouts.MessageRead);

        var message = await _framer.ReadMessageAsync<T>(readCts.Token);
        LastActivity = DateTimeOffset.UtcNow;

        return message;
    }

    /// <summary>
    /// Read raw message bytes.
    /// </summary>
    public async Task<byte[]> ReadRawMessageAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNotActive();

        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        readCts.CancelAfter(OverlayTimeouts.MessageRead);

        var data = await _framer.ReadRawMessageAsync(readCts.Token);
        LastActivity = DateTimeOffset.UtcNow;

        return data;
    }

    /// <summary>
    /// Write a message to the connection.
    /// </summary>
    public async Task WriteMessageAsync<T>(T message, CancellationToken cancellationToken = default)
    {
        ThrowIfNotActive();

        using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);

        // SECURITY: Use shorter write timeout to prevent slow clients from holding connections
        writeCts.CancelAfter(OverlayTimeouts.MessageWrite);

        await _framer.WriteMessageAsync(message, writeCts.Token);
        LastActivity = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Send a ping and wait for pong.
    /// </summary>
    /// <remarks>
    /// Only use this when no long-lived message loop is already reading the connection.
    /// Persistent overlay loops must use <see cref="SendKeepalivePingAsync" /> instead.
    /// </remarks>
    public async Task<TimeSpan> PingAsync(CancellationToken cancellationToken = default)
    {
        var ping = new PingMessage();
        _lastPingSent = DateTimeOffset.UtcNow;

        await WriteMessageAsync(ping, cancellationToken);

        using var pongCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        pongCts.CancelAfter(OverlayTimeouts.PongTimeout);

        var pong = await ReadMessageAsync<PongMessage>(pongCts.Token);

        var rtt = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(pong.Timestamp);
        return rtt;
    }

    /// <summary>
    /// Send a keepalive ping without reading from the stream.
    /// </summary>
    public async Task SendKeepalivePingAsync(CancellationToken cancellationToken = default)
    {
        var ping = new PingMessage();
        _lastPingSent = DateTimeOffset.UtcNow;

        await WriteMessageAsync(ping, cancellationToken);
    }

    /// <summary>
    /// Send a graceful disconnect message.
    /// </summary>
    public async Task DisconnectAsync(string? reason = null, CancellationToken cancellationToken = default)
    {
        if (_disposed || State == ConnectionState.Disconnecting)
        {
            return;
        }

        State = ConnectionState.Disconnecting;

        try
        {
            var disconnect = new DisconnectMessage { Reason = reason };
            await WriteMessageAsync(disconnect, cancellationToken);
            await Task.Delay(OverlayTimeouts.DisconnectGrace, cancellationToken);
        }
        catch
        {
            // Best effort
        }
        finally
        {
            await DisposeAsync();
        }
    }

    /// <summary>
    /// Check if the connection has been idle too long.
    /// </summary>
    public bool IsIdle()
    {
        return DateTimeOffset.UtcNow - LastActivity > OverlayTimeouts.Idle;
    }

    /// <summary>
    /// Check if it's time to send a keepalive.
    /// </summary>
    public bool NeedsKeepalive()
    {
        var lastPing = _lastPingSent ?? ConnectedAt;
        return DateTimeOffset.UtcNow - lastPing > OverlayTimeouts.KeepaliveInterval;
    }

    private void ThrowIfNotActive()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MeshOverlayConnection));
        }

        if (State != ConnectionState.Active)
        {
            throw new InvalidOperationException($"Connection not active (state: {State})");
        }
    }

    private static bool ValidateServerCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null)
        {
            return false;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            return true;
        }

        // Allow the overlay to use self-signed certificates during bootstrap, while still rejecting
        // certificates that are clearly invalid (time-bad, unavailable, or from a wrong hostname with no
        // self-sign identity).
        if (!sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors) &&
            !sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
        {
            return false;
        }

        if (certificate is not X509Certificate2 certificate2)
        {
            return false;
        }

        if (certificate2.NotAfter <= DateTime.UtcNow || certificate2.NotBefore > DateTime.UtcNow)
        {
            return false;
        }

        var isSelfSigned = string.Equals(certificate2.Subject, certificate2.Issuer, StringComparison.OrdinalIgnoreCase);
        if (!isSelfSigned && sslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors)
        {
            return false;
        }

        if (chain is not null)
        {
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                    status.Status != X509ChainStatusFlags.PartialChain)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidateClientCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // Client certificates are optional
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        State = ConnectionState.Disconnected;

        await _cts.CancelAsync();
        _cts.Dispose();

        try
        {
            await _sslStream.DisposeAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[MeshOverlayConnection] SSL stream dispose failed for {RemoteEndpoint}", RemoteEndPoint);
        }

        _framer.Dispose();
        _tcpClient.Dispose();
    }
}

/// <summary>
/// Connection state.
/// </summary>
public enum ConnectionState
{
    /// <summary>TCP connection in progress.</summary>
    Connecting,

    /// <summary>TLS established, waiting for handshake.</summary>
    TlsEstablished,

    /// <summary>Handshake complete, ready for messages.</summary>
    Active,

    /// <summary>Disconnect in progress.</summary>
    Disconnecting,

    /// <summary>Connection closed.</summary>
    Disconnected,
}
