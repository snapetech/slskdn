using MessagePack;
using Microsoft.Extensions.Logging;
using slskd.Mesh.Security;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Handles overlay control envelopes with peer-aware signature verification.
/// </summary>
public interface IControlDispatcher
{
    Task<bool> HandleAsync(ControlEnvelope envelope, PeerContext peer, CancellationToken ct = default);
}

public class ControlDispatcher : IControlDispatcher
{
    private readonly ILogger<ControlDispatcher> logger;
    private readonly IControlVerification verification;
    private readonly IReplayCache replayCache;

    public ControlDispatcher(
        ILogger<ControlDispatcher> logger,
        IControlVerification verification,
        IReplayCache replayCache)
    {
        this.logger = logger;
        this.verification = verification;
        this.replayCache = replayCache;
    }

    public Task<bool> HandleAsync(ControlEnvelope envelope, PeerContext peer, CancellationToken ct = default)
    {
        // 1. Check for replay attacks
        if (!replayCache.ValidateAndRecord(peer.PeerId, envelope))
        {
            logger.LogWarning(
                "[Overlay] Reject envelope from {PeerId}: replay or timestamp skew (msgId: {MessageId})",
                peer.PeerId,
                envelope.MessageId);
            return Task.FromResult(false);
        }

        // 2. Verify signature against peer's allowed keys (NOT self-asserted key)
        if (!verification.Verify(envelope, peer.AllowedControlSigningKeys))
        {
            logger.LogWarning(
                "[Overlay] Reject envelope from {PeerId}: signature invalid (msgId: {MessageId})",
                peer.PeerId,
                envelope.MessageId);
            return Task.FromResult(false);
        }

        logger.LogDebug(
            "[Overlay] Received control {Type} from {PeerId} via {Transport} (msgId: {MessageId})",
            envelope.Type,
            peer.PeerId,
            peer.Transport,
            envelope.MessageId);

        switch (envelope.Type)
        {
            case OverlayControlTypes.Ping:
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
