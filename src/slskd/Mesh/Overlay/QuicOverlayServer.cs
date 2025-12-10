using System.Net;
using System.Net.Quic;
using System.Net.Security;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

/// <summary>
/// QUIC overlay server for control envelopes.
/// </summary>
public class QuicOverlayServer : BackgroundService
{
    private readonly ILogger<QuicOverlayServer> logger;
    private readonly OverlayOptions options;
    private readonly IControlDispatcher dispatcher;
    private readonly X509Certificate2 cert;
    private QuicListener? listener;

    public QuicOverlayServer(
        ILogger<QuicOverlayServer> logger,
        IOptions<OverlayOptions> options,
        IControlDispatcher dispatcher)
    {
        this.logger = logger;
        this.options = options.Value;
        this.dispatcher = dispatcher;
        this.cert = SelfSignedCertificate.Create();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enable)
        {
            logger.LogInformation("[Overlay-QUIC] Disabled");
            return;
        }

        var listenerOpts = new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort),
            ApplicationProtocols = new List<SslApplicationProtocol> { new("mesh-overlay") },
            ConnectionOptionsCallback = _ => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { new("mesh-overlay") },
                    ServerCertificate = cert
                }
            })
        };

        listener = await QuicListener.ListenAsync(listenerOpts, stoppingToken);
        logger.LogInformation("[Overlay-QUIC] Listening on {Port}", options.ListenPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var conn = await listener.AcceptConnectionAsync(stoppingToken);
                _ = HandleConnection(conn, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutdown
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Overlay-QUIC] Accept failed");
            }
        }
    }

    private async Task HandleConnection(QuicConnection conn, CancellationToken ct)
    {
        try
        {
            await using var stream = await conn.AcceptStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            var envelope = MessagePackSerializer.Deserialize<ControlEnvelope>(ms.ToArray());
            await dispatcher.HandleAsync(envelope, ct);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC] Connection handler error");
        }
        finally
        {
            await conn.DisposeAsync();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (listener != null)
        {
            await listener.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
