using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Privacy;

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
    private readonly IPrivacyLayer? privacyLayer;

    public UdpOverlayClient(
        ILogger<UdpOverlayClient> logger,
        IOptions<OverlayOptions> options,
        IPrivacyLayer? privacyLayer = null)
    {
        this.logger = logger;
        this.options = options.Value;
        this.privacyLayer = privacyLayer;
    }

    public async Task<bool> SendAsync(ControlEnvelope envelope, IPEndPoint endpoint, CancellationToken ct = default)
    {
        if (!options.Enable) return false;

        try
        {
            // Apply outbound privacy transforms if enabled
            if (privacyLayer != null && privacyLayer.IsEnabled && envelope.Payload != null && envelope.Payload.Length > 0)
            {
                envelope.Payload = await privacyLayer.ProcessOutboundMessageAsync(envelope.Payload, ct);
                if (envelope.Payload.Length == 0)
                {
                    // Message was queued for batching, not ready to send yet
                    logger.LogTrace("[Overlay] Message queued for batching, not sending immediately");
                    return true; // Not an error, just delayed
                }
                logger.LogTrace("[Overlay] Applied outbound privacy transforms to envelope {MessageId}", envelope.MessageId);
            }

            // Check for pending batches to send
            if (privacyLayer != null)
            {
                var batches = privacyLayer.GetPendingBatches();
                if (batches != null && batches.Count > 0)
                {
                    // Send batched messages first
                    foreach (var batchPayload in batches)
                    {
                        if (!await SendPayloadAsync(batchPayload, endpoint, ct))
                        {
                            return false;
                        }
                    }
                    logger.LogDebug("[Overlay] Sent {Count} batched messages to {Endpoint}", batches.Count, endpoint);
                }
            }

            // Serialize envelope
            var payload = MessagePackSerializer.Serialize(envelope);
            if (payload.Length > options.MaxDatagramBytes)
            {
                logger.LogWarning("[Overlay] Refusing to send oversized envelope size={Size}", payload.Length);
                return false;
            }

            // Send the main payload
            var success = await SendPayloadAsync(payload, endpoint, ct);

            if (success && privacyLayer != null)
            {
                // Apply timing delay if enabled
                var delay = privacyLayer.GetOutboundDelay();
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, ct);
                    logger.LogTrace("[Overlay] Applied timing delay {Delay}ms", delay.TotalMilliseconds);
                }

                // Record the outbound message for privacy layer tracking
                privacyLayer.RecordOutboundMessage();
            }

            return success;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay] Failed to send control to {Endpoint}", endpoint);
            return false;
        }
    }

    private async Task<bool> SendPayloadAsync(byte[] payload, IPEndPoint endpoint, CancellationToken ct)
    {
        using var udp = new UdpClient();
        try
        {
            await udp.SendAsync(payload, endpoint, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Overlay] Failed to send payload to {Endpoint}: {Message}", endpoint, ex.Message);
            return false;
        }
    }
}
