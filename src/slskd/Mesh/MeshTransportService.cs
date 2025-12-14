using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Common.Security;

namespace slskd.Mesh;

/// <summary>
/// Mesh transport service that honors user-configured transport preference and anonymity settings.
/// </summary>
public interface IMeshTransportService
{
    MeshTransportPreference Preference { get; }
    Task<MeshTransportDecision> ChooseTransportAsync(string contentId, CancellationToken ct = default);
    Task<MeshTransportDecision> ChooseTransportAsync(string peerId, string? podId, string contentId, CancellationToken ct = default);
}

public record MeshTransportDecision(MeshTransportPreference Preference, string Reason, AnonymityTransportType? AnonymityTransport = null);

public class MeshTransportService : IMeshTransportService
{
    private readonly ILogger<MeshTransportService> logger;
    private readonly IOptions<MeshOptions> options;
    private readonly IAnonymityTransportSelector? anonymitySelector;
    private readonly IOptions<AdversarialOptions>? adversarialOptions;

    public MeshTransportService(
        ILogger<MeshTransportService> logger,
        IOptions<MeshOptions> options,
        IAnonymityTransportSelector? anonymitySelector = null,
        IOptions<AdversarialOptions>? adversarialOptions = null)
    {
        this.logger = logger;
        this.options = options;
        this.anonymitySelector = anonymitySelector;
        this.adversarialOptions = adversarialOptions;
    }

    public MeshTransportPreference Preference => options.Value.TransportPreference;

    /// <summary>
    /// Legacy method for backward compatibility - chooses transport without peer/pod context.
    /// </summary>
    public Task<MeshTransportDecision> ChooseTransportAsync(string contentId, CancellationToken ct = default)
    {
        return ChooseTransportAsync(peerId: null, podId: null, contentId, ct);
    }

    /// <summary>
    /// Chooses the appropriate transport considering anonymity settings and per-peer policies.
    /// </summary>
    /// <param name="peerId">The target peer ID (optional, for policy-aware selection).</param>
    /// <param name="podId">The pod ID (optional, for policy-aware selection).</param>
    /// <param name="contentId">The content ID being transported.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transport decision.</returns>
    public async Task<MeshTransportDecision> ChooseTransportAsync(string? peerId, string? podId, string contentId, CancellationToken ct = default)
    {
        var opt = options.Value;
        var basePreference = Preference;

        // Check if anonymity features are enabled and should override transport selection
        AnonymityTransportType? anonymityTransport = null;
        if (adversarialOptions?.Value.AnonymityLayer.Enabled == true && anonymitySelector != null)
        {
            var anonymityMode = adversarialOptions.Value.AnonymityLayer.Mode;

            // For non-direct anonymity modes, try to select an appropriate anonymity transport
            if (anonymityMode != AnonymityMode.Direct)
            {
                try
                {
                    // Use policy-aware transport selection if peer context is available
                    var transportTuple = await anonymitySelector.SelectAndConnectAsync(peerId ?? "unknown", podId, "dummy-host", 0, ct: ct);
                    anonymityTransport = transportTuple.Transport.TransportType;

                    logger.LogDebug("[MeshTransport] {ContentId}: Selected anonymity transport {AnonymityTransport} for peer {PeerId}",
                        contentId, anonymityTransport, peerId ?? "unknown");

                    // When using anonymity transports, prefer overlay routing to maintain privacy
                    basePreference = MeshTransportPreference.OverlayFirst;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[MeshTransport] Failed to select anonymity transport for {ContentId}, falling back to standard routing", contentId);
                }
            }
        }

        var reason = basePreference switch
        {
            MeshTransportPreference.DhtFirst => anonymityTransport.HasValue
                ? $"DHT-first with {anonymityTransport.Value} anonymity"
                : "DHT-first for efficiency",
            MeshTransportPreference.Mirrored => anonymityTransport.HasValue
                ? $"Mirrored with {anonymityTransport.Value} anonymity"
                : "Mirrored DHT+overlay for resiliency",
            MeshTransportPreference.OverlayFirst => anonymityTransport.HasValue
                ? $"Overlay-first with {anonymityTransport.Value} anonymity"
                : "Overlay-first for private paths",
            _ => "Default"
        };

        logger.LogDebug("[MeshTransport] {ContentId}: {Preference} ({Reason})", contentId, basePreference, reason);
        return new MeshTransportDecision(basePreference, reason, anonymityTransport);
    }
}

