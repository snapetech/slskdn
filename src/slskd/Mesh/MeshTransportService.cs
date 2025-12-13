using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace slskd.Mesh;

/// <summary>
/// Mesh transport service that honors user-configured transport preference.
/// </summary>
public interface IMeshTransportService
{
    MeshTransportPreference Preference { get; }
    Task<MeshTransportDecision> ChooseTransportAsync(string contentId, CancellationToken ct = default);
}

public record MeshTransportDecision(MeshTransportPreference Preference, string Reason);

public class MeshTransportService : IMeshTransportService
{
    private readonly ILogger<MeshTransportService> logger;
    private readonly IOptions<MeshOptions> options;

    public MeshTransportService(ILogger<MeshTransportService> logger, IOptions<MeshOptions> options)
    {
        this.logger = logger;
        this.options = options;
    }

    public MeshTransportPreference Preference => options.Value.TransportPreference;

    public Task<MeshTransportDecision> ChooseTransportAsync(string contentId, CancellationToken ct = default)
    {
        var opt = options.Value;
        var reason = Preference switch
        {
            MeshTransportPreference.DhtFirst => "DHT-first for efficiency",
            MeshTransportPreference.Mirrored => "Mirrored DHT+overlay for resiliency",
            MeshTransportPreference.OverlayFirst => "Overlay-first for private paths",
            _ => "Default"
        };

        logger.LogDebug("[MeshTransport] {ContentId}: {Preference} ({Reason})", contentId, Preference, reason);
        return Task.FromResult(new MeshTransportDecision(Preference, reason));
    }
}















