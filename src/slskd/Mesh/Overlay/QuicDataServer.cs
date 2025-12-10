using System.Net;
using System.Net.Quic;
using System.Net.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

/// <summary>
/// QUIC data-plane server for bulk payloads.
/// </summary>
public class QuicDataServer : BackgroundService
{
    private readonly ILogger<QuicDataServer> logger;
    private readonly DataOverlayOptions options;
    private readonly X509Certificate2 cert;
    private QuicListener? listener;

    public QuicDataServer(
        ILogger<QuicDataServer> logger,
        IOptions<DataOverlayOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
        this.cert = SelfSignedCertificate.Create("CN=mesh-overlay-data");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enable)
        {
            logger.LogInformation("[Overlay-QUIC-DATA] Disabled");
            return;
        }

        var listenerOpts = new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Any, options.ListenPort),
            ApplicationProtocols = new List<SslApplicationProtocol> { new("mesh-overlay-data") },
            ConnectionOptionsCallback = _ => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = new List<SslApplicationProtocol> { new("mesh-overlay-data") },
                    ServerCertificate = cert
                }
            })
        };

        listener = await QuicListener.ListenAsync(listenerOpts, stoppingToken);
        logger.LogInformation("[Overlay-QUIC-DATA] Listening on {Port}", options.ListenPort);

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
                logger.LogWarning(ex, "[Overlay-QUIC-DATA] Accept failed");
            }
        }
    }

    private async Task HandleConnection(QuicConnection conn, CancellationToken ct)
    {
        try
        {
            await using var stream = await conn.AcceptStreamAsync(ct);
            using var ms = new MemoryStream();
            var buffer = new byte[options.ReceiveBufferBytes];
            int read;
            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (ms.Length + read > options.MaxPayloadBytes)
                {
                    logger.LogWarning("[Overlay-QUIC-DATA] Payload too large; closing");
                    break;
                }
                ms.Write(buffer, 0, read);
            }
            logger.LogDebug("[Overlay-QUIC-DATA] Received payload size={Size}", ms.Length);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC-DATA] Connection handler error");
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
