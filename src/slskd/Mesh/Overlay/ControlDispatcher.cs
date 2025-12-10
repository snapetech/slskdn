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
    private readonly IControlSigner signer;

    public ControlDispatcher(ILogger<ControlDispatcher> logger, IControlSigner signer)
    {
        this.logger = logger;
        this.signer = signer;
    }

    public Task<bool> HandleAsync(ControlEnvelope envelope, CancellationToken ct = default)
    {
        if (!signer.Verify(envelope))
        {
            logger.LogWarning("[Overlay] Reject envelope: signature invalid");
            return Task.FromResult(false);
        }

        logger.LogDebug("[Overlay] Received control {Type} ts={Ts}", envelope.Type, envelope.TimestampUnixMs);
        return Task.FromResult(true);
    }
}
