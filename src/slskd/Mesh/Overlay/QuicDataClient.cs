using System.Net;
using System.Net.Quic;
using System.Net.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

/// <summary>
/// QUIC data-plane client for bulk payloads.
/// </summary>
public class QuicDataClient : IOverlayDataPlane
{
    private readonly ILogger<QuicDataClient> logger;
    private readonly DataOverlayOptions options;

    public QuicDataClient(ILogger<QuicDataClient> logger, IOptions<DataOverlayOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task<bool> SendAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable) return false;
        if (payload.Length > options.MaxPayloadBytes)
        {
            logger.LogWarning("[Overlay-QUIC-DATA] Refusing payload size={Size} > {Max}", payload.Length, options.MaxPayloadBytes);
            return false;
        }

        var clientOpts = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endpoint,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { new("mesh-overlay-data") },
                RemoteCertificateValidationCallback = (_, _, _, _) => true // accept self-signed
            }
        };

        try
        {
            using var conn = await QuicConnection.ConnectAsync(clientOpts, ct);
            await using var stream = await conn.OpenUnidirectionalStreamAsync(ct);
            await stream.WriteAsync(payload, ct);
            await stream.ShutdownWriteCompleted();
            logger.LogDebug("[Overlay-QUIC-DATA] Sent payload size={Size} to {Endpoint}", payload.Length, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC-DATA] Failed to send to {Endpoint}", endpoint);
            return false;
        }
    }
}
