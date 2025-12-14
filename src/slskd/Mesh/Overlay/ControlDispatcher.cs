using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Dht;
using slskd.Mesh.Transport;
using slskd.Mesh.Privacy;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Handles overlay control envelopes with comprehensive security validation.
/// </summary>
public interface IControlDispatcher
{
    Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default);
    Task<bool> HandleAsync(ControlEnvelope envelope, slskd.Mesh.Dht.MeshPeerDescriptor peerDescriptor, string peerId, CancellationToken ct = default);
}

public class ControlDispatcher : IControlDispatcher
{
    private readonly ILogger<ControlDispatcher> logger;
    private readonly ControlEnvelopeValidator validator;
    private readonly IPrivacyLayer? privacyLayer;

    public ControlDispatcher(
        ILogger<ControlDispatcher> logger,
        ControlEnvelopeValidator validator,
        IPrivacyLayer? privacyLayer = null)
    {
        this.logger = logger;
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.privacyLayer = privacyLayer;
    }

    /// <summary>
    /// Legacy method for backward compatibility - validates without peer descriptor.
    /// Use HandleAsync(envelope, peerDescriptor, peerId) for full security validation.
    /// </summary>
    public Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default)
    {
        logger.LogWarning("[Overlay] Using legacy envelope handling without peer validation - security reduced");
        return HandleControlLogicAsync(envelope);
    }

    /// <summary>
    /// Handles envelope with full peer-bound security validation.
    /// </summary>
    public async Task<bool> HandleAsync(ControlEnvelope envelope, slskd.Mesh.Dht.MeshPeerDescriptor peerDescriptor, string peerId, CancellationToken ct = default)
    {
        // Apply privacy transforms to inbound payload if privacy layer is enabled
        if (privacyLayer != null && privacyLayer.IsEnabled && envelope.Payload != null && envelope.Payload.Length > 0)
        {
            try
            {
                envelope.Payload = await privacyLayer.ProcessInboundMessageAsync(envelope.Payload, ct);
                logger.LogTrace("[Overlay] Applied inbound privacy transforms to envelope {MessageId}", envelope.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Overlay] Failed to apply inbound privacy transforms to envelope {MessageId}", envelope.MessageId);
                return false;
            }
        }

        // Perform comprehensive security validation
        var validationResult = validator.ValidateEnvelope(envelope, peerDescriptor, peerId);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("[Overlay] Reject envelope from peer {PeerId}: {Error}",
                peerId, validationResult.ErrorMessage);
            return false;
        }

        logger.LogDebug("[Overlay] Received validated control {Type} from peer {PeerId}, message {MessageId}",
            envelope.Type, peerId, envelope.MessageId);

        return await HandleControlLogicAsync(envelope);
    }

    private Task<bool> HandleControlLogicAsync(ControlEnvelope envelope)
    {
        switch (envelope.Type)
        {
            case OverlayControlTypes.Ping:
                // No payload; accept
                return Task.FromResult(true);
            case OverlayControlTypes.Pong:
                return Task.FromResult(true);
            case OverlayControlTypes.Probe:
                return Task.FromResult(true);
            default:
                logger.LogDebug("[Overlay] Unknown control type {Type}", envelope.Type);
                return Task.FromResult(false);
        }
    }
}
