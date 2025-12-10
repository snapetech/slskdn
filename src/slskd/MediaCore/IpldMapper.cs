using System.Text.Json;

namespace slskd.MediaCore;

/// <summary>
/// Maps descriptors to IPLD-compatible shape (dag-cbor/json).
/// Feature-flagged; IPFS publishing is optional.
/// </summary>
public interface IIpldMapper
{
    string ToJson(ContentDescriptor descriptor);
}

public class IpldMapper : IIpldMapper
{
    public string ToJson(ContentDescriptor descriptor)
    {
        var ipld = new
        {
            contentId = descriptor.ContentId,
            hashes = descriptor.Hashes,
            phash = descriptor.PerceptualHashes,
            size = descriptor.SizeBytes,
            codec = descriptor.Codec,
            confidence = descriptor.Confidence,
            sig = descriptor.Signature
        };

        return JsonSerializer.Serialize(ipld);
    }
}
