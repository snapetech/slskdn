using MessagePack;
using Microsoft.Extensions.Logging;

namespace slskd.Mesh.Overlay;

/// <summary>
/// Handles overlay control envelopes (signature verification stub).
/// </summary>
public interface IControlDispatcher
{
    Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default);
}

public class ControlDispatcher : IControlDispatcher
{
    private readonly ILogger<ControlDispatcher> logger;

    public ControlDispatcher(ILogger<ControlDispatcher> logger)
    {
        this.logger = logger;
    }

    public Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default)
    {
        // TODO: verify signature; for now accept if present
        if (string.IsNullOrWhiteSpace(envelope.PublicKey) || string.IsNullOrWhiteSpace(envelope.Signature))
        {
            logger.LogWarning("[Overlay] Reject envelope: missing signature");
            return Task.FromResult(false);
        }

        logger.LogDebug("[Overlay] Received control {Type} ts={Ts}", envelope.Type, envelope.TimestampUnixMs);
        return Task.FromResult(true);
    }
}
