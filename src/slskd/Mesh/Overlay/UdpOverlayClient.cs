using System.Net;
using System.Net.Sockets;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh.Overlay;

/// <summary>
/// UDP overlay client to send control envelopes.
/// </summary>
public interface IOverlayClient
{
    Task<bool> SendAsync(ControlEnvelope envelope, IPEndPoint endpoint, CancellationToken ct = default);
}

public class UdpOverlayClient : IOverlayClient
{
    private readonly ILogger<UdpOverlayClient> logger;
    private readonly OverlayOptions options;

    public UdpOverlayClient(ILogger<UdpOverlayClient> logger, IOptions<OverlayOptions> options)
    {
        this.logger = logger;
        this.options = options.Value;
    }

    public async Task<bool> SendAsync(ControlEnvelope envelope, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable) return false;

        var payload = MessagePackSerializer.Serialize(envelope);
        if (payload.Length > options.MaxDatagramBytes)
        {
            logger.LogWarning("[Overlay] Refusing to send oversized envelope size={Size}", payload.Length);
            return false;
        }

        using var udp = new UdpClient();
        try
        {
            await udp.SendAsync(payload, payload.Length, endpoint, ct);
            logger.LogDebug("[Overlay] Sent control {Type} to {Endpoint}", envelope.Type, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay] Failed to send control to {Endpoint}", endpoint);
            return false;
        }
    }
}
