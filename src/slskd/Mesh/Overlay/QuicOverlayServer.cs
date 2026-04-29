// <copyright file="QuicOverlayServer.cs" company="slskdN Team">
//     Copyright (c) slskdN Team. All rights reserved.
// </copyright>
#pragma warning disable CA2252 // Preview features - QUIC APIs require preview features

namespace slskd.Mesh.Overlay;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh;
using slskd.Mesh.Transport;

/// <summary>
/// QUIC overlay server for control-plane messages (ControlEnvelope).
/// </summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
[SupportedOSPlatform("windows")]
public class QuicOverlayServer : BackgroundService, IOverlayConnectionMetrics
{
    private readonly ILogger<QuicOverlayServer> logger;
    private readonly OverlayOptions options;
    private readonly IControlDispatcher dispatcher;
    private readonly ConnectionThrottler connectionThrottler;
    private readonly int maxRemotePayload;
    private readonly ConcurrentDictionary<IPEndPoint, QuicConnection> activeConnections = new();
    private readonly ConcurrentDictionary<IPEndPoint, string> _pinnedRemoteCertificates = new();
    private readonly ConcurrentDictionary<int, Task> activeConnectionTasks = new();
    private int nextConnectionTaskId;

    public QuicOverlayServer(
        ILogger<QuicOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher,
        ConnectionThrottler connectionThrottler,
        IOptions<Mesh.MeshOptions>? meshOptions = null)
    {
        logger.LogDebug("[QuicOverlayServer] Constructor called");
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.connectionThrottler = connectionThrottler ?? throw new ArgumentNullException(nameof(connectionThrottler));
        maxRemotePayload = meshOptions?.Value?.Security?.GetEffectiveMaxPayloadSize() ?? SecurityUtils.MaxRemotePayloadSize;
        logger.LogDebug("[QuicOverlayServer] Constructor completed");
    }

    /// <summary>
    /// Gets the count of active QUIC connections (for metrics).
    /// </summary>
    public int GetActiveConnectionCount() => activeConnections.Count;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CloseActiveConnectionsAsync().ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await DrainConnectionTasksAsync(cancellationToken).ConfigureAwait(false);
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Critical: never block host startup (BackgroundService.StartAsync runs until first await)
        // This yields immediately so Kestrel can start binding while QUIC initializes
        await Task.Yield();

        logger.LogDebug("[QuicOverlayServer] ExecuteAsync called");
        if (!options.Enable)
        {
            logger.LogInformation("[Overlay-QUIC] Disabled by configuration");
            return;
        }

        if (!QuicListener.IsSupported)
        {
            logger.LogWarning("[Overlay-QUIC] QUIC is not supported on this platform");
            return;
        }

