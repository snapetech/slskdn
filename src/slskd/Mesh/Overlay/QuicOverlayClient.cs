using System.Net;
using System.Net.Quic;
using System.Net.Security;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

/// <summary>
/// QUIC overlay client to send control envelopes.
/// </summary>
public class QuicOverlayClient : IOverlayClient
{
    private readonly ILogger<QuicOverlayClient> logger;
    private readonly OverlayOptions options;
    private readonly IControlSigner signer;

    public QuicOverlayClient(
        ILogger<QuicOverlayClient> logger,
        IOptions<OverlayOptions> options,
        IControlSigner signer)
    {
        this.logger = logger;
        this.options = options.Value;
        this.signer = signer;
    }

    public async Task<bool> SendAsync(ControlEnvelope envelope, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable) return false;

        var signed = signer.Sign(envelope);
        var payload = MessagePackSerializer.Serialize(signed);

        var clientOpts = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endpoint,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { new("mesh-overlay") },
                RemoteCertificateValidationCallback = (_, _, _, _) => true // accept self-signed
            }
        };

        try
        {
            using var conn = await QuicConnection.ConnectAsync(clientOpts, ct);
            await using var stream = await conn.OpenUnidirectionalStreamAsync(ct);
            await stream.WriteAsync(payload, ct);
            await stream.ShutdownWriteCompleted();
            logger.LogDebug("[Overlay-QUIC] Sent control {Type} to {Endpoint}", envelope.Type, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Overlay-QUIC] Failed to send to {Endpoint}", endpoint);
            return false;
        }
    }
}
