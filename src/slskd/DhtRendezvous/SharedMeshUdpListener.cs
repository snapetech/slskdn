// <copyright file="SharedMeshUdpListener.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
namespace slskd.DhtRendezvous;

using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using MonoTorrent.Connections;
using MonoTorrent.Connections.Dht;

/// <summary>
/// Shares the public DHT UDP socket with QUIC by proxying QUIC Initial flows to a loopback MsQuic listener.
/// </summary>
public sealed class SharedMeshUdpListener : IDhtListener, IDisposable
{
    private static readonly TimeSpan SessionIdleTimeout = TimeSpan.FromMinutes(2);
    private readonly IPEndPoint _listenEndPoint;
    private readonly IPEndPoint _quicBackendEndPoint;
    private readonly ILogger<SharedMeshUdpListener> _logger;
    private readonly ConcurrentDictionary<IPEndPoint, QuicProxySession> _quicSessions = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly List<UdpClient> _publicSockets = new();
    private readonly List<Task> _receiveTasks = new();
    private UdpClient? _defaultPublicUdp;
    private ListenerStatus _status = ListenerStatus.NotListening;

    public SharedMeshUdpListener(
        IPEndPoint listenEndPoint,
        IPEndPoint quicBackendEndPoint,
        ILogger<SharedMeshUdpListener> logger)
    {
        _listenEndPoint = listenEndPoint;
        _quicBackendEndPoint = quicBackendEndPoint;
        _logger = logger;
    }

    public event Action<ReadOnlyMemory<byte>, IPEndPoint>? MessageReceived;
    public event EventHandler<EventArgs>? StatusChanged;

    public IPEndPoint LocalEndPoint => (IPEndPoint?)_defaultPublicUdp?.Client.LocalEndPoint ?? _listenEndPoint;
    public ListenerStatus Status => _status;

    public void Start()
    {
        if (_defaultPublicUdp is not null)
        {
            return;
        }

        try
        {
            foreach (var endpoint in GetBindEndPoints(_listenEndPoint))
            {
                var udp = new UdpClient(endpoint);
                _publicSockets.Add(udp);
                _defaultPublicUdp ??= udp;
            }

            SetStatus(ListenerStatus.Listening);
            foreach (var udp in _publicSockets)
            {
                _receiveTasks.Add(ReceiveLoopAsync(udp, _cts.Token));
            }

            _logger.LogInformation(
                "[DHT] Shared UDP listener bound {PublicPort} on {SocketCount} socket(s); QUIC datagrams proxy to {Backend}",
                _listenEndPoint.Port,
                _publicSockets.Count,
                _quicBackendEndPoint);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            SetStatus(ListenerStatus.PortNotFree);
            throw;
        }
    }

    public void Stop()
    {
        _cts.Cancel();
        foreach (var udp in _publicSockets)
        {
            udp.Dispose();
        }

        _publicSockets.Clear();
        _receiveTasks.Clear();
        _defaultPublicUdp = null;
        foreach (var session in _quicSessions.Values)
        {
            session.Dispose();
        }

        _quicSessions.Clear();
        SetStatus(ListenerStatus.NotListening);
    }

    public async Task SendAsync(ReadOnlyMemory<byte> buffer, IPEndPoint endpoint)
    {
        var udp = _defaultPublicUdp ?? throw new InvalidOperationException("Shared UDP listener is not started.");
        await udp.SendAsync(buffer.ToArray(), endpoint).ConfigureAwait(false);
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }

    internal static bool IsQuicInitialPacket(ReadOnlySpan<byte> buffer)
    {
        return buffer.Length > 0 && (buffer[0] & 0xC0) == 0xC0;
    }

    private async Task ReceiveLoopAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                PruneIdleQuicSessions();

                if (_quicSessions.TryGetValue(result.RemoteEndPoint, out var existingSession))
                {
                    await existingSession.ForwardToBackendAsync(result.Buffer, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (IsQuicInitialPacket(result.Buffer))
                {
                    var session = _quicSessions.GetOrAdd(
                        result.RemoteEndPoint,
                        endpoint => new QuicProxySession(endpoint, _quicBackendEndPoint, udp, _logger, _cts.Token));

                    await session.ForwardToBackendAsync(result.Buffer, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                MessageReceived?.Invoke(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[DHT] Shared UDP listener receive loop error");
            }
        }
    }

    private void PruneIdleQuicSessions()
    {
        var cutoff = DateTimeOffset.UtcNow - SessionIdleTimeout;
        foreach (var (endpoint, session) in _quicSessions)
        {
            if (session.LastActivity >= cutoff || !_quicSessions.TryRemove(endpoint, out var removed))
            {
                continue;
            }

            removed.Dispose();
        }
    }

    private void SetStatus(ListenerStatus status)
    {
        if (_status == status)
        {
            return;
        }

        _status = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyList<IPEndPoint> GetBindEndPoints(IPEndPoint listenEndPoint)
    {
        if (!listenEndPoint.Address.Equals(IPAddress.Any))
        {
            return new[] { listenEndPoint };
        }

        var endpoints = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .Distinct()
            .OrderBy(address => IPAddress.IsLoopback(address) ? 1 : 0)
            .Select(address => new IPEndPoint(address, listenEndPoint.Port))
            .ToList();

        return endpoints.Count > 0
            ? endpoints
            : new[] { listenEndPoint };
    }

    private sealed class QuicProxySession : IDisposable
    {
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _backendEndPoint;
        private readonly UdpClient _backendUdp = new(new IPEndPoint(IPAddress.Loopback, 0));
        private readonly UdpClient _publicUdp;
        private readonly ILogger _logger;
        private readonly CancellationTokenSource _cts;
        private readonly Task _backendReceiveTask;

        public QuicProxySession(
            IPEndPoint remoteEndPoint,
            IPEndPoint backendEndPoint,
            UdpClient publicUdp,
            ILogger logger,
            CancellationToken parentToken)
        {
            _remoteEndPoint = remoteEndPoint;
            _backendEndPoint = backendEndPoint;
            _publicUdp = publicUdp;
            _logger = logger;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(parentToken);
            LastActivity = DateTimeOffset.UtcNow;
            _backendReceiveTask = ReceiveFromBackendAsync(_cts.Token);
        }

        public DateTimeOffset LastActivity { get; private set; }

        public async Task ForwardToBackendAsync(byte[] datagram, CancellationToken cancellationToken)
        {
            LastActivity = DateTimeOffset.UtcNow;
            await _backendUdp.SendAsync(datagram, _backendEndPoint, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _backendUdp.Dispose();
            _cts.Dispose();
        }

        private async Task ReceiveFromBackendAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _backendUdp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    LastActivity = DateTimeOffset.UtcNow;
                    await _publicUdp.SendAsync(result.Buffer, _remoteEndPoint, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[DHT] QUIC UDP proxy session for {Endpoint} stopped", _remoteEndPoint);
                    break;
                }
            }
        }
    }
}