        try
        {
            // Generate self-signed certificate for QUIC/TLS
            using var certificate = SelfSignedCertificate.Create("CN=mesh-overlay-quic");

            var listenerOptions = new QuicListenerOptions
            {
                ListenEndPoint = new IPEndPoint(IPAddress.Any, options.QuicListenPort),
                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                ConnectionOptionsCallback = (connection, hello, token) =>
                {
                    var endpoint = connection.RemoteEndPoint as IPEndPoint;
                    return new ValueTask<QuicServerConnectionOptions>(
                        new QuicServerConnectionOptions
                        {
                            DefaultStreamErrorCode = 0x01,
                            DefaultCloseErrorCode = 0x01,
                            ServerAuthenticationOptions = new SslServerAuthenticationOptions
                            {
                                ApplicationProtocols = new List<SslApplicationProtocol> { new SslApplicationProtocol("slskdn-overlay") },
                                ServerCertificate = certificate,
                                ClientCertificateRequired = false,
                                RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                                    ValidatePinnedCertificate(endpoint, certificate, chain, errors)
                            }
                        });
                }
            };

            QuicListener? listener = null;
            try
            {
                listener = await QuicListener.ListenAsync(listenerOptions, stoppingToken);
                logger.LogInformation("[Overlay-QUIC] Listening on port {Port}", options.QuicListenPort);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
            {
                logger.LogWarning(
                    "[Overlay-QUIC] QUIC overlay port {Port} is already in use. Continuing without QUIC overlay server. " +
                    "Mesh will operate in degraded mode: DHT, relay, and hole punching will still function, " +
                    "but direct inbound QUIC connections will be unavailable.",
                    options.QuicListenPort);
                return; // Gracefully exit - mesh can still function via other transports
            }
            catch (SocketException ex)
            {
                var message = "[Overlay-QUIC] QUIC overlay failed to bind to port {Port} (error: {Error}). Continuing without QUIC overlay server. " +
                    "Mesh will operate in degraded mode: DHT, relay, and hole punching will still function, " +
                    "but direct inbound QUIC connections will be unavailable.";

                logger.LogWarning(
                    ex,
                    message,
                    options.QuicListenPort,
                    ex.SocketErrorCode);
                return; // Gracefully exit - mesh can still function via other transports
            }

            if (listener == null)
            {
                return; // Should not happen, but safety check
            }

            await using (listener)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var connection = await listener.AcceptConnectionAsync(stoppingToken);
                        var ep = connection.RemoteEndPoint as IPEndPoint;
                        if (ep != null && !connectionThrottler.ShouldAllowConnection(ep.ToString(), TransportType.DirectQuic))
                        {
                            try
                            {
                                await connection.CloseAsync(0, stoppingToken);
                            }
                            catch
                            {
                                // Ignore shutdown/close failures for rejected peers.
                            }

                            await connection.DisposeAsync();
                            continue;
                        }

                        TrackConnectionTask(HandleConnectionAsync(connection, stoppingToken));
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[Overlay-QUIC] Error accepting connection");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Overlay-QUIC] Server failed");
        }
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    private async Task HandleConnectionAsync(QuicConnection connection, CancellationToken ct)
    {
        var remoteEndPoint = connection.RemoteEndPoint as IPEndPoint;
        if (remoteEndPoint != null)
        {
            activeConnections.TryAdd(remoteEndPoint, connection);
        }

        try
        {
            await using (connection)
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var stream = await connection.AcceptInboundStreamAsync(ct);
                        if (!connectionThrottler.ShouldAllowInboundStream(remoteEndPoint?.ToString() ?? "unknown"))
                        {
                            await stream.DisposeAsync();
                            continue;
                        }

                        _ = HandleStreamAsync(stream, remoteEndPoint, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "[Overlay-QUIC] Error accepting stream from {Endpoint}", remoteEndPoint);
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Connection error from {Endpoint}", remoteEndPoint);
        }
        finally
        {
            if (remoteEndPoint != null)
            {
                activeConnections.TryRemove(remoteEndPoint, out _);
            }
        }
    }

    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("windows")]
    private async Task HandleStreamAsync(QuicStream stream, IPEndPoint? remoteEndPoint, CancellationToken ct)
    {
        try
        {
            await using (stream)
            {
                // Read ControlEnvelope (MessagePack serialized)
                var buffer = new byte[options.MaxDatagramBytes];
                var totalRead = 0;

                while (totalRead < buffer.Length)
                {
                    var read = await stream.ReadAsync(buffer.AsMemory(totalRead), ct);
                    if (read == 0) break;
                    totalRead += read;
                    if (totalRead > maxRemotePayload)
                    {
                        logger.LogWarning("[Overlay-QUIC] Payload exceeds {Max} bytes, aborting stream", maxRemotePayload);
                        return;
                    }
                }

                if (totalRead == 0)
                {
                    return;
                }

                var envelope = PayloadParser.ParseMessagePackSafely<ControlEnvelope>(buffer.AsSpan(0, totalRead).ToArray(), maxRemotePayload);
                if (envelope == null)
                {
                    return;
                }

                logger.LogDebug("[Overlay-QUIC] Received control {Type} from {Endpoint}", envelope.Type, remoteEndPoint);

                var handled = await dispatcher.HandleAsync(envelope, ct);
                if (!handled)
                {
                    logger.LogWarning("[Overlay-QUIC] Failed to handle envelope {Type} from {Endpoint}", envelope.Type, remoteEndPoint);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay-QUIC] Stream error from {Endpoint}", remoteEndPoint);
        }
    }

    private bool ValidatePinnedCertificate(
        IPEndPoint? endpoint,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (endpoint is null)
        {
            logger.LogDebug("[Overlay-QUIC] Rejecting TLS handshake: remote endpoint unavailable");
            return false;
        }

        if (certificate == null)
        {
            logger.LogDebug(
                "[Overlay-QUIC] Rejecting TLS handshake from {Endpoint}: no certificate provided",
                endpoint);
            return false;
        }

        if (!IsAllowedInsecurePinnedCertificate(certificate, chain, sslPolicyErrors))
        {
            logger.LogDebug(
                "[Overlay-QUIC] Rejecting TLS handshake from {Endpoint}: TLS policy errors {Errors} are disallowed",
                endpoint,
                sslPolicyErrors);
            return false;
        }

        using var cert2 = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        var presentedPin = Mesh.Transport.SecurityUtils.ExtractSpkiPin(cert2);
        if (string.IsNullOrWhiteSpace(presentedPin))
        {
            logger.LogWarning(
                "[Overlay-QUIC] Rejecting TLS handshake from {Endpoint}: failed to extract SPKI pin",
                endpoint);
            return false;
        }

        if (_pinnedRemoteCertificates.TryGetValue(endpoint, out var expectedPin) && expectedPin == presentedPin)
        {
            logger.LogDebug(
                "[Overlay-QUIC] Accepted pinned certificate for {Endpoint}",
                endpoint);

            return true;
        }

        if (_pinnedRemoteCertificates.TryGetValue(endpoint, out var priorPin))
        {
            logger.LogWarning(
                "[Overlay-QUIC] Rotating pinned certificate for {Endpoint} due mismatch (old={OldPin}, new={NewPin})",
                endpoint,
                priorPin,
                presentedPin);
        }
        else
        {
            logger.LogInformation(
                "[Overlay-QUIC] First contact with {Endpoint}; pinning certificate {Pin}",
                endpoint,
                presentedPin);
        }

        _pinnedRemoteCertificates[endpoint] = presentedPin;
        return true;
    }

    private static bool IsAllowedInsecurePinnedCertificate(X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (certificate == null || sslPolicyErrors == SslPolicyErrors.None)
        {
            return sslPolicyErrors == SslPolicyErrors.None;
        }

        if (sslPolicyErrors != SslPolicyErrors.RemoteCertificateChainErrors)
        {
            return false;
        }

        var certificate2 = certificate as X509Certificate2;
        if (certificate2 == null || !string.Equals(certificate2.Subject, certificate2.Issuer, StringComparison.Ordinal))
        {
            return false;
        }

        if (chain == null || chain.ChainStatus.Length == 0)
        {
            return false;
        }

        foreach (var status in chain.ChainStatus)
        {
            if (status.Status != X509ChainStatusFlags.UntrustedRoot &&
                status.Status != X509ChainStatusFlags.PartialChain)
            {
                return false;
            }
        }

        return true;
    }

    private void TrackConnectionTask(Task task)
    {
        var taskId = Interlocked.Increment(ref nextConnectionTaskId);
        activeConnectionTasks.TryAdd(taskId, task);
        _ = task.ContinueWith(
            _ =>
            {
                if (activeConnectionTasks.TryRemove(taskId, out var removedTask))
                {
                    _ = removedTask;
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task CloseActiveConnectionsAsync()
    {
        var connections = activeConnections.Values.Distinct().ToArray();
        foreach (var connection in connections)
        {
            try
            {
                await connection.CloseAsync(0, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "[Overlay-QUIC] Failed to close active connection during stop");
            }
        }
    }

    private async Task DrainConnectionTasksAsync(CancellationToken cancellationToken)
    {
        var tasks = activeConnectionTasks.Values.ToArray();
        if (tasks.Length == 0)
        {
            return;
        }

        try
        {
            await Task.WhenAll(tasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host shutdown timeout should not surface as a second failure here.
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC] Error draining active connection tasks during stop");
        }
    }
}
