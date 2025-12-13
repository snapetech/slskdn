using System.Text;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using slskd.Mesh.Dht;
using slskd.VirtualSoulfind.ShadowIndex;

namespace slskd.MediaCore;

/// <summary>
/// Publishes content descriptors to DHT with signing/TTL guardrails.
/// </summary>
public interface IDescriptorPublisher
{
    Task<bool> PublishAsync(ContentDescriptor descriptor, CancellationToken ct = default);
}

public class DescriptorPublisher : IDescriptorPublisher
{
    private readonly ILogger<DescriptorPublisher> logger;
    private readonly IDescriptorValidator validator;
    private readonly IMeshDhtClient dht;
    private readonly MediaCoreOptions options;

    public DescriptorPublisher(
        ILogger<DescriptorPublisher> logger,
        IDescriptorValidator validator,
        IMeshDhtClient dht,
        IOptions<MediaCoreOptions> options)
    {
        this.logger = logger;
        this.validator = validator;
        this.dht = dht;
        this.options = options.Value;
    }

    public async Task<bool> PublishAsync(ContentDescriptor descriptor, CancellationToken ct = default)
    {
        if (!validator.Validate(descriptor, out var reason))
        {
            logger.LogWarning("[MediaCore] Descriptor invalid: {Reason}", reason);
            return false;
        }

        var key = $"mesh:content:{descriptor.ContentId}";

        // TTL capped by options (minutes)
        var ttlSeconds = Math.Min(options.MaxTtlMinutes, 60) * 60;

        try
        {
            await dht.PutAsync(key, descriptor, ttlSeconds, ct);
            logger.LogInformation("[MediaCore] Published descriptor {ContentId} (ttl={Ttl}s)", descriptor.ContentId, ttlSeconds);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MediaCore] Failed to publish descriptor {ContentId}: {Message}", descriptor.ContentId, ex.Message);
            return false;
        }
    }
}
